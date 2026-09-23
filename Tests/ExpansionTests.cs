using CoastRacer.Core;
using System.Text.Json;
static class ExpansionTests
{
 static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run()
 {
  var t=new Track("ridge");var world=new RaceSession(t);var coin=world.coins[0];var a=Simulation.Spawn(t);a.id="a";a.x=coin.x;a.y=coin.y;a.z=coin.z;
  var b=Simulation.Spawn(t);b.id="b";b.x=coin.x;b.y=coin.y;b.z=coin.z;var pair=new List<CarState>{a,b};
  world.Resolve(pair,.02f);Check(a.coins+b.coins==1&&!coin.active,"shared coin awards exactly one winner");
  var winner=a.coins==1?a:b;Check(Math.Abs(winner.gauge-.5f/1.2f)<.0001f,"coin adds 0.5 laps / 1.2 = 41.67 percent");
  a.x=b.x=100000;for(int i=0;i<99;i++)world.Resolve(pair,.02f);Check(!coin.active,"coin unavailable before two seconds");world.Resolve(pair,.021f);Check(coin.active,"coin respawns after two seconds");
  a.gauge=1;a.vehicle="atlas";a.x=b.x=0;a.z=0;b.z=5;b.speed=30;b.jamImmunity=0;Check(world.Activate(a,pair)&&a.gauge==0&&b.speed<30,"pulse is server-applicable and consumes full gauge");Check(!world.Activate(a,pair),"special cannot be activated without charge");
  b.vehicle="vortex";b.specialTime=3;b.jamImmunity=0;b.speed=30;a.gauge=1;a.specialTime=0;world.Activate(a,pair);Check(b.speed==30,"shield resists pulse");
  a=Simulation.Spawn(t);b=Simulation.Spawn(t);a.vehicle="swift";b.vehicle="atlas";a.x=0;b.x=1;a.z=b.z=0;a.y=b.y=0;a.yaw=b.yaw=0;RaceSession.Collisions(new[]{a,b});Check(Math.Abs(a.x)>Math.Abs(b.x-1),"lighter car moves farther in contact");
  a=Simulation.Spawn(t);a.furthestDistance=400;a.gauge=.3f;a.gate=3;Simulation.Recover(a,t);for(int i=0;i<50;i++)Simulation.StepCar(a,new DriveInput(),t,.02f);Check(a.gauge<=.3001f,"recovery does not charge gauge");
  var report=new List<object>();
  foreach(string trackId in new[]{"ridge","suzuka"})foreach(bool specials in new[]{false,true})for(int rotation=0;rotation<Vehicles.All.Length;rotation++){
   var track=new Track(trackId);var session=new RaceSession(track);var cars=new List<CarState>();
   for(int slot=0;slot<4;slot++){var c=Simulation.Spawn(track,slot);c.vehicle=Vehicles.All[(slot+rotation)%Vehicles.All.Length].id;c.id=c.vehicle;cars.Add(c);}
   for(int step=0;step<45000&&cars.Any(c=>!c.finished);step++){
    foreach(var car in cars){if(specials)session.UseBotSpecial(car,cars);Simulation.StepCar(car,session.Bot(car,.92f),track,.02f);}
    session.Resolve(cars,.02f);
   }
   Simulation.Rank(cars,track);foreach(var car in cars){Check(car.finished&&float.IsFinite(car.x)&&float.IsFinite(car.speed),trackId+" "+car.vehicle+" finite physics");report.Add(new{track=trackId,specials,rotation,car.vehicle,car.finished,car.finishTime,car.rank,car.coins,car.specialUses,car.distance});}
  }
  Directory.CreateDirectory("Logs");File.WriteAllText("Logs/vehicle-balance.json",JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
  Console.WriteLine("Balance report: "+report.Count+" car-race samples, 32 races, both tracks and all starting positions");
 }
}