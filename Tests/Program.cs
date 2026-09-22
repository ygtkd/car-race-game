using CoastRacer.Core;
static void Check(bool v,string label){if(!v)throw new Exception(label);Console.WriteLine("PASS "+label);}
foreach(string id in new[]{"ridge","suzuka"}){
 var t=new Track(id);Check(t.Length>900,id+" length "+t.Length.ToString("F0"));
 Check(t.points.Max(p=>p.y)-t.points.Min(p=>p.y)>10,id+" elevation");
 var stopped=Simulation.Spawn(t);for(int i=0;i<500;i++)Simulation.StepCar(stopped,new DriveInput(),t,.02f);
 Check(stopped.lap==0&&stopped.gate==1,id+" parked cannot earn laps");
 var spoof=Simulation.Spawn(t);
 for(int i=0;i<5;i++){spoof.index=Track.Samples-1;spoof.x=t.points[0].x;spoof.z=t.points[0].z;spoof.speed=10;Simulation.StepCar(spoof,new DriveInput(),t,.02f);}
 Check(spoof.lap==0,id+" finish shortcut rejected");
 var c=Simulation.Spawn(t);float maxOffroad=0;
 for(int i=0;i<90000&&!c.finished;i++){
  Simulation.StepCar(c,Simulation.Bot(c,t,.85f),t,.02f);maxOffroad+=c.offroad*.02f;
  if(i%15000==0)Console.WriteLine(id+" tick "+i+" lap "+c.lap+" gate "+c.gate+" index "+c.index+" speed "+c.speed);
 }
 Console.WriteLine(id+" time "+c.elapsed+" laps "+c.lap+" gate "+c.gate+" offroadSeconds "+maxOffroad);
 Check(c.finished,id+" AI can complete three laps");
 float before=c.elapsed;c.finished=false;int gate=c.gate;Simulation.Recover(c,t);
 Check(c.gate==gate&&c.elapsed>=before+2.99f,id+" recovery penalty and gate preservation");
}
Console.WriteLine("All core checks passed.");
ExpansionTests.Run();
