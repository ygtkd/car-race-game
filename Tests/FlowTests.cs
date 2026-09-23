using CoastRacer.Core;
static class FlowTests {
 static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
 public static void Run(){
  foreach(var id in new[]{"ridge","suzuka"}){
   var t=new Track(id);var axis=t.Tangent(0);var origin=t.points[0];
   Check(!Simulation.CrossedFinish(origin-axis*5,origin-axis*.01f,t,out _),id+" before line is not finish");
   Check(Simulation.CrossedFinish(origin-axis*10,origin+axis*10,t,out var fraction)&&Math.Abs(fraction-.5f)<.001,id+" swept finish crossing");
   Check(!Simulation.CrossedFinish(origin+axis,origin-axis,t,out _),id+" reverse rejected");
   Check(!Simulation.CrossedFinish(origin-axis+new Point(0,10,0),origin+axis+new Point(0,10,0),t,out _),id+" different vertical layer rejected");
   var c=Simulation.Spawn(t);c.gate=Track.Gates;c.lap=1;c.yaw=t.Yaw(0);c.speed=20;c.vx=axis.x*20;c.vz=axis.z*20;var p=origin-axis*2.5f;c.x=p.x;c.z=p.z;c.index=639;
   Simulation.StepCar(c,new DriveInput{throttle=1},t,.02f);Check(c.finished&&c.lap==2,id+" front crosses line to finish");
   var safe=Simulation.Spawn(t);int index=120;var center=t.points[index];var tangent=t.Tangent(index);safe.index=index;safe.x=center.x;safe.y=center.y;safe.z=center.z;
   Simulation.StepCar(safe,new DriveInput(),t,.02f);float last=safe.safeDistance;safe.x+=tangent.z*18;safe.z-=tangent.x*18;
   for(int i=0;i<200;i++)Simulation.StepCar(safe,new DriveInput(),t,.02f);
   Check(safe.recoveryRemaining>.9f&&safe.recoveryProtection==0,id+" five second grace");
   for(int i=0;i<52;i++)Simulation.StepCar(safe,new DriveInput(),t,.02f);
   Check((safe.Position-t.At(last)).Length<.2f&&safe.recoveryProtection>0,id+" recover at last road centre");
   var curb=Simulation.Spawn(t);curb.index=0;curb.x=origin.x+axis.z*18;curb.z=origin.z-axis.x*18;Simulation.StepCar(curb,new DriveInput(),t,.02f);Check(curb.onGrass&&curb.recoveryRemaining>0,id+" grass enables warning");curb.x=origin.x+axis.z*7.5f;curb.z=origin.z-axis.x*7.5f;Simulation.StepCar(curb,new DriveInput(),t,.02f);Check(!curb.onGrass&&curb.recoveryRemaining==0&&curb.recoveryElapsed==0,id+" kerb clears warning immediately");
   var grey=Simulation.Spawn(t);var saved=grey.safePoint;
   grey.index=0;grey.x=origin.x+axis.z*12;grey.z=origin.z-axis.x*12;
   for(int n=0;n<310;n++)Simulation.StepCar(grey,new DriveInput{brake=1},t,.02f);
   Check(!grey.onGrass&&grey.recoveryRemaining==0&&grey.recoveryProtection==0,id+" grey runoff never triggers recovery after six seconds");
   Check((grey.safePoint-saved).Length<.01f,id+" runoff preserves last road recovery anchor");
   grey.x=origin.x+axis.z*18;grey.z=origin.z-axis.x*18;grey.index=0;Simulation.StepCar(grey,new DriveInput(),t,.02f);
   Check(grey.recoveryRemaining>4.9f,id+" leaving runoff starts fresh five second warning");
   grey.x=origin.x+axis.z*12;grey.z=origin.z-axis.x*12;grey.index=0;Simulation.StepCar(grey,new DriveInput(),t,.02f);
   Check(grey.recoveryRemaining==0&&!grey.onGrass,id+" grass to grey cancels recovery");
   var fast=Simulation.Spawn(t);fast.index=0;fast.x=origin.x+axis.z*12;fast.z=origin.z-axis.x*12;fast.speed=40;fast.yaw=t.Yaw(0);Simulation.StepCar(fast,new DriveInput{throttle=1,assist=0},t,.02f);
   Check(fast.speed<40,id+" grey slows full throttle at racing speed");
   var anchor=Simulation.Spawn(t);anchor.safeIndex=320;anchor.safePoint=t.points[320];anchor.safeYaw=t.Yaw(320);anchor.index=0;Simulation.Recover(anchor,t);Check(anchor.index==320&&(anchor.Position-t.points[320]).Length<.01f&&anchor.yaw==t.Yaw(320),id+" recovery retains road segment even far from current hint");
   var a=new CarState{finished=true,finishTime=100,elapsed=100,id="human"};var b=new CarState{bot=true,id="bot1",elapsed=100};var d=new CarState{bot=true,id="bot2",index=300,gate=13,elapsed=100};
   var list=new[]{a,b,d};Check(Simulation.SettleOnline(list,t)&&b.estimated&&d.estimated&&d.finishTime<b.finishTime&&a.finishTime==100,id+" CPU only estimates preserve human time");
   var lastHuman=new CarState{id="last",elapsed=100};Check(Simulation.SettleOnline(new[]{a,lastHuman},t)&&lastHuman.dnf&&!lastHuman.estimated&&lastHuman.finishTime==0,id+" final human gets no time");
   var h1=new CarState{id="h1"};var h2=new CarState{id="h2"};Check(!Simulation.SettleOnline(new[]{h1,h2,b},t)&&!h1.estimated,id+" never estimate humans");
   var tied=new CarState{finished=true,finishTime=100.01f};Check(Simulation.SettleOnline(new[]{a,tied},t)&&!tied.dnf,id+" simultaneous crossing retains both measured times");
   var race=new RaceSession(t);var eight=Enumerable.Range(0,8).Select(i=>{var car=Simulation.Spawn(t,i);car.id="bot"+i;car.bot=true;car.vehicle=Vehicles.All[i].id;return car;}).ToArray();
   for(int n=0;n<45000&&eight.Any(x=>!x.finished);n++){foreach(var car in eight){race.UseBotSpecial(car,eight);Simulation.StepCar(car,race.Bot(car,.9f),t,.02f);}race.Resolve(eight,.02f);}
   Check(eight.All(x=>x.finished&&float.IsFinite(x.finishTime)),id+" eight distinct cars finish full race");
  }
 }
}
