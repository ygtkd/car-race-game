using System;
using System.Collections.Generic;

namespace CoastRacer.Core
{
    [Serializable] public struct Point
    {
        public float x,y,z;
        public Point(float X,float Y,float Z){x=X;y=Y;z=Z;}
        public static Point operator +(Point a,Point b)=>new Point(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Point operator -(Point a,Point b)=>new Point(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Point operator *(Point a,float f)=>new Point(a.x*f,a.y*f,a.z*f);
        public float Length=>(float)Math.Sqrt(x*x+y*y+z*z);
    }
    public static class Mathx
    {
        public const float Pi=(float)Math.PI;
        public static float Clamp(float x,float lo,float hi)=>Math.Max(lo,Math.Min(hi,x));
        public static float Angle(float x){while(x>Pi)x-=2*Pi;while(x< -Pi)x+=2*Pi;return x;}
        public static float Move(float a,float b,float amount)=>a+Clamp(b-a,-amount,amount);
    }
    public sealed class Track
    {
        public const int Samples=640;
        public const int Gates=24;
        public const float RoadEdge=8.1f, GrassEdge=16f, BarrierEdge=25f;
        public readonly string id;
        public readonly Point[] points=new Point[Samples];
        public readonly float[] distance=new float[Samples+1];
        public readonly int[] cornerIds;public readonly float width=14;
        public string RecordKey=>id=="shonan"?"shonan-v3":id;
        public bool IsSeaBridge(int i)=>id=="shonan"&&points[i].z< -90&&points[i].z> -390&&Math.Abs(points[i].x)<12;
        public float BarrierAt(int i)=>IsSeaBridge(i)?8.3f:BarrierEdge;
        public float Length=>distance[Samples];
        public Track(string name)
        {
            id=name=="suzuka"?"suzuka":name=="shonan"?"shonan":"ridge";
            Point[] controls=id=="suzuka"?Suzuka():id=="shonan"?Shonan():Ridge();
            for(int i=0;i<Samples;i++){
                float t=(float)i/Samples*controls.Length;int k=(int)t;t-=k;
                Point a=controls[(k-1+controls.Length)%controls.Length],b=controls[k],c=controls[(k+1)%controls.Length],d=controls[(k+2)%controls.Length];
                points[i]=(b*2+(c-a)*t+(a*2-b*5+c*4-d)*(t*t)+(b*3-a-c*3+d)*(t*t*t))*.5f;
                if(i>0)distance[i]=distance[i-1]+(points[i]-points[i-1]).Length;
            }
            distance[Samples]=distance[Samples-1]+(points[0]-points[Samples-1]).Length;
            if(id=="shonan"){
                // Rebase the closed route 600m before the mainland bridge entrance.
                int entry=Nearest(new Point(9.5f,7,-90));float target=(distance[entry]-600+Length)%Length;
                int start=0;for(int i=1;i<Samples;i++)if(Math.Abs(distance[i]-target)<Math.Abs(distance[start]-target))start=i;
                var original=(Point[])points.Clone();for(int i=0;i<Samples;i++)points[i]=original[(start+i)%Samples];
                distance[0]=0;for(int i=1;i<=Samples;i++)distance[i]=distance[i-1]+(points[i%Samples]-points[i-1]).Length;
            }
            cornerIds=RacingExtras.BuildCorners(this);
        }
        static Point[] Shonan()=>new[]{
            new Point(9.5f,7,0),new Point(9.5f,7,-100),new Point(9.5f,7,-200),new Point(9.5f,7,-300),new Point(9.5f,7,-400),
            new Point(85,10,-460),new Point(155,18,-485),new Point(215,27,-550),new Point(170,34,-615),
            new Point(220,38,-690),new Point(150,43,-770),new Point(60,47,-805),new Point(-20,43,-750),
            new Point(-110,35,-790),new Point(-205,27,-700),new Point(-165,21,-625),new Point(-225,16,-550),
            new Point(-160,11,-485),new Point(-60,8,-440),new Point(-9.5f,7,-400),new Point(-9.5f,7,-300),new Point(-9.5f,7,-200),new Point(-9.5f,7,-100),
            new Point(-65,7,12),new Point(-250,7,40),new Point(-340,7,40),new Point(-517,7,40),new Point(-612,8,95),
            new Point(-639,10,210),new Point(-564,11,315),new Point(-445,10,330),new Point(-355,9,295),
            new Point(-272,9,320),new Point(-190,8,280),new Point(-100,7,270),new Point(-40,7,205),new Point(9.5f,7,110)};
        static Point[] Ridge()=>new[]{
            new Point(0,6,0),new Point(0,6,100),new Point(0,7,220),new Point(40,10,285),
            new Point(125,17,280),new Point(170,23,220),new Point(130,30,165),
            new Point(185,34,120),new Point(265,26,175),new Point(330,19,175),
            new Point(355,14,85),new Point(350,11,-40),new Point(300,8,-115),
            new Point(240,5,-100),new Point(225,4,-30),new Point(170,8,-15),
            new Point(130,12,-85),new Point(90,15,-155),new Point(25,11,-160),new Point(-15,7,-90)};
        static Point[] Suzuka()
        {
            // Hand-drawn from the official circuit map. Plan scale and elevations are intentionally approximate.
            float[,] raw={
                {340,451,6},{240,435,6},{150,420,6},{115,408,6},{105,380,6},{128,362,7},
                {198,380,9},{250,363,11},{305,394,13},{355,369,15},{386,382,18},{410,424,20},
                {472,416,23},{510,390,25},{527,335,26},{528,279,24},{570,239,18},
                {630,284,10},{674,320,7},{687,347,6},{697,363,6},{706,355,6},
                {702,299,9},{719,261,13},{752,232,17},{803,207,19},{845,204,20},
                {898,230,21},{931,239,21},{958,218,22},{970,193,22},{954,177,22},
                {918,172,22},{850,176,22},{800,185,21},{690,224,21},{600,259,21},
                {568,288,20},{552,336,18},{547,379,15},{530,417,10},{514,419,8},
                {502,435,7},{505,448,7},{491,463,6},{458,481,6},{402,477,6}};
            var result=new Point[raw.GetLength(0)];
            for(int i=0;i<result.Length;i++)result[i]=new Point((raw[i,0]-340)*1.45f,raw[i,2],(451-raw[i,1])*1.45f);
            return result;
        }
        public Point Tangent(int i)
        {
            Point d=points[(i+1)%Samples]-points[(i-1+Samples)%Samples];
            return d*(1/Math.Max(.001f,d.Length));
        }
        public float Yaw(int i){Point d=Tangent(i);return (float)Math.Atan2(d.x,d.z);}
        public int Nearest(Point p,int hint=-1,int span=35)
        {
            float best=float.MaxValue;int found=0;
            int count=hint<0?Samples:span*2+1;
            for(int n=0;n<count;n++){
                int i=hint<0?n:(hint+n-span+Samples)%Samples;
                Point d=p-points[i];float score=d.x*d.x+d.z*d.z+d.y*d.y*4;
                if(score<best){best=score;found=i;}
            }
            return found;
        }
        public Point At(float metres)
        {
            metres=(metres%Length+Length)%Length;
            int lo=0,hi=Samples;
            while(hi-lo>1){int mid=(lo+hi)/2;if(distance[mid]<=metres)lo=mid;else hi=mid;}
            return points[lo]+(points[(lo+1)%Samples]-points[lo])*((metres-distance[lo])/Math.Max(.001f,distance[lo+1]-distance[lo]));
        }
        public float RoadDistance(Point p,int hint){
            float best=float.MaxValue,result=distance[hint];
            foreach(int i in new[]{(hint+Samples-1)%Samples,hint}){Point a=points[i],delta=points[(i+1)%Samples]-a,v=p-a;float f=Mathx.Clamp((v.x*delta.x+v.y*delta.y+v.z*delta.z)/Math.Max(.001f,delta.Length*delta.Length),0,1);float error=(p-(a+delta*f)).Length;if(error<best){best=error;result=distance[i]+f*delta.Length;}}
            return result%Length;
        }
        public bool SceneryClear(Point p,float radius){
            float limit=GrassEdge+radius+2;
            for(int i=0;i<Samples;i++)if(PlanDistanceSquared(p,points[i],points[(i+1)%Samples])<limit*limit)return false;
            return true;
        }
        public static float PlanDistanceSquared(Point p,Point a,Point b){
            float x=b.x-a.x,z=b.z-a.z,f=Mathx.Clamp(((p.x-a.x)*x+(p.z-a.z)*z)/Math.Max(.001f,x*x+z*z),0,1);
            float dx=p.x-a.x-x*f,dz=p.z-a.z-z*f;return dx*dx+dz*dz;
        }
        public bool ForeignRoadBelow(int index,float radius,float clearance=3.5f){
            for(int i=0;i<Samples;i++){
                int separation=Math.Abs(i-index);separation=Math.Min(separation,Samples-separation);
                if(separation<40||points[index].y-points[i].y<clearance)continue;
                if(PlanDistanceSquared(points[index],points[i],points[(i+1)%Samples])<radius*radius)return true;
            }return false;
        }
        public float TargetSpeed(int i)
        {
            float curve=0;
            for(int n=3;n<24;n+=3){
                int j=(i+n)%Samples;
                float turn=Math.Abs(Mathx.Angle(Yaw(j)-Yaw(i)));
                float metres=(distance[j]-distance[i]+Length)%Length;
                curve=Math.Max(curve,turn/Math.Max(1,metres));
            }
            return Mathx.Clamp((float)Math.Sqrt(6.8f/Math.Max(.002f,curve)),13,58);
        }
    }
    [Serializable] public sealed class DriveInput
    {
        public float steer,throttle,brake,assist=1,sensitivity=1;
        public bool recover;
    }
    [Serializable] public sealed class CarState
    {
        public string id="",name="",vehicle="apex",badge="";
        public float recoveryRemaining,recoveryElapsed,recoveryProtection,safeDistance;
        public bool bot,estimated,hasFront,onGrass;public Point lastFront,safePoint;public int safeIndex;public float safeYaw;
        public float abilityImmunity,guardBoost,guardFx,draftCharge,draftPower,cornerBudget,cornerFlash,cornerSpeedSum;
        public bool guardTriggered,cornerClean;public int guardBlocks,cornerActive=-1,cornerLap,cornerMask,cornerSamples,cornerBonuses,cornerPrevious=-1;
        public float gauge,specialTime,jamTime,jamImmunity,distance,furthestDistance;public int coins,specialUses,disruptions,cpuLevel=3,gridSlot;public float peakSpeed,cruiseDistance;public bool night;
        public float x,y,z,yaw,speed,vx,vz,steering,elapsed,lapStart,bestLap,finishTime,offroad,slip;
        public int index,lap,gate=1,rank,gear=1;
        public bool finished,connected=true,dnf,wrongWay;
        public Point Position=>new Point(x,y,z);
    }
    public static class Simulation
    {
        public const float Step=0.02f;
        public const int Laps=2;
        public static CarState Spawn(Track track,int slot=0)
        {
            Point p=track.points[0],t=track.Tangent(0);
            float side=slot%2==0?-2.5f:2.5f;
            p+=new Point(t.z,0,-t.x)*side-t*(slot/2*5);
            int index=track.Nearest(p);float distance=track.RoadDistance(p,index);return new CarState{gridSlot=slot,x=p.x,y=p.y,z=p.z,yaw=track.Yaw(0),index=index,safeIndex=index,safeDistance=distance,safePoint=track.At(distance),safeYaw=track.Yaw(index)};
        }
        public static void Recover(CarState car,Track t)
        {
            int index=car.safeIndex;
            // A penalty makes recovery slower than driving. Returning behind the next gate never awards progress.
            Point p=car.safePoint;car.x=p.x;car.y=p.y;car.z=p.z;car.yaw=car.safeYaw;car.onGrass=false;
            RacingExtras.Reset(car);car.hasFront=false;car.speed=car.vx=car.vz=0;car.index=index;car.elapsed+=3;car.offroad=0;car.recoveryRemaining=car.recoveryElapsed=0;car.recoveryProtection=2;
        }
        public static void StepCar(CarState c,DriveInput input,Track t,float dt)
        {
            if(c.dnf)return;if(c.finished){Cruise(c,t,dt);return;}
            dt=Mathx.Clamp(dt,0,.05f);RacingExtras.Tick(c,dt);c.elapsed+=dt;c.recoveryProtection=Math.Max(0,c.recoveryProtection-dt);
            Point previousFront=c.hasFront?c.lastFront:Front(c);
            float previousDistance=t.RoadDistance(c.Position,c.index);
            float assist=Mathx.Clamp(input.assist,0,1);
            var spec=Vehicles.Get(c.vehicle);c.specialTime=Math.Max(0,c.specialTime-dt);c.jamTime=Math.Max(0,c.jamTime-dt);c.jamImmunity=Math.Max(0,c.jamImmunity-dt);
            bool boost=c.specialTime>0&&(spec.special=="boost"||spec.special=="dash"||spec.special=="aero"),gripActive=c.specialTime>0&&(spec.special=="grip"||spec.special=="surf");
            bool cadence=c.specialTime>0&&spec.special=="cadence",feast=c.specialTime>0&&spec.special=="feast";
            c.index=t.Nearest(c.Position,c.index,6);
            Point center=t.points[c.index],forward=t.Tangent(c.index);
            float lateral=(c.x-center.x)*forward.z-(c.z-center.z)*forward.x;
            bool grass=Math.Abs(lateral)>Track.GrassEdge,runoff=Math.Abs(lateral)>Track.RoadEdge&&!grass;
            float steer=Mathx.Clamp(input.steer* Mathx.Clamp(input.sensitivity,.5f,1.6f),-1,1);
            c.steering=Mathx.Move(c.steering,steer,dt*(assist>0?2.8f:5));
            float throttle=Mathx.Clamp(input.throttle,0,1),brake=Mathx.Clamp(input.brake,0,1);
            // Brake assist limits corner entry speed, but never drives or steers for the player.
            if(assist>0 && !grass && c.speed>t.TargetSpeed(c.index)+3)brake=Math.Max(brake,.45f*assist);
            throttle*=1-brake; // Braking takes priority even while a high-power special is active.
            float slope=forward.y*(float)Math.Cos(Mathx.Angle(c.yaw-t.Yaw(c.index)));
            float acceleration=throttle*spec.acceleration*(c.guardBoost>0?1.10f:1)*(1-.35f*c.speed/spec.maxSpeed)*(boost?SpecialPower.BoostAcceleration:cadence?SpecialPower.CadenceAcceleration:feast?SpecialPower.FeastAcceleration:gripActive?SpecialPower.GripAcceleration:1)-brake*15-.32f-c.speed*c.speed*(c.specialTime>0&&spec.special=="aero"?.0003f:.0015f)*(1-.55f*c.draftPower)-slope*9.81f;
            if(c.jamTime>0)acceleration-=SpecialPower.PulseDrag;
            if(runoff)acceleration-=2.5f+c.speed*.22f;
            if(grass)acceleration-=(3+c.speed*.18f)*(gripActive?0:1);
            c.speed=Mathx.Clamp(c.speed+acceleration*dt,0,spec.maxSpeed*(boost?SpecialPower.BoostSpeed:1)*(c.guardBoost>0?1.10f:1)*(1+.05f*c.draftPower));
            float maxAngle=.58f/(1+c.speed*.04f);
            float wantedYaw=c.speed/2.7f*(float)Math.Tan(c.steering*maxAngle);
            float grip=(grass?4.2f:spec.grip)*(gripActive?SpecialPower.Grip:1);
            float actualYaw=Mathx.Clamp(wantedYaw,-grip/Math.Max(3,c.speed),grip/Math.Max(3,c.speed));
            c.slip=Math.Abs(wantedYaw-actualYaw);
            c.yaw=Mathx.Angle(c.yaw+actualYaw*dt);
            float vx=(float)Math.Sin(c.yaw)*c.speed,vz=(float)Math.Cos(c.yaw)*c.speed;
            float response=Math.Min(1,dt*(grass?4:9+assist*7));
            c.vx+=(vx-c.vx)*response;c.vz+=(vz-c.vz)*response;
            c.x+=c.vx*dt;c.z+=c.vz*dt;
            c.index=t.Nearest(c.Position,c.index,6);center=t.points[c.index];forward=t.Tangent(c.index);
            c.y=center.y;
            lateral=(c.x-center.x)*forward.z-(c.z-center.z)*forward.x;
            c.onGrass=Math.Abs(lateral)>Track.GrassEdge;c.offroad=Math.Abs(lateral)>Track.RoadEdge?1:0;
            if(c.onGrass){c.recoveryElapsed+=dt;c.recoveryRemaining=Math.Max(0,5-c.recoveryElapsed);if(c.recoveryElapsed>=5){Recover(c,t);return;}}else{c.recoveryElapsed=c.recoveryRemaining=0;if(Math.Abs(lateral)<=Track.RoadEdge){c.safeDistance=t.RoadDistance(c.Position,c.index);c.safePoint=t.At(c.safeDistance);c.safeIndex=c.index;c.safeYaw=t.Yaw(c.index);}}
            c.wrongWay=c.speed>3 && Math.Cos(Mathx.Angle(c.yaw-t.Yaw(c.index)))<-.25;
            // Barrier at the outer runoff edge. Vertical layers are selected using local track continuity.
            if(Math.Abs(lateral)>t.BarrierAt(c.index)- (t.IsSeaBridge(c.index)?1:0)){
                c.cornerClean=false;
                float bound=Math.Sign(lateral)*(t.BarrierAt(c.index)-(t.IsSeaBridge(c.index)?1.2f:.2f));
                c.x=center.x+forward.z*bound;c.z=center.z-forward.x*bound;
                c.speed*=.55f;c.vx*=.35f;c.vz*=.35f;
            }
            c.gear=Math.Min(6,1+(int)(c.speed/11));c.peakSpeed=Math.Max(c.peakSpeed,c.speed);
            int gateIndex=(c.gate%Track.Gates)*Track.Samples/Track.Gates;
            int separation=Math.Abs(c.index-gateIndex);separation=Math.Min(separation,Track.Samples-separation);
            float crossFraction=1;
            bool crossed=CrossedFinish(previousFront,Front(c),t,out crossFraction);
            // Intermediate gates cover the legal runoff corridor. Ordered gates still prevent shortcuts.
            bool gatePassed=separation<=12 && Track.PlanDistanceSquared(c.Position,t.points[gateIndex],t.points[gateIndex])<=(Track.BarrierEdge+4)*(Track.BarrierEdge+4) && Math.Abs(c.y-t.points[gateIndex].y)<3 && Math.Abs(lateral)<=Track.BarrierEdge && !c.wrongWay && c.speed>0;
            float currentDistance=t.RoadDistance(c.Position,c.index);
            bool sweptGate=c.gate<Track.Gates && currentDistance>=previousDistance && currentDistance-previousDistance<15 && previousDistance<=t.distance[gateIndex] && currentDistance>=t.distance[gateIndex] && separation<=12 && Math.Abs(lateral)<=Track.BarrierEdge && !c.wrongWay;
            if(c.gate==Track.Gates?crossed:(gatePassed||sweptGate)){
                c.gate++;
                if(c.gate>Track.Gates){
                    c.lap++;
                    float crossingTime=c.elapsed-dt*(1-crossFraction);
                    float lapTime=crossingTime-c.lapStart;
                    c.bestLap=c.bestLap<=0?lapTime:Math.Min(c.bestLap,lapTime);
                    c.lapStart=crossingTime;c.gate=1;
                    if(c.lap>=Laps){c.finished=true;c.finishTime=crossingTime;c.cruiseDistance=currentDistance;c.specialTime=c.jamTime=c.jamImmunity=0;RacingExtras.Reset(c);}
                }
            }
            RacingExtras.Corner(c,t);c.lastFront=Front(c);c.hasFront=true;
            float progress=c.lap*t.Length+t.distance[c.index];
            float gateLimit=c.lap*t.Length+t.distance[Math.Min(Track.Samples,c.gate*Track.Samples/Track.Gates)]+12;
            if(!c.wrongWay&&c.offroad==0&&progress<=gateLimit&&progress>c.furthestDistance){
                float gain=progress-c.furthestDistance;c.furthestDistance=progress;
                if(gain<30){c.distance+=gain;c.gauge=Math.Min(1,c.gauge+gain/(t.Length*1.2f));}
            }
        }
        public static int[] Grid(int count,Random random){var slots=new int[count];for(int i=0;i<count;i++)slots[i]=i;for(int i=count-1;i>0;i--){int j=random.Next(i+1),v=slots[i];slots[i]=slots[j];slots[j]=v;}return slots;}
        public static float Difficulty(int level)=>new[]{.80f,.96f,1.10f,1.25f,1.50f}[Math.Max(1,Math.Min(5,level))-1];
        public static void Cruise(CarState c,Track t,float dt){
            dt=Mathx.Clamp(dt,0,.05f);c.speed=Mathx.Move(c.speed,Math.Min(18,t.TargetSpeed(c.index)*.65f),dt*12);c.cruiseDistance+=c.speed*dt;
            var p=t.At(c.cruiseDistance);int i=t.Nearest(p);float yaw=t.Yaw(i);c.steering=Mathx.Clamp(Mathx.Angle(yaw-c.yaw)*5,-1,1);c.yaw=yaw;c.index=i;c.x=p.x;c.y=p.y;c.z=p.z;c.vx=(float)Math.Sin(yaw)*c.speed;c.vz=(float)Math.Cos(yaw)*c.speed;
        }
        public static float FrontLength(string vehicle)=>vehicle=="bicycle"||vehicle=="stormbike"||vehicle=="aerobike"?1.83f:vehicle=="banana"?2.2f:vehicle=="kebab"||vehicle=="tuktuk"?1.8f:2.2f;
        public static Point Front(CarState c)=>c.Position+new Point((float)Math.Sin(c.yaw),0,(float)Math.Cos(c.yaw))*FrontLength(c.vehicle);
        public static bool CrossedFinish(Point before,Point after,Track t,out float fraction){
            Point axis=t.Tangent(0),a=before-t.points[0],b=after-t.points[0];
            float da=a.x*axis.x+a.z*axis.z,db=b.x*axis.x+b.z*axis.z;
            fraction=1;if(da>=0||db<0||db<=da)return false;
            fraction=-da/(db-da);Point cross=a+(b-a)*fraction;
            return Math.Abs(cross.x*axis.z-cross.z*axis.x)<=Track.GrassEdge&&Math.Abs(cross.y)<3;
        }
        public static void EstimateBots(IList<CarState> cars,Track t){
            float now=0;foreach(var c in cars)now=Math.Max(now,c.elapsed);
            foreach(var c in cars){if(!c.bot||c.finished||c.dnf)continue;
                float position=Math.Min(t.distance[c.index],t.distance[Math.Min(Track.Samples,c.gate*Track.Samples/Track.Gates)]);float left=Math.Max(1,Laps*t.Length-c.lap*t.Length-position);float seconds=0;
                for(float d=0;d<left;d+=10){int i=t.Nearest(t.At(position+d));float speed=Math.Max(8,Math.Min(Vehicles.Get(c.vehicle).maxSpeed*.72f,t.TargetSpeed(i)*Difficulty(c.cpuLevel)));seconds+=Math.Min(10,left-d)/speed;}
                c.finishTime=now+seconds;c.finished=true;c.estimated=true;c.cruiseDistance=t.RoadDistance(c.Position,c.index);c.specialTime=c.jamTime=c.jamImmunity=0;RacingExtras.Reset(c);
            }Rank(cars,t);
        }
        public static bool SettleOnline(IList<CarState> cars,Track t){
            var remaining=new List<CarState>();bool human=false;
            foreach(var c in cars)if(!c.finished&&!c.dnf){remaining.Add(c);if(!c.bot)human=true;}
            if(remaining.Count>0&&!human)EstimateBots(cars,t);
            else if(remaining.Count>0)return false;
            Rank(cars,t);return true;
        }
        public static DriveInput Bot(CarState c,Track t,float aggression=1)
        {
            float look=8+c.speed*.65f;
            Point goal=t.At(t.distance[c.index]+look);
            float delta=Mathx.Angle((float)Math.Atan2(goal.x-c.x,goal.z-c.z)-c.yaw);
            float target=t.TargetSpeed(c.index)*aggression*(c.specialTime>0&&Vehicles.Get(c.vehicle).special=="boost"?1.1f:c.specialTime>0&&Vehicles.Get(c.vehicle).special=="grip"?1.04f:1);
            return new DriveInput{steer=Mathx.Clamp(delta*2.8f,-1,1),throttle=c.speed<target?1:.12f,brake=c.speed>target+1?.7f:0,assist=1};
        }
        public static float Progress(CarState c,Track t)=>c.lap*t.Length+t.distance[c.index];
        public static void Rank(IList<CarState> cars,Track t)
        {
            var sorted=new List<CarState>(cars);
            sorted.Sort((a,b)=>{
                if(a.finished!=b.finished)return a.finished?-1:1;
                if(a.finished)return a.finishTime.CompareTo(b.finishTime);
                if(a.dnf!=b.dnf)return a.dnf?1:-1;
                return Progress(b,t).CompareTo(Progress(a,t));
            });
            for(int i=0;i<sorted.Count;i++)sorted[i].rank=i+1;
        }
    }
}