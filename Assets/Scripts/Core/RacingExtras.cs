using System;
using System.Collections.Generic;
namespace CoastRacer.Core {
 // All timers are simulation seconds and all awards are authoritative in RaceSession.
 public static class RacingExtras {
  public static void Reset(CarState c){c.abilityImmunity=c.guardBoost=c.guardFx=c.draftCharge=c.draftPower=c.cornerFlash=0;c.guardTriggered=false;c.cornerActive=-1;c.cornerPrevious=-1;c.cornerClean=false;}
  public static void Tick(CarState c,float dt){c.abilityImmunity=Math.Max(0,c.abilityImmunity-dt);c.guardBoost=Math.Max(0,c.guardBoost-dt);c.guardFx=Math.Max(0,c.guardFx-dt);c.cornerFlash=Math.Max(0,c.cornerFlash-dt);}
  public static void Block(CarState c){
   if(c.abilityImmunity<=0||c.guardTriggered)return;
   c.guardTriggered=true;c.guardBlocks++;c.guardBoost=5;c.guardFx=Math.Max(5,c.abilityImmunity);
  }
  public static void Drafting(IList<CarState> cars,Track t,float dt){
   foreach(var c in cars){
    if(c.finished||c.dnf||!c.connected||c.recoveryProtection>0){c.draftPower=c.draftCharge=0;continue;}
    bool valid=false;
    if(c.speed>=60/3.6f&&!c.wrongWay&&c.offroad==0)foreach(var other in cars){
     if(other==c||other.finished||other.dnf||!other.connected||other.wrongWay||other.offroad>0||other.speed<10||Math.Abs(other.y-c.y)>2)continue;
     float ahead=(t.RoadDistance(other.Position,other.index)-t.RoadDistance(c.Position,c.index)+t.Length)%t.Length;
     if(ahead<3||ahead>27||Math.Abs(Mathx.Angle(other.yaw-c.yaw))>Mathx.Pi/12)continue;
     float dx=c.x-other.x,dz=c.z-other.z,back=-(dx*(float)Math.Sin(other.yaw)+dz*(float)Math.Cos(other.yaw));
     float gap=back-Simulation.FrontLength(c.vehicle)-Simulation.FrontLength(other.vehicle);
     float side=dx*(float)Math.Cos(other.yaw)-dz*(float)Math.Sin(other.yaw);
     if(gap>=3&&gap<=20&&Math.Abs(side)<=1.5f){valid=true;break;}
    }
    c.draftCharge=valid?Math.Min(1,c.draftCharge+dt):0;
    c.draftPower=Mathx.Move(c.draftPower,c.draftCharge>=.999f?1:0,dt);
   }
  }
  public static int[] BuildCorners(Track t){
   var active=new bool[Track.Samples];
   for(int i=0;i<active.Length;i++){var a=(i+Track.Samples-4)%Track.Samples;var b=(i+4)%Track.Samples;float metres=(t.distance[b]-t.distance[a]+t.Length)%t.Length;active[i]=Math.Abs(Mathx.Angle(t.Yaw(b)-t.Yaw(a)))/Math.Max(1,metres)>.0035f;}
   // Fill short gaps, so a continuous bend has a single entry and exit.
   for(int i=0;i<active.Length;i++)if(active[i])for(int d=2;d<=7&&i+d<active.Length;d++)if(active[i+d]){for(int k=1;k<d;k++)active[i+k]=true;break;}
   var ids=new int[active.Length];int id=-1;bool before=false;
   for(int i=0;i<ids.Length;i++){if(active[i]&&!before)id++;ids[i]=active[i]?Math.Min(30,id):-1;before=active[i];}return ids;
  }
  public static void Corner(CarState c,Track t){
   if(c.finished||c.dnf)return;
   if(c.cornerLap!=c.lap){c.cornerLap=c.lap;c.cornerMask=0;c.cornerBudget=0;c.cornerActive=-1;}
   int next=t.cornerIds[c.index];
   int delta=c.cornerPrevious<0?0:(c.index-c.cornerPrevious+Track.Samples)%Track.Samples;
   if(c.cornerActive>=0)c.cornerClean&=delta<8&&c.offroad==0&&!c.wrongWay;
   if(c.cornerActive!=next){
    if(c.cornerActive>=0&&next<0&&c.cornerClean&&c.cornerSamples>10&&c.cornerSpeedSum/c.cornerSamples>=.6f&&(c.cornerMask&(1<<c.cornerActive))==0){
     float award=Math.Min(.03f,Math.Max(0,.15f-c.cornerBudget));c.gauge=Math.Min(1,c.gauge+award);c.cornerBudget+=award;c.cornerMask|=1<<c.cornerActive;if(award>0){c.cornerBonuses++;c.cornerFlash=.8f;}
    }
    c.cornerActive=next;c.cornerClean=next>=0&&c.recoveryProtection<=0&&c.cornerPrevious>=0&&delta<8&&t.cornerIds[(c.index+Track.Samples-1)%Track.Samples]!=next;c.cornerSpeedSum=0;c.cornerSamples=0;
   }
   if(next>=0){c.cornerClean&=c.offroad==0&&!c.wrongWay&&c.recoveryProtection<=0;c.cornerClean&=c.speed>=Math.Min(Vehicles.Get(c.vehicle).maxSpeed,t.TargetSpeed(c.index))*.35f;c.cornerSamples++;c.cornerSpeedSum+=c.speed/Math.Min(Vehicles.Get(c.vehicle).maxSpeed,t.TargetSpeed(c.index));}
   c.cornerPrevious=c.index;
  }
 }
}