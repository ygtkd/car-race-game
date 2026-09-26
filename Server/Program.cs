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
    if(acceptsBr && (HttpMethods.IsGet(context.Request.Method)||HttpMethods.IsHead(context.Request.Method)) && (requestPath.StartsWith("/play/Build/",StringComparison.Ordinal)||System.Text.RegularExpressions.Regex.IsMatch(requestPath,@"^/play/releases/[a-f0-9]{12}/Build/[^/]+$"))){
        string file=Path.GetFileName(requestPath);
        if(provider.TryGetContentType(file,out var contentType)){
            string compressed=requestPath.StartsWith("/play/Build/",StringComparison.Ordinal)?Path.Combine(staticRoot,"play","Build",file+".br"):Path.Combine(staticRoot,requestPath.TrimStart('/').Replace('/',Path.DirectorySeparatorChar)+".br");
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
app.UseStaticFiles(new StaticFileOptions{ContentTypeProvider=provider,OnPrepareResponse=context=>{if(new[]{".html",".css",".js"}.Contains(Path.GetExtension(context.File.Name)))context.Context.Response.Headers.CacheControl="no-cache";if(context.File.Name=="release.json")context.Context.Response.Headers.CacheControl="no-store";}});
app.MapGet("/api/rooms",(RaceHub hub)=>hub.ListRooms());
app.MapGet("/health",()=>Results.Ok(new{status="ok",protocol=3}));
app.MapGet("/api/courses",()=>new[]{new{id="ridge",name="山岳サーキット"},new{id="suzuka",name="鈴鹿サーキット"},new{id="shonan",name="湘南海岸"}});
app.MapGet("/api/records/{track}",async(string track,ResultStore store,CancellationToken ct)=>{
    if(track!="ridge"&&track!="suzuka"&&track!="shonan")return Results.BadRequest();
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
    public RaceHub(ResultStore s,IConfiguration config,ILogger<RaceHub> log){store=s;logger=log;limit=Math.Clamp(config.GetValue("Racer:MaxPlayers",8),2,32);}
    public object ListRooms(){lock(gate)return rooms.Values.Where(r=>r.phase=="lobby"&&r.players.Count+r.botCount<8).Select(r=>new{code=r.code,track=r.track.id,night=r.night,difficulty=r.difficulty,host=r.players.FirstOrDefault(p=>p.state.id==r.owner)?.state.name??"",players=r.players.Count,bots=r.botCount,capacity=8,phase=r.phase}).ToArray();}
    public bool TryConnect(){lock(gate){if(connections>=limit)return false;connections++;return true;}}
    public void DisconnectSlot(){lock(gate)connections--;}
    public async Task Handle(WebSocket socket,CancellationToken ct)
    {
        var peer=new Peer(socket);var sender=peer.SendLoop(ct);Player? player=null;Room? room=null;
        bool voluntaryLeave=false;var buffer=new byte[4096];int burst=0;long rateEpoch=Environment.TickCount64;
        try{
            while(socket.State==WebSocketState.Open&&!ct.IsCancellationRequested){
                using var receiveTimeout=CancellationTokenSource.CreateLinkedTokenSource(ct);receiveTimeout.CancelAfter(player==null?TimeSpan.FromSeconds(15):TimeSpan.FromSeconds(30));
                var result=await socket.ReceiveAsync(buffer,receiveTimeout.Token);
                if(result.MessageType==WebSocketMessageType.Close){voluntaryLeave=result.CloseStatus==WebSocketCloseStatus.NormalClosure;break;}
                if(!result.EndOfMessage||result.MessageType!=WebSocketMessageType.Text){peer.Post(new{type="error",code="INVALID_MESSAGE"});break;}
                if(Environment.TickCount64-rateEpoch>1000){burst=0;rateEpoch=Environment.TickCount64;}
                if(++burst>60)break;
                JsonDocument doc;
                try{doc=JsonDocument.Parse(buffer.AsMemory(0,result.Count));}catch{peer.Post(new{type="error",code="INVALID_MESSAGE"});break;}
                using(doc){
                    var root=doc.RootElement;if(root.ValueKind!=JsonValueKind.Object){peer.Post(new{type="error",code="INVALID_MESSAGE"});break;}string action=GetString(root,"action");
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
                                    room=new Room(code,GetString(root,"track"));rooms.Add(code,room);
                                }else rooms.TryGetValue(GetString(root,"code").ToUpperInvariant(),out room);
                                if(room==null){peer.Post(new{type="error",code="ROOM_NOT_FOUND"});continue;}
                                if(room.phase!="lobby"){peer.Post(new{type="error",code="RACE_RUNNING"});room=null;continue;}
                                if(room.players.Count+room.botCount>=8){peer.Post(new{type="error",code="ROOM_FULL"});room=null;continue;}
                                string name=GetString(root,"name").Trim();if(name.Length==0)name="ドライバー";if(name.Length>16)name=name[..16];
                                name=new string(name.Where(c=>!char.IsControl(c)).ToArray());
                                player=new Player{token=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24)),peer=peer,state=Simulation.Spawn(room.track,room.players.Count)};
                                player.state.id=Guid.NewGuid().ToString("N");player.state.name=name;player.state.vehicle=Vehicles.Get(GetString(root,"vehicle")).id;player.state.badge=Vehicles.Badge(GetString(root,"badge"));room.players.Add(player);foreach(var member in room.players)member.ready=false;
                                if(room.owner=="")room.owner=player.state.id;
                            }else{peer.Post(new{type="error",code="JOIN_FIRST"});continue;}
                            peer.Post(new{type="joined",code=room!.code,token=player.token,id=player.state.id,maxPlayers=8});
                            BroadcastLobby(room);continue;
                        }
                        if(room==null)continue;
                        if(action=="profile"&&room.phase=="lobby"){
                            string name=GetString(root,"name");if(name.Length>0)player.state.name=new string(name.Where(c=>!char.IsControl(c)).Take(16).ToArray());
                            player.state.badge=Vehicles.Badge(GetString(root,"badge"));
                            if(room.phase=="lobby"||room.phase=="finished"){player.state.vehicle=Vehicles.Get(GetString(root,"vehicle")).id;player.ready=false;}
                            BroadcastLobby(room);
                        }
                        else if(action=="loaded"&&room.phase=="loading"&&GetString(root,"raceId")==room.raceId){player.loaded=true;}
                        else if(action=="special"&&room.phase=="race"){room.session.Activate(player.state,room.players.Select(x=>x.state).ToList());}
                        else if(action=="ready" && room.phase=="lobby"){player.ready=root.TryGetProperty("ready",out var r)&&r.ValueKind==JsonValueKind.True;TryStart(room);BroadcastLobby(room);}
                        else if(action=="rematch"&&room.phase=="finished"){room.players.RemoveAll(p=>p.state.bot||!p.state.connected);room.phase="lobby";foreach(var p in room.players){p.ready=false;p.state.dnf=false;}BroadcastLobby(room);}
                        else if(action=="configure"&&player.state.id==room.owner&&room.phase=="lobby"){
                            if(!root.TryGetProperty("bots",out var bv)||bv.ValueKind!=JsonValueKind.Number||!bv.TryGetInt32(out int bots)||bots<0||bots+room.players.Count>8){peer.Post(new{type="error",code="ROOM_FULL"});continue;}
                            room.night=root.TryGetProperty("night",out var night)&&night.ValueKind==JsonValueKind.True;room.difficulty=root.TryGetProperty("difficulty",out var difficulty)&&difficulty.ValueKind==JsonValueKind.Number&&difficulty.TryGetInt32(out int level)?Math.Clamp(level,1,5):3;room.botCount=bots;room.track=new Track(GetString(root,"track"));foreach(var p in room.players)p.ready=false;BroadcastLobby(room);
                        }
                        else if(action=="track" && player.state.id==room.owner && room.phase=="lobby"){
                            room.track=new Track(GetString(root,"track"));foreach(var p in room.players)p.ready=false;BroadcastLobby(room);
                        }
                        else if(action=="start" && player.state.id==room.owner && (room.phase=="lobby"||room.phase=="finished")){
                            TryStart(room);
                        }
                        else if(action=="input"&&!player.state.finished&&!player.state.dnf){
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
            lock(gate)if(player!=null&&player.peer==peer){player.state.connected=false;player.disconnectedAt=Environment.TickCount64;if(voluntaryLeave&&!player.state.finished)player.state.dnf=true;player.input=new DriveInput{brake=1};player.peer=null;if(room!=null){if(room.phase=="lobby"){if(voluntaryLeave)room.players.Remove(player);foreach(var member in room.players)member.ready=false;room.owner=room.players.FirstOrDefault(p=>!p.state.bot&&p.state.connected)?.state.id??"";}BroadcastLobby(room);}}
            if(socket.State==WebSocketState.CloseReceived){try{using var closeTimeout=new CancellationTokenSource(2000);await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure,"Closed",closeTimeout.Token);}catch{}}
            peer.Close();socket.Abort();try{await sender;}catch{}
        }
    }
    static string GetString(JsonElement root,string key)=>root.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?(v.GetString()??""):"";
    void TryStart(Room room){
        if(room.phase!="lobby"||room.players.Count+room.botCount<1||room.players.Any(p=>!p.ready||!p.state.connected))return;
        room.raceId=Guid.NewGuid().ToString("N");room.phase="loading";room.loadStarted=Environment.TickCount64;room.session=new RaceSession(room.track);room.countdown=3;room.firstFinish=0;room.time=0;room.recorded=false;
        for(int i=0;i<room.botCount;i++)room.players.Add(new Player{loaded=true,state=new CarState{id="bot"+i,name="GT "+(i+1),vehicle=Vehicles.All[Random.Shared.Next(Vehicles.All.Length)].id,bot=true}});
        var slots=Simulation.Grid(room.players.Count,Random.Shared);
        for(int i=0;i<room.players.Count;i++){var p=room.players[i];var old=p.state;p.state=Simulation.Spawn(room.track,slots[i]);p.state.cpuLevel=room.difficulty;p.state.night=room.night;p.state.id=old.id;p.state.name=old.name;p.state.vehicle=old.vehicle;p.state.badge=old.badge;p.state.bot=old.bot;p.input=new DriveInput();p.recorded=false;p.ready=false;p.loaded=p.state.bot;}
    }
    void BroadcastLobby(Room r)
    {
        foreach(var p in r.players)p.peer?.Post(new{type="lobby",code=r.code,track=r.track.id,owner=r.owner,phase=r.phase,bots=r.botCount,night=r.night,difficulty=r.difficulty,players=r.players.Where(x=>!x.state.bot).Select(x=>new{id=x.state.id,name=x.state.name,vehicle=x.state.vehicle,badge=x.state.badge,ready=x.ready,loaded=x.loaded,connected=x.state.connected}).ToArray()});
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while(await timer.WaitForNextTickAsync(stoppingToken)){
            var saves=new List<RaceResult[]>();
            lock(gate){
                foreach(var room in rooms.Values.ToArray()){
                    if(room.players.Where(p=>!p.state.bot).All(p=>!p.state.connected)&&room.players.Where(p=>!p.state.bot).All(p=>Environment.TickCount64-p.disconnectedAt>20000)){rooms.Remove(room.code);continue;}
                    foreach(var p in room.players.Where(p=>!p.state.bot)){
                        if(!p.state.finished&&!p.state.connected&&Environment.TickCount64-p.disconnectedAt>20000)p.state.dnf=true;
                        if(Environment.TickCount64-p.lastInput>750)p.input=new DriveInput{brake=1};
                    }
                    if(room.phase=="lobby"&&room.players.RemoveAll(p=>p.state.dnf)>0){foreach(var member in room.players)member.ready=false;BroadcastLobby(room);}
                    if(!room.players.Any(p=>!p.state.bot&&p.state.id==room.owner&&p.state.connected)){
                        room.owner=room.players.FirstOrDefault(p=>!p.state.bot&&p.state.connected)?.state.id??"";
                    }
                    if(room.phase=="loading"){
                        if(room.players.Any(p=>!p.state.connected)||Environment.TickCount64-room.loadStarted>30000){
                            room.phase="lobby";room.players.RemoveAll(p=>p.state.bot||p.state.dnf);foreach(var p in room.players){p.ready=false;p.loaded=false;p.peer?.Post(new{type="error",code="LOAD_CANCELLED"});}BroadcastLobby(room);
                        }else if(room.players.All(p=>p.loaded)){room.phase="countdown";room.countdown=3;}
                    }
                    else if(room.phase=="countdown"){room.countdown-=.05f;if(room.countdown<=0)room.phase="race";}
                    else if(room.phase=="race"){
                        room.time+=.05f;
                        var states=room.players.Select(p=>p.state).ToList();
                        foreach(var p in room.players){if(p.state.bot)room.session.UseBotSpecial(p.state,states);Simulation.StepCar(p.state,p.state.bot?room.session.Bot(p.state,Simulation.Difficulty(room.difficulty)):p.input,room.track,.05f);}
                        room.session.Resolve(room.players.Select(p=>p.state).ToList(),.05f);
                        Simulation.Rank(room.players.Select(p=>p.state).ToList(),room.track);
                        if(room.firstFinish==0 && room.players.Any(p=>p.state.finished))room.firstFinish=room.time;
                        foreach(var p in room.players)if(!p.recorded&&!p.state.bot&&p.state.finished&&!p.state.estimated&&!p.state.dnf){p.recorded=true;saves.Add(new[]{new RaceResult(room.raceId,p.state.id,p.state.name,room.track.id+"-2lap",p.state.rank,p.state.finishTime,p.state.bestLap,false,DateTime.UtcNow)});}


                        if(Simulation.SettleOnline(states,room.track)){
                            room.phase="finished";Simulation.Rank(room.players.Select(p=>p.state).ToList(),room.track);
                            if(!room.recorded){room.recorded=true;saves.Add(room.players.Where(p=>!p.recorded&&!p.state.bot&&!p.state.estimated&&p.state.finished&&!p.state.dnf).Select(p=>new RaceResult(room.raceId,p.state.id,p.state.name,room.track.id+"-2lap",p.state.rank,p.state.finishTime,p.state.bestLap,p.state.dnf,DateTime.UtcNow)).ToArray());}
                            BroadcastLobby(room);
                        }
                    }
                    if(room.phase=="finished")foreach(var p in room.players)Simulation.StepCar(p.state,new DriveInput(),room.track,.05f);
                    if(room.phase=="lobby"||room.phase=="finished"){if(++room.lobbyTicks%20==0)BroadcastLobby(room);}
                    foreach(var p in room.players)p.peer?.Post(new{type="state",track=room.track.id,night=room.night,difficulty=room.difficulty,phase=room.phase,raceId=room.raceId,time=room.session.time,coins=room.session.coins,countdown=room.countdown,self=p.state.id,cars=room.players.Select(x=>x.state).ToArray()});
                }
            }
            foreach(var result in saves)if(result.Length>0)_=Persist(result,stoppingToken);
        }
    }
    async Task Persist(RaceResult[] results,CancellationToken ct)
    {
        try{await store.Save(results,ct);}catch(Exception ex){logger.LogWarning(ex,"Result persistence failed");lock(gate){foreach(var room in rooms.Values.Where(r=>r.raceId==results[0].RaceId))foreach(var p in room.players)p.peer?.Post(new{type="error",code="SAVE_FAILED"});}}
    }
    sealed class Room
    {
        public string code,owner="",phase="lobby",raceId="";
        public RaceSession session;public long loadStarted;public Track track;public List<Player> players=new();public float countdown,time,firstFinish;public bool recorded;public int lobbyTicks,botCount,difficulty=3;public bool night;
        public Room(string c,string t){code=c;track=new Track(t);session=new RaceSession(track);}
    }
    sealed class Player
    {
        public string token="";public Peer? peer;public CarState state=new();public DriveInput input=new();
        public bool ready,loaded,recorded;public long disconnectedAt,lastInput;public float lastRecovery=-10;
    }
    sealed class Peer
    {
        readonly WebSocket socket;readonly Channel<string> output=Channel.CreateBounded<string>(new BoundedChannelOptions(8){FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true});
        public Peer(WebSocket s){socket=s;}
        public void Post(object data)=>output.Writer.TryWrite(JsonSerializer.Serialize(data,Json));
        public void Close()=>output.Writer.TryComplete();
        public async Task SendLoop(CancellationToken ct){
            try{await foreach(var json in output.Reader.ReadAllAsync(ct)){
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(3000);
                await socket.SendAsync(Encoding.UTF8.GetBytes(json),WebSocketMessageType.Text,true,timeout.Token);
            }}finally{socket.Abort();}
        }
    }
}
