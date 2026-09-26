using CoastRacer.Core;
using System.Text.Json;
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
if(args.Contains("--search")){
 var scores=new List<object>();foreach(var id in new[]{"ridge","suzuka","shonan"})foreach(float aggression in new[]{1.14f,1.40f,1.60f,1.80f,2.0f}){
 var t=new Track(id);var c=Simulation.Spawn(t);c.cpuLevel=5;int recovery=0;
 for(int i=0;i<70000&&!c.finished;i++){bool before=c.recoveryProtection>0;Simulation.StepCar(c,Simulation.Bot(c,t,aggression),t,.02f);if(!before&&c.recoveryProtection>0)recovery++;}
 scores.Add(new{id,aggression,c.finished,c.finishTime,recovery});Console.WriteLine($"{id} {aggression}: {c.finishTime} recoveries={recovery}");}
 File.WriteAllText("Logs/revision8-cpu-search.json",JsonSerializer.Serialize(scores,new JsonSerializerOptions{WriteIndented=true}));return;
}
Boundaries.Run();
var maps=new[]{"ridge","suzuka","shonan"}.Select(id=>{var t=new Track(id);Console.WriteLine(id+" length "+t.Length);return new{id,points=t.points,bridges=Enumerable.Range(0,Track.Samples).Select(i=>t.ForeignRoadBelow(i,22,4)||t.IsSeaBridge(i)).ToArray()};}).ToArray();
File.WriteAllText("Art/Blender/tracks.json",JsonSerializer.Serialize(maps,new JsonSerializerOptions{IncludeFields=true}));
Check(new Track("shonan").Length>=3000&&new Track("shonan").Length<=4000,"Shonan 3-4km");
var track=new Track("ridge");
foreach(var vehicle in new[]{"atlas","stormbike"})foreach(var range in new[]{29.9f,30,30.1f}){var a=Simulation.Spawn(track);a.vehicle=vehicle;a.gauge=1;var b=Simulation.Spawn(track);b.x=a.x+range;new RaceSession(track).Activate(a,new[]{a,b});Check((b.jamTime>0)==(range<=30),vehicle+" radius "+range);}
var shield=Simulation.Spawn(track);shield.vehicle="vortex";shield.gauge=1;var attack=Simulation.Spawn(track);attack.vehicle="atlas";var session=new RaceSession(track);session.Activate(shield,new[]{shield});
for(int i=0;i<550;i++)Simulation.StepCar(shield,new DriveInput{brake=1},track,.02f);
float left=shield.abilityImmunity;attack.x=shield.x+3;attack.z=shield.z;attack.gauge=1;session.Activate(attack,new[]{shield,attack});Check(shield.guardBoost==5&&shield.guardBlocks==1&&shield.abilityImmunity==left&&shield.jamTime==0,"Late block boosts without extending immunity");attack.specialTime=0;attack.gauge=1;session.Activate(attack,new[]{shield,attack});Check(shield.guardBlocks==1,"Block cannot stack");
for(int i=0;i<60;i++)Simulation.StepCar(shield,new DriveInput{brake=1},track,.02f);Check(shield.abilityImmunity==0&&shield.guardBoost>3&&shield.guardFx>3,"Late boost and FX outlive immunity");
var a1=Simulation.Spawn(track);var a2=Simulation.Spawn(track);a1.speed=a2.speed=30;a2.z=a1.z+12;a2.yaw=a1.yaw=0;a2.index=track.Nearest(a2.Position);
for(int i=0;i<110;i++)RacingExtras.Drafting(new[]{a1,a2},track,.02f);Check(a1.draftPower>.99f,"Draft charges behind car");a2.finished=true;for(int i=0;i<51;i++)RacingExtras.Drafting(new[]{a1,a2},track,.02f);Check(a1.draftPower==0,"Draft fades and excludes finisher");
var compare=new List<object>();foreach(var id in new[]{"ridge","suzuka"})foreach(var vehicle in new[]{"apex","atlas","aerobike"})foreach(var level in new[]{2,5}){
 var t=new Track(id);var c=Simulation.Spawn(t);c.vehicle=vehicle;c.cpuLevel=level;int recoveries=0;var benchmark=new RaceSession(t);foreach(var coin in benchmark.coins){coin.active=false;coin.respawnAt=float.MaxValue;}
 for(int i=0;i<60000&&!c.finished;i++){bool before=c.recoveryProtection>0;Simulation.StepCar(c,benchmark.TrafficBot(c,new[]{c},Simulation.Difficulty(level)),t,.02f);if(!before&&c.recoveryProtection>0)recoveries++;}
 Check(c.finished,id+" "+vehicle+" Lv"+level+" benchmark");if(level==2){using var baseline=JsonDocument.Parse(File.ReadAllText("docs/REVISION_8_CPU_BASELINE.json"));var old=baseline.RootElement.EnumerateArray().First(x=>x.GetProperty("id").GetString()==id&&x.GetProperty("vehicle").GetString()==vehicle).GetProperty("finishTime").GetSingle();Check(Math.Abs(c.finishTime/old-1)<.05f,"Level 2 matches old level 5 within five percent "+id+" "+vehicle);}compare.Add(new{id,vehicle,level,c.finishTime,recoveries});
}
File.WriteAllText("Logs/revision8-cpu.json",JsonSerializer.Serialize(compare,new JsonSerializerOptions{WriteIndented=true}));
var reports=new List<object>();
for(int race=0;race<10;race++){
 var t=new Track(new[]{"ridge","suzuka","shonan"}[race%3]);var r=new RaceSession(t);int level=race%5+1;var slots=Simulation.Grid(8,new Random(80+race));var cars=Enumerable.Range(0,8).Select(i=>{var c=Simulation.Spawn(t,slots[i]);c.id="car"+i;c.vehicle=Vehicles.All[(i+race)%10].id;c.cpuLevel=level;c.bot=true;return c;}).ToArray();int recovery=0;
 for(int step=0;step<90000&&cars.Any(c=>!c.finished);step++){
  foreach(var c in cars){bool before=c.recoveryProtection>0;r.UseBotSpecial(c,cars);Simulation.StepCar(c,r.TrafficBot(c,cars,Simulation.Difficulty(level)),t,.02f);if(!before&&c.recoveryProtection>0)recovery++;CheckFinite(c);}r.Resolve(cars,.02f);
 }
 Simulation.Rank(cars,t);Check(cars.All(c=>c.finished&&c.lap==2),"Race "+race+" "+t.id+" Lv"+level+" 8 finish");
 reports.Add(new{race,track=t.id,level,recovery,cars=cars.Select(c=>new{c.vehicle,c.finishTime,c.rank,c.specialUses,c.guardBlocks,c.cornerBonuses})});
}
File.WriteAllText("Logs/revision8-races.json",JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
void CheckFinite(CarState c){if(!float.IsFinite(c.x)||!float.IsFinite(c.speed))throw new Exception("Nonfinite");}