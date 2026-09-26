using CoastRacer.Core;
using System.Text.Json;
static class Boundaries {
 public static void Run(){
  void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
  var t=new Track("ridge");
  foreach(var dt in new[]{.02f,.05f})foreach(var special in new[]{"vortex","kebab"}){
   var c=Simulation.Spawn(t);c.vehicle=special;c.gauge=1;var r=new RaceSession(t);r.Activate(c,new[]{c});
   for(int i=0;i<(int)Math.Round(5/dt);i++)Simulation.StepCar(c,new DriveInput{brake=1},t,dt);
   Check(c.abilityImmunity>6.9f&&c.guardBoost==0,"Five-second skill leaves immunity "+special+" "+dt);
   for(int i=0;i<(int)Math.Round(7.05/dt);i++)Simulation.StepCar(c,new DriveInput{brake=1},t,dt);
   Check(c.abilityImmunity<.001f&&c.guardBoost==0,"Unattacked immunity expires "+special+" "+dt);
  }
  var passive=Simulation.Spawn(t);passive.jamImmunity=10;RacingExtras.Block(passive);Check(passive.guardBoost==0&&passive.guardBlocks==0,"Passive immunity does not grant counterboost");
  var a=Simulation.Spawn(t);a.vehicle="atlas";a.gauge=1;var b=Simulation.Spawn(t);b.y+=4;new RaceSession(t).Activate(a,new[]{a,b});Check(b.jamTime==0,"High-low roads excluded from pulse");
  var lead=Simulation.Spawn(t);lead.z+=12;lead.index=t.Nearest(lead.Position);lead.speed=30;a=Simulation.Spawn(t);a.speed=30;
  for(int i=0;i<49;i++)RacingExtras.Drafting(new[]{a,lead},t,.02f);Check(a.draftPower==0,"Draft not granted before one second");
  lead.yaw=Mathx.Pi;for(int i=0;i<110;i++)RacingExtras.Drafting(new[]{a,lead},t,.02f);Check(a.draftPower==0,"Opposite direction cannot draft");lead.yaw=0;lead.y+=4;for(int i=0;i<110;i++)RacingExtras.Drafting(new[]{a,lead},t,.02f);Check(a.draftPower==0,"Different height cannot draft");
  int begin=Enumerable.Range(1,Track.Samples-2).First(i=>t.cornerIds[i]>=0&&t.cornerIds[i-1]<0);int end=begin;while(end<Track.Samples&&t.cornerIds[end]>=0)end++;
  CarState Drive(int invalid=0){var c=Simulation.Spawn(t);for(int i=begin-1;i<=end;i++){c.index=i;c.speed=t.TargetSpeed(i);c.offroad=invalid==1&&i==begin+2?1:0;c.wrongWay=invalid==2&&i==begin+2;for(int repeat=0;repeat<3;repeat++){if(invalid==3)c.speed=1;RacingExtras.Corner(c,t);}}return c;}
  var clean=Drive();Check(clean.cornerBonuses==1&&Math.Abs(clean.gauge-.03f)<.0001f,"Clean full corner awards three percent");
  foreach(int invalid in new[]{1,2,3})Check(Drive(invalid).cornerBonuses==0,"Invalid corner denied "+invalid);
  for(int i=begin-1;i<=end;i++){clean.index=i;clean.speed=t.TargetSpeed(i);for(int n=0;n<3;n++)RacingExtras.Corner(clean,t);}Check(clean.cornerBonuses==1,"Repeated corner cannot farm gauge");
  var middle=Simulation.Spawn(t);for(int i=begin+2;i<=end;i++){middle.index=i;middle.speed=t.TargetSpeed(i);for(int n=0;n<3;n++)RacingExtras.Corner(middle,t);}Check(middle.cornerBonuses==0,"Spawn inside bend cannot claim entry");
  var coast=new Track("shonan");int east=Enumerable.Range(0,Track.Samples).First(i=>coast.IsSeaBridge(i)&&coast.points[i].x>0);int west=Enumerable.Range(0,Track.Samples).First(i=>coast.IsSeaBridge(i)&&coast.points[i].x<0);
  Check(coast.Tangent(east).z<0&&coast.Tangent(west).z>0,"East outbound and west inbound bridge directions");
  var car=Simulation.Spawn(coast);car.index=east;var point=coast.points[east];car.x=0;car.z=point.z;car.y=point.y;car.yaw=coast.Yaw(east);Simulation.StepCar(car,new DriveInput(),coast,.02f);Check(car.x>1&&coast.points[car.index].x>0,"Median collision retains east carriageway");
  var serialized=JsonSerializer.Serialize(clean,new JsonSerializerOptions{IncludeFields=true});var restored=JsonSerializer.Deserialize<CarState>(serialized,new JsonSerializerOptions{IncludeFields=true})!;Check(restored.cornerMask==clean.cornerMask&&restored.cornerBonuses==clean.cornerBonuses,"Snapshot retains corner award deduplication");
  Check(coast.RecordKey=="shonan-v3"&&new Track("suzuka").RecordKey=="suzuka","Only changed layout changes record key");
 }
}