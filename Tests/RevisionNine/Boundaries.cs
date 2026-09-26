using CoastRacer.Core;
static class RevisionNineChecks {
 public static void Run(){
 void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
 var t=new Track("shonan");var o=t.points[0];var axis=t.Tangent(0);
 Check(!Simulation.CrossedFinish(o-axis*5,o-axis*.01f,t,out _),"New line does not finish before crossing");
 Check(!Simulation.CrossedFinish(o+axis,o-axis,t,out _),"New line rejects reverse");
 Check(!Simulation.CrossedFinish(o-axis+new Point(0,9,0),o+axis+new Point(0,9,0),t,out _),"New line rejects wrong height");
 foreach(float dt in new[]{.02f,.05f})foreach(int lap in new[]{-1,0,1}){
 var c=Simulation.Spawn(t);c.lap=Math.Max(0,lap);c.gate=lap<0?1:Track.Gates;c.index=639;c.yaw=t.Yaw(0);c.speed=20;c.vx=axis.x*20;c.vz=axis.z*20;
 var p=o-axis*(Simulation.FrontLength(c.vehicle)+.05f);c.x=p.x;c.y=p.y;c.z=p.z;
 Simulation.StepCar(c,new DriveInput{throttle=1},t,dt);
 Check(c.lap==(lap<0?0:lap+1)&&c.finished==(lap==1),"Initial/first/final crossing lap="+lap+" dt="+dt);
 }
 foreach(bool east in new[]{true,false}){
 int i=Enumerable.Range(0,640).First(i=>t.IsSeaBridge(i)&&(t.points[i].x>0)==east);
 var c=Simulation.Spawn(t);c.safeIndex=i;c.safePoint=t.points[i];c.safeYaw=t.Yaw(i);c.index=(i+300)%640;Simulation.Recover(c,t);
 Check(c.index==i&&(c.x>0)==east,"Recovery keeps correct bridge lane "+east);
 }
 var session=new RaceSession(t);
 foreach(var coin in session.coins){var p=t.points[coin.index];var tangent=t.Tangent(coin.index);float side=(coin.x-p.x)*tangent.z-(coin.z-p.z)*tangent.x;float turn=Mathx.Angle(t.Yaw((coin.index+5)%640)-t.Yaw((coin.index+635)%640));
 Check(Math.Abs(side)<Track.RoadEdge&&side*turn<0,"Coin on outside of driveable curve "+coin.id);
 }
 }
}
