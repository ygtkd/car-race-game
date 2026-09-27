using CoastRacer.Core;
using System.Text.Json;
var maps=new[]{"ridge","suzuka","shonan"}.Select(id=>{var t=new Track(id);Console.WriteLine(id+" length "+t.Length+" bridge "+t.distance[t.Nearest(new Point(9.5f,7,-90))]);return new{id,points=t.points,sections=t.sections.Select(x=>(int)x).ToArray(),bridges=Enumerable.Range(0,Track.Samples).Select(i=>t.ForeignRoadBelow(i,22,4)||t.IsSeaBridge(i)||t.IsElevated(i)).ToArray()};}).ToArray();
File.WriteAllText("Art/Blender/tracks.json",JsonSerializer.Serialize(maps,new JsonSerializerOptions{IncludeFields=true}));

void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
var track=new Track("shonan");Check(track.Length>=4000&&track.Length<=5000,"Length 4-5km");var entry=track.distance[track.Nearest(new Point(9.5f,7,-90))];Check(!track.IsHighway(0)&&!track.IsCave(0),"Start on ordinary road");float highwayLength=Enumerable.Range(0,640).Where(i=>track.IsHighway(i)).Sum(i=>track.distance[i+1]-track.distance[i]);Console.WriteLine("HIGHWAY_LENGTH "+highwayLength);Check(highwayLength>=900,"Highway at least 900m");
int highway=Enumerable.Range(0,640).First(i=>track.IsHighway(i)&&track.IsHighway((i+5)%640));int cave=Enumerable.Range(0,640).First(i=>track.IsCave(i));
Check(track.RecordKey=="shonan-v4","Record version");Check(track.SpeedFactor(track.points[highway],highway)==1.5f,"Highway factor");Check(track.SpeedFactor(track.points[highway]+new Point(0,-20,0),highway)==1,"Underpass not boosted");Check(track.SpeedFactor(track.points[cave],cave)==1,"Cave not boosted");
var h=track.points[highway];var tangent=track.Tangent(highway);Check(track.SpeedFactor(h+new Point(tangent.z*18,0,-tangent.x*18),highway)==1,"Runoff not boosted");
CarState At(int i,string id){var c=Simulation.Spawn(track);var p=track.points[i];c.vehicle=id;c.x=p.x;c.y=p.y;c.z=p.z;c.index=i;c.yaw=track.Yaw(i);return c;}
foreach(var spec in Vehicles.All){
 var c=At(highway,spec.id);c.speed=spec.maxSpeed*1.6f;Simulation.StepCar(c,new DriveInput{throttle=1,assist=0},track,.02f);Check(c.speed>spec.maxSpeed*1.49f,spec.id+" smooth high speed");
 var normal=At(0,spec.id);normal.speed=spec.maxSpeed*1.4f;Simulation.StepCar(normal,new DriveInput{throttle=1,assist=0},track,.02f);Check(normal.speed<spec.maxSpeed*1.4f&&normal.speed>spec.maxSpeed*1.3f,spec.id+" exit decelerates without clamp");
 c.safeIndex=cave;c.safePoint=track.points[cave];c.safeYaw=track.Yaw(cave);Simulation.Recover(c,track);Check(c.index==cave&&Math.Abs(c.y-track.points[cave].y)<.01f&&track.SpeedFactor(c.Position,c.index)==1,spec.id+" cave recovery");
}
// Isolate speed integration on a fixed straight, without the corner assist.
foreach(var spec in Vehicles.All){
 float Reach(int index){var c=At(index,spec.id);for(int k=0;k<4000;k++){var pos=track.points[index];c.x=pos.x;c.y=pos.y;c.z=pos.z;c.index=index;c.yaw=track.Yaw(index);c.steering=0;Simulation.StepCar(c,new DriveInput{throttle=1,assist=0},track,.02f);}return c.speed;}
 float ordinary=Reach(0),fast=Reach(highway);Check(fast>ordinary*1.4f&&fast<=spec.maxSpeed*1.501f,spec.id+" highway real speed gain");
}
var reports=new List<object>();
for(int race=0;race<10;race++){
 var t=new Track("shonan");var session=new RaceSession(t);int level=race%5+1;var slots=Simulation.Grid(8,new Random(100+race));var cars=Enumerable.Range(0,8).Select(i=>{var c=Simulation.Spawn(t,slots[i]);c.vehicle=Vehicles.All[(i+race)%10].id;c.id="car"+i;c.bot=true;c.cpuLevel=level;return c;}).ToArray();int recovery=0;
 for(int step=0;step<75000&&cars.Any(c=>!c.finished);step++){
  foreach(var c in cars){bool before=c.recoveryProtection>0;session.UseBotSpecial(c,cars);Simulation.StepCar(c,session.TrafficBot(c,cars,Simulation.Difficulty(level)),t,.02f);if(!before&&c.recoveryProtection>0)recovery++;if(!float.IsFinite(c.x)||!float.IsFinite(c.speed))throw new Exception("Nonfinite");}session.Resolve(cars,.02f);
 }
 Simulation.Rank(cars,t);Check(cars.All(c=>c.finished&&c.lap==2),"8 cars finish race "+race+" Lv"+level);reports.Add(new{race,level,recovery,cars=cars.Select(c=>new{c.vehicle,c.finishTime,c.rank,c.peakSpeed})});
 Console.WriteLine("RECOVERIES "+recovery);File.WriteAllText("Logs/revision10-races.json",JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
}
