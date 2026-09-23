using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CoastRacer.Core;

var staticRoot=Path.Combine(AppContext.BaseDirectory,"wwwroot");
if(!Directory.Exists(staticRoot))staticRoot=Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),"../Builds/site"));
var builder=WebApplication.CreateBuilder(new WebApplicationOptions{Args=args,WebRootPath=staticRoot});
builder.Services.AddSingleton<ResultStore>();
builder.Services.AddSingleton<RaceHub>();
builder.Services.AddHostedService(sp=>sp.GetRequiredService<RaceHub>());
var app=builder.Build();
app.UseWebSockets(new WebSocketOptions{KeepAliveInterval=TimeSpan.FromSeconds(20)});
app.Use(async(context,next)=>{
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    context.Response.Headers["Referrer-Policy"]="same-origin";
    await next();
});
app.UseDefaultFiles();
var provider=new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".wasm"]="application/wasm";provider.Mappings[".data"]="application/octet-stream";
app.Use(async(context,next)=>{
    // Serve offline-compressed public game assets, avoiding compression CPU work on F1.
    string requestPath=context.Request.Path.Value??"";
    bool acceptsBr=context.Request.Headers.AcceptEncoding.ToString().Split(',').Any(x=>x.Trim()=="br");
    if(acceptsBr && (HttpMethods.IsGet(context.Request.Method)||HttpMethods.IsHead(context.Request.Method)) && requestPath.StartsWith("/play/Build/",StringComparison.Ordinal)){
        string file=Path.GetFileName(requestPath);
        if(provider.TryGetContentType(file,out var contentType)){
            string compressed=Path.Combine(staticRoot,"play","Build",file+".br");
            if(File.Exists(compressed)){
                context.Response.ContentType=contentType;context.Response.Headers.ContentEncoding="br";
                context.Response.Headers.Vary="Accept-Encoding";context.Response.Headers.CacheControl="no-cache";
                context.Response.ContentLength=new FileInfo(compressed).Length;
                if(!HttpMethods.IsHead(context.Request.Method))await context.Response.SendFileAsync(compressed,context.RequestAborted);
                return;
            }
        }
    }
    await next();
});
app.UseStaticFiles(new StaticFileOptions{ContentTypeProvider=provider});
app.MapGet("/health",()=>Results.Ok(new{status="ok",protocol=3}));
app.MapGet("/api/courses",()=>new[]{new{id="ridge",name="山岳サーキット"},new{id="suzuka",name="鈴鹿サーキット"}});
app.MapGet("/api/records/{track}",async(string track,ResultStore store,CancellationToken ct)=>{
    if(track!="ridge"&&track!="suzuka")return Results.BadRequest();
    try{return Results.Ok(await store.Read(track+"-2lap",ct));}catch(Exception ex){if(ex is Microsoft.Data.SqlClient.SqlException firewall && firewall.Number==40615){var ip=System.Text.RegularExpressions.Regex.Match(firewall.Message,@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b").Value;app.Logger.LogError("SQL_RECORDS_FAILURE type=Firewall clientIp={ClientIp}",ip);}app.Logger.LogError("SQL_RECORDS_FAILURE type={Type} sqlNumber={Number} innerType={InnerType}",ex.GetType().Name,ex is Microsoft.Data.SqlClient.SqlException sql?sql.Number:0,ex.InnerException?.GetType().Name);return Results.Json(new{error="RECORDS_UNAVAILABLE"},statusCode:503);}
});
app.Map("/ws",async(HttpContext context,RaceHub hub)=>{
    if(!context.WebSockets.IsWebSocketRequest){context.Response.StatusCode=400;return;}
    string origin=context.Request.Headers.Origin.ToString();
    if(origin.Length>0 && (!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||!string.Equals(uri.Authority,context.Request.Host.Value,StringComparison.OrdinalIgnoreCase))){context.Response.StatusCode=403;return;}
    if(!hub.TryConnect()){context.Response.StatusCode=429;return;}
    using var ws=await context.WebSockets.AcceptWebSocketAsync();
    try{await hub.Handle(ws,context.RequestAborted);}finally{hub.DisconnectSlot();}
});
app.Run();

public sealed class RaceHub:BackgroundService
{
    public static readonly JsonSerializerOptions Json=new(){IncludeFields=true,PropertyNamingPolicy=null};
    readonly object gate=new();
    readonly Dictionary<string,Room> rooms=new();
    readonly ResultStore store;
    readonly int limit;
    readonly ILogger<RaceHub> logger;
    int connections;
    public RaceHub(ResultStore s,IConfiguration config,ILogger<RaceHub> log){store=s;logger=log;limit=Math.Clamp(config.GetValue("Racer:MaxPlayers",2),2,4);}
    public bool TryConnect(){lock(gate){if(connections>=limit)return false;connections++;return true;}}
    public void DisconnectSlot(){lock(gate)connections--;}
    public async Task Handle(WebSocket socket,CancellationToken ct)
    {
        var peer=new Peer(socket);var sender=peer.SendLoop(ct);Player? player=null;Room? room=null;
        var buffer=new byte[4096];int burst=0;long rateEpoch=Environment.TickCount64;
        try{
            while(socket.State==WebSocketState.Open&&!ct.IsCancellationRequested){
                var result=await socket.ReceiveAsync(buffer,ct);
                if(result.MessageType==WebSocketMessageType.Close)break;
                if(!result.EndOfMessage||result.MessageType!=WebSocketMessageType.Text){peer.Post(new{type="error",code="INVALID_MESSAGE"});break;}
                if(Environment.TickCount64-rateEpoch>1000){burst=0;rateEpoch=Environment.TickCount64;}
                if(++burst>60)break;
                JsonDocument doc;
                try{doc=JsonDocument.Parse(buffer.AsMemory(0,result.Count));}catch{peer.Post(new{type="error",code="INVALID_MESSAGE"});continue;}
                using(doc){
                    var root=doc.RootElement;if(root.ValueKind!=JsonValueKind.Object){peer.Post(new{type="error",code="INVALID_MESSAGE"});continue;}string action=GetString(root,"action");
                    lock(gate){
                        if(player==null){
                            if(action=="resume"){
                                string token=GetString(root,"token");
                                room=rooms.Values.FirstOrDefault(r=>r.players.Any(p=>p.token==token));
                                player=room?.players.FirstOrDefault(p=>p.token==token && p.disconnectedAt>0 && Environment.TickCount64-p.disconnectedAt<20000 && !p.state.dnf);
                                if(player==null){peer.Post(new{type="error",code="SESSION_EXPIRED"});continue;}
                                player.peer=peer;player.disconnectedAt=0;player.state.connected=true;
                            }else if(action=="create"||action=="join"){
                                if(action=="create"){
                                    if(rooms.Count>=4){peer.Post(new{type="error",code="SERVER_FULL"});continue;}
                                    string code;do{code=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(3));}while(rooms.ContainsKey(code));
                                    room=new Room(code,GetString(root,"track")=="suzuka"?"suzuka":"ridge");rooms.Add(code,room);
                                }else rooms.TryGetValue(GetString(root,"code").ToUpperInvariant(),out room);
                                if(room==null){peer.Post(new{type="error",code="ROOM_NOT_FOUND"});continue;}
                                if(room.phase!="lobby"&&room.phase!="finished"){peer.Post(new{type="error",code="RACE_RUNNING"});room=null;continue;}
                                if(room.players.Count>=limit){peer.Post(new{type="error",code="ROOM_FULL"});room=null;continue;}
                                string name=GetString(root,"name").Trim();if(name.Length==0)name="ドライバー";if(name.Length>16)name=name[..16];
                                name=new string(name.Where(c=>!char.IsControl(c)).ToArray());
                                player=new Player{token=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)),peer=peer,state=Simulation.Spawn(room.track,room.players.Count)};
                                player.state.id=Guid.NewGuid().ToString("N");player.state.name=name;player.state.vehicle=Vehicles.Get(GetString(root,"vehicle")).id;player.state.badge=Vehicles.Badge(GetString(root,"badge"));room.players.Add(player);
                                if(room.owner=="")room.owner=player.state.id;
                            }else{peer.Post(new{type="error",code="JOIN_FIRST"});continue;}
                            peer.Post(new{type="joined",code=room!.code,token=player.token,id=player.state.id,maxPlayers=limit});
                            BroadcastLobby(room);continue;
                        }
                        if(room==null)continue;
                        if(action=="profile"){
                            string name=GetString(root,"name");if(name.Length>0)player.state.name=new string(name.Where(c=>!char.IsControl(c)).Take(16).ToArray());
                            player.state.badge=Vehicles.Badge(GetString(root,"badge"));
                            if(room.phase=="lobby"||room.phase=="finished"){player.state.vehicle=Vehicles.Get(GetString(root,"vehicle")).id;player.ready=false;}
                            BroadcastLobby(room);
                        }
                        else if(action=="loaded"&&room.phase=="loading"&&GetString(root,"raceId")==room.raceId){player.loaded=true;}
                        else if(action=="special"&&room.phase=="race"){room.session.Activate(player.state,room.players.Select(x=>x.state).ToList());}
                        else if(action=="ready" && (room.phase=="lobby"||room.phase=="finished")){player.ready=root.TryGetProperty("ready",out var r)&&r.ValueKind==JsonValueKind.True;BroadcastLobby(room);}
                        else if(action=="track" && player.state.id==room.owner && room.phase=="lobby"){
                            room.track=new Track(GetString(root,"track"));foreach(var p in room.players)p.ready=false;BroadcastLobby(room);
                        }
                        else if(action=="start" && player.state.id==room.owner && (room.phase=="lobby"||room.phase=="finished")){
                            var participants=room.players.Where(p=>p.state.connected).ToList();
                            if(participants.Count<2||participants.Any(p=>!p.ready)){peer.Post(new{type="error",code="NOT_READY"});continue;}
                            room.players.RemoveAll(p=>!p.state.connected);room.raceId=Guid.NewGuid().ToString("N");room.phase="loading";room.loadStarted=Environment.TickCount64;room.session=new RaceSession(room.track);room.countdown=3;room.firstFinish=0;room.time=0;room.recorded=false;
                            for(int i=0;i<room.players.Count;i++){
                                var p=room.players[i];string id=p.state.id,name=p.state.name,vehicle=p.state.vehicle,badge=p.state.badge;p.state=Simulation.Spawn(room.track,i);p.state.id=id;p.state.name=name;p.state.vehicle=vehicle;p.state.badge=badge;p.input=new DriveInput();p.ready=false;p.loaded=false;
                            }
                        }
                        else if(action=="input"){
                            DriveInput value;try{value=JsonSerializer.Deserialize<DriveInput>(root.GetRawText(),Json)??new DriveInput();}catch(JsonException){peer.Post(new{type="error",code="INVALID_MESSAGE"});continue;}
                            if(!float.IsFinite(value.steer)||!float.IsFinite(value.throttle)||!float.IsFinite(value.brake)||!float.IsFinite(value.assist)||!float.IsFinite(value.sensitivity))continue;
                            value.steer=Math.Clamp(value.steer,-1,1);value.throttle=Math.Clamp(value.throttle,0,1);value.brake=Math.Clamp(value.brake,0,1);
                            value.assist=Math.Clamp(value.assist,0,1);value.sensitivity=Math.Clamp(value.sensitivity,.5f,1.6f);
                            player.input=value;player.lastInput=Environment.TickCount64;
                        }
                    }
                }
            }
        }catch(OperationCanceledException){}catch(WebSocketException){}finally{
            lock(gate)if(player!=null&&player.peer==peer){player.state.connected=false;player.disconnectedAt=Environment.TickCount64;player.input=new DriveInput{brake=1};player.peer=null;if(room!=null)BroadcastLobby(room);}
            peer.Close();socket.Abort();try{await sender;}catch{}
        }
    }
    static string GetString(JsonElement root,string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?(v.GetString()??""):"";
    void BroadcastLobby(Room r)
    {
        foreach(var p in r.players)p.peer?.Post(new{type="lobby",code=r.code,track=r.track.id,owner=r.owner,phase=r.phase,players=r.players.Select(x=>new{id=x.state.id,name=x.state.name,vehicle=x.state.vehicle,badge=x.state.badge,ready=x.ready,loaded=x.loaded,connected=x.state.connected}).ToArray()});
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while(await timer.WaitForNextTickAsync(stoppingToken)){
            var saves=new List<RaceResult[]>();
            lock(gate){
                foreach(var room in rooms.Values.ToArray()){
                    if(room.players.All(p=>!p.state.connected)&&room.players.All(p=>Environment.TickCount64-p.disconnectedAt>20000)){rooms.Remove(room.code);continue;}
                    foreach(var p in room.players){
                        if(!p.state.connected&&Environment.TickCount64-p.disconnectedAt>20000)p.state.dnf=true;
                        if(Environment.TickCount64-p.lastInput>750)p.input=new DriveInput{brake=1};
                    }
                    if(!room.players.Any(p=>p.state.id==room.owner&&p.state.connected)){
                        room.owner=room.players.FirstOrDefault(p=>p.state.connected)?.state.id??"";
                    }
                    if(room.phase=="loading"){
                        if(room.players.Any(p=>!p.state.connected)||Environment.TickCount64-room.loadStarted>30000){
                            room.phase="lobby";foreach(var p in room.players){p.ready=false;p.loaded=false;p.peer?.Post(new{type="error",code="LOAD_CANCELLED"});}BroadcastLobby(room);
                        }else if(room.players.All(p=>p.loaded)){room.phase="countdown";room.countdown=3;}
                    }
                    else if(room.phase=="countdown"){room.countdown-=.05f;if(room.countdown<=0)room.phase="race";}
                    else if(room.phase=="race"){
                        room.time+=.05f;
                        foreach(var p in room.players)Simulation.StepCar(p.state,p.input,room.track,.05f);
                        room.session.Resolve(room.players.Select(p=>p.state).ToList(),.05f);
                        Simulation.Rank(room.players.Select(p=>p.state).ToList(),room.track);
                        if(room.firstFinish==0 && room.players.Any(p=>p.state.finished))room.firstFinish=room.time;
                        bool deadline=room.time>900||(room.firstFinish>0&&room.time-room.firstFinish>90);
                        if(deadline)foreach(var p in room.players.Where(p=>!p.state.finished))p.state.dnf=true;
                        if(room.players.All(p=>p.state.finished||p.state.dnf)){
                            room.phase="finished";Simulation.Rank(room.players.Select(p=>p.state).ToList(),room.track);
                            if(!room.recorded){room.recorded=true;saves.Add(room.players.Select(p=>new RaceResult(room.raceId,p.state.id,p.state.name,room.track.id+"-2lap",p.state.rank,p.state.finishTime,p.state.bestLap,p.state.dnf,DateTime.UtcNow)).ToArray());}
                            BroadcastLobby(room);
                        }
                    }
                    if(room.phase=="lobby"||room.phase=="finished"){if(++room.lobbyTicks%20==0)BroadcastLobby(room);}
                    foreach(var p in room.players)p.peer?.Post(new{type="state",track=room.track.id,phase=room.phase,raceId=room.raceId,time=room.session.time,coins=room.session.coins,countdown=room.countdown,self=p.state.id,cars=room.players.Select(x=>x.state).ToArray()});
                }
            }
            foreach(var result in saves)_=Persist(result,stoppingToken);
        }
    }
    async Task Persist(RaceResult[] results,CancellationToken ct)
    {
        try{await store.Save(results,ct);}catch(Exception ex){logger.LogWarning(ex,"Result persistence failed");lock(gate){foreach(var room in rooms.Values.Where(r=>r.raceId==results[0].RaceId))foreach(var p in room.players)p.peer?.Post(new{type="error",code="SAVE_FAILED"});}}
    }
    sealed class Room
    {
        public string code,owner="",phase="lobby",raceId="";
        public RaceSession session;public long loadStarted;public Track track;public List<Player> players=new();public float countdown,time,firstFinish;public bool recorded;public int lobbyTicks;
        public Room(string c,string t){code=c;track=new Track(t);session=new RaceSession(track);}
    }
    sealed class Player
    {
        public string token="";public Peer? peer;public CarState state=new();public DriveInput input=new();
        public bool ready,loaded;public long disconnectedAt,lastInput;public float lastRecovery=-10;
    }
    sealed class Peer
    {
        readonly WebSocket socket;readonly Channel<string> output=Channel.CreateBounded<string>(new BoundedChannelOptions(8){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
        public Peer(WebSocket s){socket=s;}
        public void Post(object data)=>output.Writer.TryWrite(JsonSerializer.Serialize(data,Json));
        public void Close()=>output.Writer.TryComplete();
        public async Task SendLoop(CancellationToken ct){
            await foreach(var json in output.Reader.ReadAllAsync(ct)){
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(3000);
                await socket.SendAsync(Encoding.UTF8.GetBytes(json),WebSocketMessageType.Text,true,timeout.Token);
            }
        }
    }
}
