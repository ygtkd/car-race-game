using System.Text.Json;
using CoastRacer.Core;
void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(Vehicles.Badge("winner")=="winner","Legacy badges preserved");
Check(Vehicles.Badge("invalid~top~0~0~1~0~gold~shield~star")=="","Unknown badge rejected");
var custom=Vehicles.Badge("winner~left~0.27~0.81~1.5~45~blue~hexagon~bolt");var design=BadgeDesign.Parse(custom);Check(design.face=="left"&&design.x==.27f&&design.color=="blue"&&design.pattern=="bolt","Custom badge round trip");
var bad=BadgeDesign.Parse(Vehicles.Badge("finish~oops~NaN~Infinity~999~-999~hack~hack~hack"));Check(bad.x==.5f&&bad.y==.72f&&bad.size==1.8f&&bad.angle==-180&&bad.face=="top","Invalid badge settings normalized");
var track0=new Track("ridge");var baseCar=Simulation.Spawn(track0);var boost=Simulation.Spawn(track0);boost.specialTime=5;for(int i=0;i<50;i++){Simulation.StepCar(baseCar,new DriveInput{throttle=1,assist=0},track0,.02f);Simulation.StepCar(boost,new DriveInput{throttle=1,assist=0},track0,.02f);}Check(boost.speed>baseCar.speed*2,"Boost clearly changes one-second acceleration");float before=boost.speed;for(int i=0;i<30;i++)Simulation.StepCar(boost,new DriveInput{throttle=1,brake=1,assist=0},track0,.02f);Check(boost.speed<before-5,"Brake overrides boosted throttle");
Check(SpecialPower.BoostAcceleration==4.5f&&SpecialPower.Grip==4&&SpecialPower.ShieldMass==9,"Fivefold performance increments");
var rows=new List<object>();var uses=new HashSet<string>();
for(int race=0;race<10;race++){
 var track=new Track(race%2==0?"ridge":"suzuka");var session=new RaceSession(track);var cars=Enumerable.Range(0,8).Select(i=>{var c=Simulation.Spawn(track,i);c.id="test-"+i;c.vehicle=Vehicles.All[(i+race)%8].id;return c;}).ToArray();var recoveries=new int[8];var grass=new float[8];
 for(int step=0;step<90000&&cars.Any(c=>!c.finished);step++){
  for(int i=0;i<cars.Length;i++){var c=cars[i];bool recovering=c.recoveryProtection>0;if(c.gauge>=1&&c.specialTime==0){if(session.Activate(c,cars))uses.Add(c.vehicle);}Simulation.StepCar(c,session.Bot(c,.86f+race%3*.025f),track,.02f);if(!recovering&&c.recoveryProtection>0)recoveries[i]++;if(c.onGrass)grass[i]+=.02f;CheckFinite(c);}
  session.Resolve(cars,.02f);
 }
 Simulation.Rank(cars,track);Check(cars.All(c=>c.finished&&c.lap==2&&c.finishTime>0),"Race "+(race+1)+" "+track.id+" all eight cars finish two laps");Check(cars.Select(c=>c.rank).Distinct().Count()==8,"Race "+(race+1)+" ranks unique");
 var report=cars.Select((c,i)=>new{c.vehicle,c.rank,c.finishTime,c.specialUses,c.coins,recoveries=recoveries[i],grassSeconds=grass[i]}).ToArray();rows.Add(new{race=race+1,track=track.id,cars=report});Console.WriteLine(JsonSerializer.Serialize(rows[^1]));
}
Check(uses.Count==8,"All eight specials exercised in ten races");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/revision6-ten-races.json",JsonSerializer.Serialize(new{races=rows,acceleration=new{normal=baseCar.speed,boosted=before},note="Ten automated races in the shared production driving engine. Not physical-device testing."},new JsonSerializerOptions{WriteIndented=true}));
static void CheckFinite(CarState c){if(!float.IsFinite(c.x)||!float.IsFinite(c.y)||!float.IsFinite(c.z)||!float.IsFinite(c.speed)||c.speed<0)throw new Exception("Non-finite vehicle state");}
