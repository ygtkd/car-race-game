using CoastRacer.Core;
static class RevisionFiveTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS R5 "+label);}
 public static void Run(){
  foreach(var id in new[]{"ridge","suzuka"}){
   var t=new Track(id);var axis=t.Tangent(0);var right=new Point(axis.z,0,-axis.x);
   foreach(float side in new[]{-15f,-12f,12f,15f}){
    var origin=t.points[0]+right*side;Check(Simulation.CrossedFinish(origin-axis,origin+axis,t,out _),id+" grey crossing "+side);
    Check(!Simulation.CrossedFinish(origin+axis,origin-axis,t,out _),id+" grey reverse rejected "+side);
    var c=Simulation.Spawn(t);c.lap=1;c.gate=Track.Gates;c.index=639;c.yaw=t.Yaw(0);c.speed=20;c.vx=axis.x*20;c.vz=axis.z*20;var p=origin-axis*(Simulation.FrontLength(c.vehicle)+.01f);c.x=p.x;c.y=p.y;c.z=p.z;
    Simulation.StepCar(c,new DriveInput{throttle=1},t,.02f);Check(c.finished&&c.lap==2,id+" grey final lap "+side);
   }
   var grass=t.points[0]+right*18;Check(!Simulation.CrossedFinish(grass-axis,grass+axis,t,out _),id+" grass finish rejected");
  }
  var track=new Track("ridge");var session=new RaceSession(track);
  foreach(var vehicle in Vehicles.All){var car=Simulation.Spawn(track);car.vehicle=vehicle.id;car.gauge=1;Check(session.Activate(car,new[]{car})&&car.specialTime==5,vehicle.id+" lasts five seconds");for(int n=0;n<249;n++)Simulation.StepCar(car,new DriveInput{brake=1},track,.02f);Check(car.specialTime>0,vehicle.id+" remains active until five seconds");for(int n=0;n<3;n++)Simulation.StepCar(car,new DriveInput{brake=1},track,.02f);Check(car.specialTime==0,vehicle.id+" expires after five seconds");}
  var pulse=Simulation.Spawn(track);var target=Simulation.Spawn(track);pulse.vehicle="atlas";pulse.gauge=1;target.x=pulse.x+20;target.z=pulse.z;target.y=pulse.y;target.speed=30;
  session.Activate(pulse,new[]{pulse,target});Check(Math.Abs(target.speed-6f)<.001f&&target.jamTime==5,"pulse reduction is bounded and lasts five seconds");
  foreach(var vehicle in Vehicles.All.Where(v=>new[]{"boost","dash","cadence","feast","grip","surf"}.Contains(v.special))){var normal=Simulation.Spawn(track);var powered=Simulation.Spawn(track);normal.vehicle=powered.vehicle=vehicle.id;powered.specialTime=5;Simulation.StepCar(normal,new DriveInput{throttle=1,assist=0},track,.02f);Simulation.StepCar(powered,new DriveInput{throttle=1,assist=0},track,.02f);float bonus=vehicle.special=="boost"||vehicle.special=="dash"?3.50f:vehicle.special=="cadence"?3.00f:vehicle.special=="feast"?2.00f:1.20f;Check(Math.Abs(powered.speed-normal.speed-vehicle.acceleration*bonus*.02f)<.0001f,vehicle.id+" current acceleration increment");}
 }
}
