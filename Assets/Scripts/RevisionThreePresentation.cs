using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer {
 public sealed partial class RaceGame {
  int effectQuality=1;
  void UpdateSpecialVisuals(){
   for(int i=0;i<cars.Count;i++){
    var fx=visuals[i].GetComponent<SpecialVisual>();
    if(!fx)fx=visuals[i].gameObject.AddComponent<SpecialVisual>();
    fx.Render(cars[i],phase=="race"&&!showroom,effectQuality);
   }
  }
  // Deterministic low-poly crowns and tapered branches are baked with the scenery.
  void NaturalTree(Transform parent,Vector3 position,int seed,bool pine){
   if(!track.SceneryClear(new Point(position.x,position.y,position.z),6))return;
   int nearest=track.Nearest(new Point(position.x,position.y,position.z));
   var roadPoint=V(track.points[nearest]);
   if(new Vector2(position.x-roadPoint.x,position.z-roadPoint.z).magnitude<Track.BarrierEdge+2)return;
   float variance=Mathf.Abs(Mathf.Sin(seed*12.31f));float height=6+variance*4;
   var bark=Mat("Tree bark",new Color(.24f,.18f,.12f));
   var leaves=Mat("Foliage "+(Mathf.Abs(seed)%3),new Color(.12f+variance*.04f,.23f+variance*.08f,.09f));
   TaperedBranch(parent,position,position+new Vector3(.22f,height*.83f,0),.34f,.08f,bark);
   int count=pine?5:7;
   for(int k=0;k<count;k++){
    float angle=(seed*17+k*137.5f)*Mathf.Deg2Rad;
    float level=pine?.3f+k*.12f:.48f+(k%3)*.15f;
    float radius=pine?(1-level)*4:1.4f+Mathf.Abs(Mathf.Sin(seed+k))*.8f;
    Vector3 start=position+Vector3.up*(height*level);
    Vector3 tip=start+new Vector3(Mathf.Cos(angle)*radius,height*.14f,Mathf.Sin(angle)*radius);
    TaperedBranch(parent,start,tip,.12f,.035f,bark);
    Foliage(parent,pine?start+Vector3.up:tip,new Vector3(pine?radius*1.5f:1.65f,pine?2.5f:1.9f,pine?radius*1.5f:1.65f),seed+k,leaves,pine);
   }
  }
  void TaperedBranch(Transform parent,Vector3 start,Vector3 end,float bottom,float top,Material mat){
   const int sides=7;var vertices=new Vector3[sides*2];var triangles=new int[sides*6];
   var axis=(end-start).normalized;var right=Vector3.Cross(axis,Vector3.forward).normalized;var forward=Vector3.Cross(axis,right);
   for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;var r=right*Mathf.Cos(a)+forward*Mathf.Sin(a);vertices[i]=start+r*bottom;vertices[i+sides]=end+r*top;int j=(i+1)%sides,k=i*6;triangles[k]=i;triangles[k+1]=j;triangles[k+2]=i+sides;triangles[k+3]=j;triangles[k+4]=j+sides;triangles[k+5]=i+sides;}
   SceneryMesh(parent,"Tapered tree branch",vertices,triangles,mat);
  }
  void Foliage(Transform parent,Vector3 center,Vector3 size,int seed,Material mat,bool pine){
   const int sides=9,rings=5;var vertices=new Vector3[sides*rings];var triangles=new int[sides*(rings-1)*6];
   for(int y=0;y<rings;y++)for(int i=0;i<sides;i++){
    float a=i*Mathf.PI*2/sides+(y%2)*.19f;float h=(float)y/(rings-1);float r=pine?1-h:Mathf.Sin(h*Mathf.PI);r=Mathf.Max(.03f,r)*(1+.17f*Mathf.Sin(seed+i*2.7f+y*3.1f));
    vertices[y*sides+i]=center+Vector3.Scale(new Vector3(Mathf.Cos(a)*r,(h-.4f)*2,Mathf.Sin(a)*r),size);
    if(y==rings-1)continue;int v=y*sides+i,j=y*sides+(i+1)%sides,k=(y*sides+i)*6;triangles[k]=v;triangles[k+1]=j;triangles[k+2]=v+sides;triangles[k+3]=j;triangles[k+4]=j+sides;triangles[k+5]=v+sides;
   }
   SceneryMesh(parent,"Irregular foliage",vertices,triangles,mat);
  }
  void SceneryMesh(Transform parent,string name,Vector3[] vertices,int[] triangles,Material mat){
   var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);var mesh=new Mesh{name="Coast tree mesh",vertices=vertices,triangles=triangles};mesh.RecalculateNormals();obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
  }
 }
 // Fixed renderer pool per car; snapshots drive effects, never local collision guesses.
 public sealed class SpecialVisual:MonoBehaviour {
  LineRenderer[] lines;Material material;int uses=-1;Vector3 pulseOrigin;float pulseAge=10;
  void Init(){
   material=new Material(Resources.Load<Shader>("CoastEffect"));lines=new LineRenderer[8];
   for(int i=0;i<lines.Length;i++){var obj=new GameObject("Special ribbon "+i);obj.transform.SetParent(transform,false);var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=25;line.numCapVertices=0;line.widthMultiplier=.09f;line.enabled=false;lines[i]=line;}
  }
  public void Render(CarState car,bool racing,int quality){
   if(lines==null)Init();foreach(var line in lines)line.enabled=false;
   if(!racing||car.finished||car.dnf||!car.connected){uses=car.specialUses;pulseAge=10;return;}
   if(car.specialUses!=uses){if(uses>=0&&car.specialUses>uses){pulseOrigin=new Vector3(car.x,car.y+.25f,car.z);pulseAge=0;}uses=car.specialUses;}
   pulseAge=5-car.specialTime;
   bool jam=car.jamTime>0;string type=Vehicles.Get(car.vehicle).special;
   if(car.specialTime<=0&&!jam)return;
   Color color=jam?new Color(1,.25f,.25f):type=="boost"?new Color(.25f,.7f,1):type=="grip"?new Color(.4f,1,.25f):type=="shield"?new Color(.6f,.45f,1):type=="pulse"?new Color(1,.35f,.8f):type=="feast"?new Color(1,.55f,.1f):type=="surf"?new Color(.15f,1,.9f):type=="dash"?new Color(1,.85f,.15f):Color.white;
   material.color=color;int count=quality==0?4:8;float age=car.elapsed;
   for(int n=0;n<count;n++){

    var line=lines[n];line.enabled=true;line.widthMultiplier=jam?.09f:.12f;
    for(int k=0;k<25;k++){
     float f=k/24f,a=f*Mathf.PI*2;Vector3 point;
     if(jam)point=new Vector3(Mathf.Cos(a)*1.5f,1.2f+n*.13f,Mathf.Sin(a)*1.5f);
     else if(type=="pulse"){line.SetPosition(k,transform.position+Vector3.up*.25f+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*((pulseAge+n*.12f)%1.2f)*20);continue;}
     else if(type=="shield")point=n%2==0?new Vector3(Mathf.Cos(a)*1.5f,1+Mathf.Sin(a)*1.6f,n*.1f):new Vector3(Mathf.Cos(a)*1.5f,1+(n-3)*.25f,Mathf.Sin(a)*2.6f);
     else if(type=="grip")point=new Vector3((n%2==0?-1:1)*1.1f+Mathf.Cos(a)*.24f,.18f,Mathf.Sin(a)*2.4f);
     else if(type=="surf")point=new Vector3(Mathf.Sin(a+age*4)*(.8f+n*.12f),.25f+Mathf.Cos(a+age*4)*.15f,2-f*7);
     else if(type=="feast")point=new Vector3(Mathf.Cos(a+age)*(.8f+n*.06f),.5f+f*2.5f,Mathf.Sin(a+age)*1.4f-1);
     else if(type=="cadence")point=new Vector3(Mathf.Cos(a+age*5)*(.8f+n*.05f),.5f+Mathf.Sin(a+age*5)*.7f,-1-f*5);
     else point=new Vector3((n%2==0?-1:1)*(.5f+n*.08f),.3f+Mathf.Sin(f*9-age*12)*.08f,-1.8f-f*(type=="dash"?5:7));
     line.SetPosition(k,transform.TransformPoint(point));
    }
   }
  }
  void OnDestroy(){if(material)Destroy(material);}
 }
}