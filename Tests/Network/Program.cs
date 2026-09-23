using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoastRacer.Core;
var options=new JsonSerializerOptions{IncludeFields=true};
string address=args.FirstOrDefault()??"ws://127.0.0.1:5081/ws";
foreach(string trackId in new[]{"ridge","suzuka"}){
 using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(14));
 var ct=timeout.Token;var track=new Track(trackId);
 using var a=new ClientWebSocket();using var b=new ClientWebSocket();
 await a.ConnectAsync(new Uri(address),ct);await b.ConnectAsync(new Uri(address),ct);
 async Task Send(ClientWebSocket ws,object data)=>await ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data,options)),WebSocketMessageType.Text,true,ct);
 async Task<JsonDocument> Read(ClientWebSocket ws){
  using var bytes=new MemoryStream();var buffer=new byte[32768];WebSocketReceiveResult r;
  do{r=await ws.ReceiveAsync(buffer,ct);if(r.MessageType==WebSocketMessageType.Close)throw new Exception("unexpected close");bytes.Write(buffer,0,r.Count);}while(!r.EndOfMessage);
  return JsonDocument.Parse(bytes.ToArray());
 }
 async Task<JsonDocument> Until(ClientWebSocket ws,string type){
  while(true){var d=await Read(ws);if(d.RootElement.GetProperty("type").GetString()==type)return d;d.Dispose();}
 }
 await Send(a,new{action="create",track=trackId,name="検証ドライバー甲",vehicle="kebab"});
 using var ja=await Until(a,"joined");string code=ja.RootElement.GetProperty("code").GetString();string aid=ja.RootElement.GetProperty("id").GetString();
 await Send(b,new{action="join",code,name="検証ドライバー乙",vehicle="bicycle"});
 using var jb=await Until(b,"joined");string bid=jb.RootElement.GetProperty("id").GetString();
 await Send(a,new{action="ready",ready=true});await Send(b,new{action="ready",ready=true});
 await Task.Delay(150,ct);await Send(a,new{action="start"});
 Console.WriteLine("START "+trackId+" room "+code);
 async Task<CarState[]> Drive(ClientWebSocket ws,string id){
  int lap=-1;float report=0;string loadedRace="";var session=new RaceSession(track);
  while(true){
   using var d=await Read(ws);var root=d.RootElement;string type=root.GetProperty("type").GetString();
   if(type=="error")throw new Exception(root.GetRawText());
   if(type!="state")continue;
   string phase=root.GetProperty("phase").GetString();
   var cars=JsonSerializer.Deserialize<CarState[]>(root.GetProperty("cars").GetRawText(),options);
   var me=cars.Single(c=>c.id==id);
   if(phase=="loading") {string race=root.GetProperty("raceId").GetString();if(race!=loadedRace){loadedRace=race;await Send(ws,new{action="loaded",raceId=race});}continue;}
   var coins=JsonSerializer.Deserialize<CoinState[]>(root.GetProperty("coins").GetRawText(),options);for(int i=0;i<3;i++)session.coins[i]=coins[i];
   if(me.lap!=lap||me.elapsed-report>30){lap=me.lap;report=me.elapsed;Console.WriteLine(trackId+" "+id[..4]+" lap="+lap+" gate="+me.gate+" t="+me.elapsed.ToString("F1"));}
   if(phase=="finished")return cars;
   if(phase=="race"){
    var input=session.Bot(me,.92f);int uses=me.specialUses;session.UseBotSpecial(me,cars);if(me.specialUses>uses)await Send(ws,new{action="special"});
    await Send(ws,new{action="input",input.steer,input.throttle,input.brake,input.assist,input.sensitivity});
   }
  }
 }
 var results=await Task.WhenAll(Drive(a,aid),Drive(b,bid));
 foreach(var result in results){
  if(result.Any(c=>!c.finished||c.dnf||c.lap!=Simulation.Laps))throw new Exception("Race incomplete "+JsonSerializer.Serialize(result,options));
 }
 var one=results[0].OrderBy(c=>c.rank).Select(c=>(c.id,c.rank,c.finishTime)).ToArray();
 var two=results[1].OrderBy(c=>c.rank).Select(c=>(c.id,c.rank,c.finishTime)).ToArray();
 if(!one.SequenceEqual(two))throw new Exception("Clients disagree on result");
 Console.WriteLine("PASS "+trackId+" two clients complete race and share identical standings");
 await Send(a,new{action="ready",ready=true});await Send(b,new{action="ready",ready=true});
 await Task.Delay(100,ct);await Send(a,new{action="start"});
 async Task<JsonDocument> Phase(ClientWebSocket socket,string expected){while(true){var doc=await Until(socket,"state");if(doc.RootElement.GetProperty("phase").GetString()==expected)return doc;doc.Dispose();}}
 var loading=await Task.WhenAll(Phase(a,"loading"),Phase(b,"loading"));
 for(int i=0;i<2;i++){await Send(i==0?a:b,new{action="loaded",raceId=loading[i].RootElement.GetProperty("raceId").GetString()});loading[i].Dispose();}
 using var restarted=await Phase(a,"countdown");
 Console.WriteLine("PASS "+trackId+" rematch");
 a.Abort();
 b.Abort();
 await Task.Delay(21000,ct);
}
Console.WriteLine("PASS all full online race checks");
