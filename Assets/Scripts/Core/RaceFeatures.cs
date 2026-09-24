using System;
using System.Collections.Generic;
namespace CoastRacer.Core
{
    [Serializable] public sealed class VehicleSpec
    {
        public string id,name,special;
        public float mass,maxSpeed,acceleration,grip;
        public VehicleSpec(string i,string n,string s,float m,float v,float a,float g){id=i;name=n;special=s;mass=m;maxSpeed=v;acceleration=a;grip=g;}
    }
    public static class Vehicles
    {
        public static readonly VehicleSpec[] All={
            new VehicleSpec("apex","APEX GT","boost",1240,54,10.1f,10.0f),
            new VehicleSpec("swift","SWIFT R","grip",1040,51,10.2f,10.2f),
            new VehicleSpec("vortex","VORTEX S","shield",1450,59,9.0f,9.5f),
            new VehicleSpec("atlas","ATLAS X","pulse",1590,52,9.8f,10.0f),
            new VehicleSpec("kebab","KEBAB VAN","feast",1700,51,10.0f,10.1f),
            new VehicleSpec("banana","BANANA BOAT","surf",1150,55,9.6f,10.0f),
            new VehicleSpec("tuktuk","TUK TUK","dash",980,52,10.3f,10.1f),
            new VehicleSpec("bicycle","BICYCLE","cadence",820,50,10.6f,10.5f)
        };
        public static VehicleSpec Get(string id){foreach(var v in All)if(v.id==id)return v;return All[0];}
        public static string Badge(string value)=>BadgeDesign.Normalize(value);
    }
    [Serializable] public sealed class CoinState
    {
        public int id,index;public float x,y,z,respawnAt;public bool active=true;public string collector="";
    }
    // Shared authoritative race rules. Clients may predict their own motion but never confirm pickups/effects.
    public sealed class RaceSession
    {
        public readonly Track track;public readonly CoinState[] coins=new CoinState[3];public float time;
        public RaceSession(Track t)
        {
            track=t;
            for(int n=0;n<3;n++){
                int begin=65+n*180,index=begin;float best=-1;
                for(int i=begin;i<begin+100;i++){
                    float curve=Math.Abs(Mathx.Angle(t.Yaw((i+5)%Track.Samples)-t.Yaw((i-5+Track.Samples)%Track.Samples)));
                    float score=1/(.01f+Math.Abs(curve-.10f));if(score>best){best=score;index=i;}
                }
                float turn=Mathx.Angle(t.Yaw((index+5)%Track.Samples)-t.Yaw((index-5+Track.Samples)%Track.Samples));
                float side=turn>=0?-1:1;Point p=t.points[index],tangent=t.Tangent(index);p+=new Point(tangent.z,0,-tangent.x)*(side*(t.width*.5f-1.2f));
                coins[n]=new CoinState{id=n,index=index,x=p.x,y=p.y,z=p.z};
            }
        }
        public bool Activate(CarState c,IList<CarState> cars)
        {
            if(c.finished||c.dnf||c.gauge<.9999f||c.specialTime>0)return false;
            c.gauge=0;c.specialUses++;c.specialTime=SpecialPower.Duration;
            if(Vehicles.Get(c.vehicle).special=="feast"){c.jamTime=0;c.jamImmunity=5;}
            if(Vehicles.Get(c.vehicle).special=="pulse")foreach(var other in cars){
                if(other==c||other.finished||other.dnf||other.jamImmunity>0||Math.Abs(other.y-c.y)>3)continue;
                float dx=other.x-c.x,dz=other.z-c.z;
                if(dx*dx+dz*dz<SpecialPower.PulseRadius*SpecialPower.PulseRadius && !(Vehicles.Get(other.vehicle).special=="shield"&&other.specialTime>0)){
                    other.speed*=SpecialPower.PulseSpeedRetained;other.vx*=SpecialPower.PulseSpeedRetained;other.vz*=SpecialPower.PulseSpeedRetained;other.jamTime=5;other.jamImmunity=5;
                }
            }
            return true;
        }
        public void Resolve(IList<CarState> cars,float dt)
        {
            time+=dt;Collisions(cars);
            foreach(var coin in coins){
                if(!coin.active&&time+0.00001f>=coin.respawnAt)coin.active=true;
                if(!coin.active)continue;
                CarState winner=null!;float best=4.4f;
                // Closest car wins; stable ID breaks exact ties independently of list order.
                foreach(var c in cars){
                    if(c.finished||c.dnf||!c.connected||Math.Abs(c.y-coin.y)>3)continue;
                    float dx=c.x-coin.x,dz=c.z-coin.z,d=dx*dx+dz*dz;
                    if(d<best||(d==best&&winner!=null&&string.CompareOrdinal(c.id,winner.id)<0)){best=d;winner=c;}
                }
                if(winner!=null){coin.active=false;coin.collector=winner.id;coin.respawnAt=time+2;winner.coins++;winner.gauge=Math.Min(1,winner.gauge+.5f/1.2f);}
            }
        }
        public void UseBotSpecial(CarState c,IList<CarState> cars)
        {
            if(c.gauge<1||c.speed<10)return;string special=Vehicles.Get(c.vehicle).special;
            bool use=(special=="boost"||special=="dash"||special=="cadence"||special=="feast")?track.TargetSpeed(c.index)>35:(special=="grip"||special=="surf")?track.TargetSpeed(c.index)<30:false;
            if(special=="pulse"||special=="shield")foreach(var other in cars){float dx=other.x-c.x,dz=other.z-c.z;if(other!=c&&!other.finished&&dx*dx+dz*dz<SpecialPower.PulseRadius*SpecialPower.PulseRadius)use=true;}
            if(use)Activate(c,cars);
        }
        public DriveInput Bot(CarState c,float aggression=1)
        {
            var input=Simulation.Bot(c,track,aggression);
            foreach(var coin in coins){
                float ahead=(track.distance[coin.index]-track.distance[c.index]+track.Length)%track.Length;
                if(!coin.active||ahead>65||ahead<2)continue;
                float look=7+c.speed*.30f;Point goal=track.At(track.distance[c.index]+look);int index=track.Nearest(goal,c.index);Point tangent=track.Tangent(index);
                Point centre=track.points[coin.index],ct=track.Tangent(coin.index);
                float side=(coin.x-centre.x)*ct.z-(coin.z-centre.z)*ct.x;
                float blend=(float)Math.Exp(-(ahead-look)*(ahead-look)/600);
                goal+=new Point(tangent.z,0,-tangent.x)*(side*blend);
                float delta=Mathx.Angle((float)Math.Atan2(goal.x-c.x,goal.z-c.z)-c.yaw);
                input.steer=Mathx.Clamp(delta*2.8f,-1,1);
                if(c.speed>track.TargetSpeed(c.index)*.76f){input.brake=.7f;input.throttle=0;}
            }
            return input;
        }
        public static void Collisions(IList<CarState> cars)
        {
            for(int i=0;i<cars.Count;i++)for(int j=i+1;j<cars.Count;j++){
                var a=cars[i];var b=cars[j];if(a.recoveryProtection>0||b.recoveryProtection>0||a.finished||b.finished||a.dnf||b.dnf||Math.Abs(a.y-b.y)>2)continue;
                float deepest=0,nx=0,nz=0;
                foreach(float sa in new[]{-1f,1f})foreach(float sb in new[]{-1f,1f}){
                    float dx=b.x+(float)Math.Sin(b.yaw)*sb-a.x-(float)Math.Sin(a.yaw)*sa;
                    float dz=b.z+(float)Math.Cos(b.yaw)*sb-a.z-(float)Math.Cos(a.yaw)*sa;
                    float d=(float)Math.Sqrt(dx*dx+dz*dz),overlap=(a.vehicle=="banana"?.55f:.975f)+(b.vehicle=="banana"?.55f:.975f)-d;
                    if(overlap>deepest){deepest=overlap;nx=d>.001f?dx/d:1;nz=d>.001f?dz/d:0;}
                }
                if(deepest<=0)continue;
                float ma=Vehicles.Get(a.vehicle).mass,mb=Vehicles.Get(b.vehicle).mass;
                if(a.specialTime>0&&Vehicles.Get(a.vehicle).special=="shield")ma*=SpecialPower.ShieldMass;
                if(b.specialTime>0&&Vehicles.Get(b.vehicle).special=="shield")mb*=SpecialPower.ShieldMass;
                float wa=mb/(ma+mb),wb=ma/(ma+mb),correction=Math.Min(deepest+.005f,1.0f);
                a.x-=nx*correction*wa;a.z-=nz*correction*wa;b.x+=nx*correction*wb;b.z+=nz*correction*wb;
                float closing=(a.vx-b.vx)*nx+(a.vz-b.vz)*nz;
                if(closing>0){float impulse=Math.Min(closing*1.05f,25);a.vx-=nx*impulse*wa;a.vz-=nz*impulse*wa;b.vx+=nx*impulse*wb;b.vz+=nz*impulse*wb;
                    a.speed=Mathx.Clamp(a.vx*(float)Math.Sin(a.yaw)+a.vz*(float)Math.Cos(a.yaw),0,Vehicles.Get(a.vehicle).maxSpeed*SpecialPower.BoostSpeed);
                    b.speed=Mathx.Clamp(b.vx*(float)Math.Sin(b.yaw)+b.vz*(float)Math.Cos(b.yaw),0,Vehicles.Get(b.vehicle).maxSpeed*SpecialPower.BoostSpeed);
                }
            }
        }
    }
}