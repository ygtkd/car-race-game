#nullable enable
﻿using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoastRacer.Core;
var url=args.Length>0?args[0]:"ws://127.0.0.1:5099/ws";
void Check(bool v,string name){if(!v)throw new Exception(name);Console.WriteLine("PASS "+name);}
await using var a=new Peer();await using var b=new Peer();await a.Open(url);await a.Send(new{action="create",track="shonan",name="Online test A",vehicle="apex"});await Wait(()=>a.id!="");await b.Open(url);await b.Send(new{action="join",code=a.code,name="Online test B",vehicle="swift"});await Wait(()=>b.id!="");await a.Send(new{action="configure",track="shonan",bots=1,difficulty=3,night=true});await Task.Delay(100);await a.Send(new{action="ready",ready=true});await b.Send(new{action="ready",ready=true});await Wait(()=>a.state?.phase=="loading"&&b.state?.phase=="loading");string race=a.state!.raceId;await a.Send(new{action="loaded",raceId=race});await b.Send(new{action="loaded",raceId=race});await Wait(()=>a.state?.phase=="race");
var track=new Track("shonan");bool observedLast=false;float firstTime=0,firstDistance=0;Point firstPosition=new();int tick=0;var end=DateTime.UtcNow.AddMinutes(7);
while(DateTime.UtcNow<end&&b.state?.phase!="finished"){
 var sa=a.state;var sb=b.state;if(sa?.cars==null||sb?.cars==null){await Task.Delay(50);continue;}var ca=sa.cars.First(c=>c.id==a.id);var cb=sb.cars.First(c=>c.id==b.id);
 if(ca.finished&&!cb.finished&&!observedLast){observedLast=true;firstTime=ca.finishTime;firstDistance=ca.distance;firstPosition=ca.Position;Check(sa.phase=="race"&&!cb.dnf,"Last human remains in race after first human finishes");}
 var ia=ca.finished?new DriveInput{throttle=1,steer=1}:Simulation.Bot(ca,track,.91f);var ib=cb.elapsed<16?new DriveInput{brake=1}:Simulation.Bot(cb,track,.78f);
 await a.Input(ia);await b.Input(ib);if(++tick%600==0)Console.WriteLine($"PROGRESS A lap {ca.lap} {ca.elapsed:0.0}s; B lap {cb.lap} {cb.elapsed:0.0}s");await Task.Delay(50);
}
var final=b.state??throw new Exception("No final state");Check(final.phase=="finished","Room finishes after last human actually crosses finish");var humanA=final.cars.First(c=>c.id==a.id);var humanB=final.cars.First(c=>c.id==b.id);Check(observedLast&&humanA.finished&&humanB.finished&&!humanA.estimated&&!humanB.estimated&&!humanB.dnf&&humanB.finishTime>humanA.finishTime,"Both human results use real finish times");Check(humanA.finishTime==firstTime&&humanA.distance==firstDistance&&(humanA.Position-firstPosition).Length>10,"Finished player ignores malicious steering and cruises without extra records");Check(final.cars.All(c=>c.finished||c.dnf),"Remaining CPU settles");
File.WriteAllText("Logs/revision7-online-finish.json",JsonSerializer.Serialize(final,Peer.Json));await a.Send(new{action="rematch"});await Task.Delay(150);await a.Send(new{action="ready",ready=true});await b.Send(new{action="ready",ready=true});await Wait(()=>a.state?.phase=="loading"&&a.state.raceId!=race);Check(a.state!.cars.Select(c=>c.gridSlot).Distinct().Count()==a.state.cars.Length,"Rematch assigns a fresh valid grid");
static async Task Wait(Func<bool> ready){for(int i=0;i<400;i++){if(ready())return;await Task.Delay(50);}throw new Exception("Timed out waiting for protocol state");}
sealed class Snapshot {public string phase="",raceId="";public CarState[] cars=Array.Empty<CarState>();}
sealed class Peer:IAsyncDisposable {
 public static readonly JsonSerializerOptions Json=new(){IncludeFields=true,PropertyNameCaseInsensitive=true,WriteIndented=true};readonly ClientWebSocket socket=new();readonly CancellationTokenSource stop=new();Task? reader;public string id="",code="";public volatile Snapshot? state;
 public async Task Open(string url){await socket.ConnectAsync(new Uri(url),stop.Token);reader=Read();}
 public async Task Send(object data){byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(data,Json);await socket.SendAsync(bytes,WebSocketMessageType.Text,true,stop.Token);}
 public Task Input(DriveInput value)=>Send(new{action="input",value.throttle,value.brake,value.steer,value.assist,value.sensitivity});
 async Task Read(){byte[] bytes=new byte[65536];try{while(!stop.IsCancellationRequested){var r=await socket.ReceiveAsync(bytes,stop.Token);if(r.MessageType==WebSocketMessageType.Close)break;if(!r.EndOfMessage)throw new Exception("Unexpected oversized message");using var doc=JsonDocument.Parse(bytes.AsMemory(0,r.Count));var root=doc.RootElement;string? type=root.GetProperty("type").GetString();if(type=="joined"){id=root.GetProperty("id").GetString()!;code=root.GetProperty("code").GetString()!;}else if(type=="state")state=JsonSerializer.Deserialize<Snapshot>(bytes.AsSpan(0,r.Count),Json);else if(type=="error")Console.WriteLine("SERVER "+root.GetProperty("code").GetString());}}catch(OperationCanceledException){}catch(WebSocketException){}}
 public async ValueTask DisposeAsync(){stop.Cancel();socket.Abort();if(reader!=null)try{await reader;}catch{}socket.Dispose();stop.Dispose();}
}
