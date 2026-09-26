using System.Text.Json;
using CoastRacer.Core;
void Check(bool v,string message){if(!v)throw new Exception(message);Console.WriteLine("PASS "+message);}
var maps=new[]{"ridge","suzuka","shonan"}.Select(id=>{var t=new Track(id);return new{id,points=t.points,bridges=Enumerable.Range(0,Track.Samples).Select(i=>t.ForeignRoadBelow(i,22,4)).ToArray()};});
File.WriteAllText("Art/Blender/tracks.json",JsonSerializer.Serialize(maps,new JsonSerializerOptions{IncludeFields=true}));
Check(Vehicles.All.Length==10,"Ten vehicles");Check(BadgeDesign.Items.Length==30,"Thirty rewards");
var t=new Track("ridge");var last=Simulation.Spawn(t);var winner=Simulation.Spawn(t);winner.finished=true;winner.finishTime=10;Check(!Simulation.SettleOnline(new[]{winner,last},t)&&!last.dnf,"Final human is allowed to finish");last.bot=true;Check(Simulation.SettleOnline(new[]{winner,last},t)&&last.estimated&&!last.dnf,"Last CPU receives estimated time");
float finish=winner.finishTime,dist=winner.distance;var pos=winner.Position;for(int i=0;i<100;i++)Simulation.StepCar(winner,new DriveInput{throttle=1,steer=1},t,.02f);Check((winner.Position-pos).Length>5&&winner.finishTime==finish&&winner.distance==dist,"Post-finish autopilot moves without modifying result");
foreach(float range in new[]{29.9f,30f,30.1f}){var a=Simulation.Spawn(t);a.vehicle="atlas";a.gauge=1;var b=Simulation.Spawn(t);b.x=a.x+range;b.z=a.z;b.speed=30;new RaceSession(t).Activate(a,new[]{a,b});Check(range<=30?b.jamTime==5&&b.jamImmunity==12&&a.disruptions==1:b.jamTime==0,"Pulse boundary "+range);}
var rng=new Random(17);var sequences=new HashSet<string>();for(int i=0;i<20;i++){var grid=Simulation.Grid(8,rng);Check(grid.Distinct().Count()==8,"Unique grid "+i);sequences.Add(string.Join(',',grid));}Check(sequences.Count>1,"Grid randomizes");
var reports=new List<object>();
for(int race=0;race<10;race++){
 var tr=new Track(new[]{"ridge","suzuka","shonan"}[race%3]);var session=new RaceSession(tr);int level=race%5+1;var slots=Simulation.Grid(8,new Random(race+70));var cars=Enumerable.Range(0,8).Select(i=>{var c=Simulation.Spawn(tr,slots[i]);c.id="test"+i;c.bot=true;c.cpuLevel=level;c.night=race%2==1;c.vehicle=Vehicles.All[(race+i)%10].id;return c;}).ToArray();var recovery=new int[8];
 for(int step=0;step<90000&&cars.Any(c=>!c.finished);step++){
  for(int i=0;i<8;i++){var c=cars[i];bool before=c.recoveryProtection>0;if(c.gauge>=1&&c.specialTime<=0)session.Activate(c,cars);Simulation.StepCar(c,session.Bot(c,Simulation.Difficulty(level)),tr,.02f);if(!before&&c.recoveryProtection>0)recovery[i]++;if(!float.IsFinite(c.x)||!float.IsFinite(c.speed))throw new Exception("Nonfinite state");}session.Resolve(cars,.02f);
 }
 Simulation.Rank(cars,tr);Check(cars.All(c=>c.finished&&c.lap==2),"Race "+race+" "+tr.id+" Lv"+level+" all finish");Check(cars.Select(c=>c.rank).Distinct().Count()==8,"Unique ranks "+race);reports.Add(new{race=race+1,track=tr.id,night=race%2==1,level,cars=cars.Select((c,i)=>new{c.vehicle,c.gridSlot,c.finishTime,c.rank,c.specialUses,c.disruptions,recoveries=recovery[i]}).ToArray()});
}
var times=new List<object>();foreach(string id in new[]{"ridge","suzuka","shonan"}){float previous=float.MaxValue;for(int level=1;level<=5;level++){var tr=new Track(id);var c=Simulation.Spawn(tr);for(int i=0;i<60000&&!c.finished;i++)Simulation.StepCar(c,Simulation.Bot(c,tr,Simulation.Difficulty(level)),tr,.02f);Check(c.finished&&c.finishTime<previous,"Difficulty ordering "+id+" "+level);previous=c.finishTime;times.Add(new{id,level,time=c.finishTime});}}
File.WriteAllText("Logs/revision7-races.json",JsonSerializer.Serialize(new{races=reports,difficulty=times},new JsonSerializerOptions{WriteIndented=true}));
// Immunity must outlive the visible ability and must prevent repeat damage.
var immune=Simulation.Spawn(t);immune.vehicle="vortex";immune.gauge=1;var attacker=Simulation.Spawn(t);attacker.vehicle="stormbike";attacker.x=immune.x+5;var effects=new RaceSession(t);effects.Activate(immune,new[]{immune,attacker});
for(int i=0;i<251;i++)Simulation.StepCar(immune,new DriveInput{brake=1},t,.02f);
Check(immune.specialTime==0&&immune.jamImmunity>6,"Immunity continues after five-second ability");attacker.gauge=1;attacker.x=immune.x+2;attacker.z=immune.z;effects.Activate(attacker,new[]{attacker,immune});Check(immune.jamTime==0&&attacker.disruptions==0,"Immune hit is neither damage nor a mission count");
for(int i=0;i<350;i++)Simulation.StepCar(immune,new DriveInput{brake=1},t,.02f);Check(immune.jamImmunity==0,"Immunity expires after twelve seconds");
var low=Simulation.Spawn(t);var high=Simulation.Spawn(t);low.vehicle="atlas";low.gauge=1;high.y+=4;effects.Activate(low,new[]{low,high});Check(high.jamTime==0,"Bridge elevation blocks pulse");
var light=Simulation.Spawn(t);var heavy=Simulation.Spawn(t);light.vehicle="bicycle";heavy.vehicle="atlas";heavy.x=light.x+1.5f;float lx=light.x,hx=heavy.x;RaceSession.Collisions(new[]{light,heavy});Check(Math.Abs(light.x-lx)>Math.Abs(heavy.x-hx)*3,"Heavy vehicle resists lateral displacement");
var coast=new Track("shonan");var shared=new RaceSession(coast);var ca=Simulation.Spawn(coast);var cb=Simulation.Spawn(coast);ca.id="a";cb.id="b";var coin=shared.coins[0];ca.x=cb.x=coin.x;ca.y=cb.y=coin.y;ca.z=cb.z=coin.z;ca.recoveryProtection=cb.recoveryProtection=1;shared.Resolve(new[]{ca,cb},.02f);Check(ca.coins+cb.coins==1&&!coin.active,"Shonan shared pickup has a single winner");ca.x+=50;cb.x+=50;shared.Resolve(new[]{ca,cb},1.99f);Check(!coin.active,"Coin remains absent before two seconds");shared.Resolve(new[]{ca,cb},.01f);Check(coin.active,"Shared coin respawns at two seconds");
Check(Vehicles.Badge("winner;finish;collector;veteran")=="winner;finish;collector","Equipment limited to three known items");
