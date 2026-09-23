using CoastRacer.Core;
static class RevisionFourTests {
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS R4 "+label);}
 public static void Run(){
  foreach(var id in new[]{"ridge","suzuka"}){
   var t=new Track(id);
   for(int gate=1;gate<Track.Gates;gate++)foreach(float side in new[]{-12f,12f}){
    int i=gate*Track.Samples/Track.Gates;var p=t.points[i];var tangent=t.Tangent(i);var c=Simulation.Spawn(t);c.index=i;c.gate=gate;c.x=p.x+tangent.z*side;c.y=p.y;c.z=p.z-tangent.x*side;c.speed=8;c.yaw=t.Yaw(i);
    Simulation.StepCar(c,new DriveInput{throttle=1},t,.02f);if(c.gate!=gate+1)Console.WriteLine($"R4 missed index={c.index} expected={i} yaw={c.yaw} tangent={t.Yaw(c.index)} wrongWay={c.wrongWay}");Check(c.gate==gate+1,id+" runoff gate "+gate+" side "+side);
   }
   var axis=t.Tangent(0);var start=t.points[0];var slow=Simulation.Spawn(t);slow.gate=Track.Gates;slow.lap=1;slow.speed=.4f;slow.yaw=t.Yaw(0);slow.vx=axis.x*.4f;slow.vz=axis.z*.4f;var before=start-axis*(Simulation.FrontLength(slow.vehicle)+.001f);slow.x=before.x;slow.z=before.z;slow.index=639;
   Simulation.StepCar(slow,new DriveInput(),t,.02f);Check(slow.finished&&slow.lap==2,id+" crawling final crossing finishes");float finish=slow.finishTime;Simulation.StepCar(slow,new DriveInput{throttle=1},t,.02f);Check(slow.finishTime==finish&&slow.lap==2,id+" finish is recorded once");
   var missing=Simulation.Spawn(t);missing.gate=10;missing.lap=1;missing.x=before.x;missing.z=before.z;missing.speed=20;missing.yaw=t.Yaw(0);missing.index=639;Simulation.StepCar(missing,new DriveInput(),t,.02f);Check(!missing.finished,id+" shortcut remains rejected");
  }
  var suzuka=new Track("suzuka");Check(suzuka.ForeignRoadBelow(489,35),"upper crossing needs bridge deck");Check(!suzuka.ForeignRoadBelow(225,35),"lower crossing is not a bridge roof");
  foreach(int i in new[]{225,489}){
   var p=suzuka.points[i];Check(!suzuka.SceneryClear(p+new Point(0,30,0),8),"scenery cannot occupy road footprint at layer "+i);
   var c=Simulation.Spawn(suzuka);c.index=i;c.x=p.x;c.y=p.y;c.z=p.z;c.yaw=suzuka.Yaw(i);c.speed=15;Simulation.StepCar(c,new DriveInput{throttle=1},suzuka,.02f);Check(Math.Abs(c.index-i)<=6&&Math.Abs(c.y-p.y)<1,"crossing keeps its own height and section "+i);
  }
 }
}