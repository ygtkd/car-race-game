using System;
using System.Globalization;
namespace CoastRacer.Core {
 [Serializable] public sealed class BadgeDesign {
  public string badge="",face="top",shape="shield",pattern="star",color="gold";public float x=.5f,y=.72f,size=1,angle;
  public static readonly string[] Items={"finish","explorer","collector","winner","garage","veteran","finish50","wins10","wins25","distance10","distance50","distance100","coins100","coins300","specials20","specials100","disrupt1","disrupt10","disrupt50","speed100","speed160","speed220","night1","night10","nightAll","shonan1","shonan10","bikeJam","bikeAero","nightExpert"};
  public static string Base(string s)=>Array.IndexOf(Items,s)>=0?s:"";
  static string Option(string s,string choices,string fallback){return ("|"+choices+"|").Contains("|"+s+"|")?s:fallback;}
  static float Number(string s,float fallback,float min,float max){return float.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out var n)&&!float.IsNaN(n)&&!float.IsInfinity(n)?Mathx.Clamp(n,min,max):fallback;}
  public static BadgeDesign Parse(string value){var d=new BadgeDesign();if(string.IsNullOrEmpty(value)||value.Length>160)return d;var p=value.Split('~');d.badge=Base(p[0]);if(p.Length!=9||d.badge=="")return d;d.face=Option(p[1],"top|left|right|front|rear","top");d.x=Number(p[2],.5f,0,1);d.y=Number(p[3],.72f,0,1);d.size=Number(p[4],1,.75f,1.25f);d.angle=Number(p[5],0,-180,180);d.color=Option(p[6],"gold|silver|red|blue|green|violet","gold");d.shape=Option(p[7],"shield|circle|hexagon","shield");d.pattern=Option(p[8],"star|bolt|checker|wings","star");return d;}
  public static string Normalize(string value){if(string.IsNullOrEmpty(value)||value.Length>500)return "";var parts=value.Split(';');var result=new System.Collections.Generic.List<string>();foreach(var part in parts){if(result.Count==3)break;var v=NormalizeOne(part);if(v!="")result.Add(v);}return string.Join(";",result);}
  static string NormalizeOne(string value){var d=Parse(value);if(d.badge==""||!value.Contains("~"))return d.badge;return string.Join("~",d.badge,d.face,d.x.ToString("0.###",CultureInfo.InvariantCulture),d.y.ToString("0.###",CultureInfo.InvariantCulture),d.size.ToString("0.###",CultureInfo.InvariantCulture),d.angle.ToString("0.###",CultureInfo.InvariantCulture),d.color,d.shape,d.pattern);}
 }
 public static class SpecialPower {
  public const float Gain=5,Duration=5,BoostAcceleration=1+.70f*Gain,CadenceAcceleration=1+.60f*Gain,FeastAcceleration=1+.40f*Gain,GripAcceleration=1+.24f*Gain,BoostSpeed=1+.24f*Gain,Grip=1+.60f*Gain,ShieldMass=1+1.60f*Gain,PulseRadius=30,PulseDrag=4*Gain;
  // A 180% velocity reduction is not meaningful. Keep 20% of velocity so the target can still drive.
  public const float PulseSpeedRetained=.20f;
 }
}
