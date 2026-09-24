using System;
using System.Collections.Generic;
using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer {
 public sealed partial class RaceGame {
  float badgeViewHeight=3.4f;
  void SetBadgeView(string face){showroomAngle=face=="left"?-90:face=="right"?90:face=="rear"?180:0;badgeViewHeight=face=="top"?8:1.8f;}
  void BuildCustomBadge(Transform car,string value){
   var d=BadgeDesign.Parse(value);if(d.badge=="")return;
   var filters=car.GetComponentsInChildren<MeshFilter>();var points=new List<Vector3[]>();var triangles=new List<int[]>();var bounds=new Bounds();bool first=true;
   foreach(var mf in filters){if(mf.name.Contains("shadow"))continue;var mesh=mf.sharedMesh;if(!mesh||!mesh.isReadable)continue;var v=mesh.vertices;for(int i=0;i<v.Length;i++){v[i]=car.InverseTransformPoint(mf.transform.TransformPoint(v[i]));if(first){bounds=new Bounds(v[i],Vector3.zero);first=false;}else bounds.Encapsulate(v[i]);}points.Add(v);triangles.Add(mesh.triangles);}
   if(first)return;
   Vector3 normal=d.face=="left"?Vector3.left:d.face=="right"?Vector3.right:d.face=="front"?Vector3.forward:d.face=="rear"?Vector3.back:Vector3.up;
   Vector3 u=d.face=="top"||d.face=="front"||d.face=="rear"?Vector3.right:Vector3.forward,vaxis=d.face=="top"?Vector3.forward:Vector3.up;
   float wu=Mathf.Abs(Vector3.Dot(bounds.size,u)),wv=Mathf.Abs(Vector3.Dot(bounds.size,vaxis));Vector3 origin=bounds.center+u*((d.x-.5f)*wu)+vaxis*((d.y-.5f)*wv)+normal*(bounds.size.magnitude+2),dir=-normal;
   float best=float.MaxValue,nearest=float.MaxValue;Vector3 hit=bounds.center,fallback=hit,surfaceNormal=normal;
   for(int m=0;m<points.Count;m++){var vertices=points[m];foreach(var q in vertices){var delta=q-origin;float error=delta.sqrMagnitude-Mathf.Pow(Vector3.Dot(delta,dir),2);if(error<nearest){nearest=error;fallback=q;}}
    var indices=triangles[m];for(int t=0;t<indices.Length;t+=3){var a=vertices[indices[t]];var e1=vertices[indices[t+1]]-a;var e2=vertices[indices[t+2]]-a;var h=Vector3.Cross(dir,e2);float det=Vector3.Dot(e1,h);if(Mathf.Abs(det)<.000001f)continue;float inv=1/det;var delta=origin-a;float b=Vector3.Dot(delta,h)*inv;if(b<0||b>1)continue;var q=Vector3.Cross(delta,e1);float c=Vector3.Dot(dir,q)*inv;if(c<0||b+c>1)continue;float distance=Vector3.Dot(e2,q)*inv;if(distance>0&&distance<best){best=distance;hit=origin+dir*distance;surfaceNormal=Vector3.Cross(e1,e2).normalized;if(Vector3.Dot(surfaceNormal,normal)<0)surfaceNormal=-surfaceNormal;}}
   }
   if(best==float.MaxValue)hit=fallback;
   var root=new GameObject("Achievement badge").transform;root.SetParent(car,false);root.localPosition=hit+surfaceNormal*.035f;var up=d.face=="top"?Vector3.forward:Vector3.up;if(Mathf.Abs(Vector3.Dot(surfaceNormal,up))>.95f)up=Vector3.right;root.localRotation=Quaternion.LookRotation(surfaceNormal,up)*Quaternion.Euler(0,0,d.angle);root.localScale=Vector3.one*(.55f*d.size);root.gameObject.AddComponent<BadgeMeshCleanup>();
   Color color=d.color=="red"?new Color(.85f,.13f,.13f):d.color=="blue"?new Color(.1f,.5f,.95f):d.color=="green"?new Color(.16f,.7f,.38f):d.color=="violet"?new Color(.65f,.3f,.9f):d.color=="silver"?new Color(.7f,.8f,.9f):new Color(.96f,.65f,.12f);
   var fill=Mat("Badge color "+d.color,color);var border=Mat("Badge border",new Color(.96f,.97f,1));var ink=Mat("Badge ink",new Color(.045f,.065f,.1f));
   var outline=new List<Vector2>();if(d.shape=="shield")outline.AddRange(new[]{new Vector2(-.5f,.5f),new Vector2(-.5f,-.15f),new Vector2(0,-.6f),new Vector2(.5f,-.15f),new Vector2(.5f,.5f)});else{int n=d.shape=="hexagon"?6:32;for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;outline.Add(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.55f);}}
   BadgePolygon(root,outline.ToArray(),1,0,border);BadgePolygon(root,outline.ToArray(),.84f,.009f,fill);
   if(d.pattern=="checker"){for(int x=0;x<4;x++)for(int y=0;y<4;y++)if((x+y)%2==0)BadgePolygon(root,new[]{new Vector2(x*.14f-.28f,y*.14f-.20f),new Vector2(x*.14f-.14f,y*.14f-.20f),new Vector2(x*.14f-.14f,y*.14f-.06f),new Vector2(x*.14f-.28f,y*.14f-.06f)},1,.019f,ink);}
   else if(d.pattern=="bolt")BadgePolygon(root,new[]{new Vector2(.05f,.35f),new Vector2(-.23f,-.02f),new Vector2(-.01f,-.02f),new Vector2(-.09f,-.30f),new Vector2(.24f,.08f),new Vector2(.02f,.08f)},1,.019f,ink);
   else if(d.pattern=="wings"){foreach(int side in new[]{-1,1})for(int n=0;n<3;n++)BadgePolygon(root,new[]{new Vector2(side*.02f,.13f-n*.09f),new Vector2(side*(.39f-n*.06f),.24f-n*.08f),new Vector2(side*(.30f-n*.05f),.04f-n*.08f)},1,.019f,ink);}
   else{var star=new Vector2[10];for(int n=0;n<10;n++){float a=Mathf.PI/2+n*Mathf.PI/5;star[n]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(n%2==0?.32f:.14f);}BadgePolygon(root,star,1,.019f,ink);}
   int count=Array.IndexOf(new[]{"finish","explorer","collector","winner","garage","veteran"},d.badge)+1;for(int n=0;n<count;n++){float x=(n-(count-1)*.5f)*.075f;BadgePolygon(root,new[]{new Vector2(x-.02f,-.38f),new Vector2(x+.02f,-.38f),new Vector2(x+.02f,-.32f),new Vector2(x-.02f,-.32f)},1,.022f,border);}
  }
  void BadgePolygon(Transform parent,Vector2[] polygon,float scale,float z,Material mat){
   var vertices=new Vector3[polygon.Length+1];Vector2 center=Vector2.zero;foreach(var p in polygon)center+=p;center/=polygon.Length;vertices[0]=new Vector3(center.x*scale,center.y*scale,z);for(int i=0;i<polygon.Length;i++)vertices[i+1]=new Vector3(polygon[i].x*scale,polygon[i].y*scale,z);
   // Per-triangle copies make every motif visible from either side without normal cancellation.
   var indices=new int[polygon.Length*6];for(int i=0;i<polygon.Length;i++){int next=(i+1)%polygon.Length+1,k=i*6;indices[k]=0;indices[k+1]=i+1;indices[k+2]=next;indices[k+3]=0;indices[k+4]=next;indices[k+5]=i+1;}
   var obj=new GameObject("Badge design",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);var mesh=new Mesh{name="Badge mesh",vertices=vertices,triangles=indices};var normals=new Vector3[vertices.Length];for(int i=0;i<normals.Length;i++)normals[i]=Vector3.forward;mesh.normals=normals;obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=mat;
  }
  void MountainRock(Transform parent,Vector3 position,int seed){
   if(!track.SceneryClear(new Point(position.x,position.y,position.z),10))return;
   var random=new System.Random(seed);var ring=new Vector3[16];for(int i=0;i<8;i++){float a=i*Mathf.PI/4,r=.75f+(float)random.NextDouble()*.3f;ring[i]=position+new Vector3(Mathf.Cos(a)*7*r,-2,Mathf.Sin(a)*6*r);ring[i+8]=position+new Vector3(Mathf.Cos(a)*5*r,3+(float)random.NextDouble()*4,Mathf.Sin(a)*4*r);}
   var vertices=new List<Vector3>();var tris=new List<int>();Action<Vector3,Vector3,Vector3> face=(a,b,c)=>{int n=vertices.Count;vertices.AddRange(new[]{a,b,c});tris.AddRange(new[]{n,n+1,n+2});};var top=position+new Vector3(-1,7.5f,.5f);
   for(int i=0;i<8;i++){int j=(i+1)%8;face(ring[i],ring[i+8],ring[j]);face(ring[j],ring[i+8],ring[j+8]);face(ring[i+8],top,ring[j+8]);}
   var obj=new GameObject("Angular mountain rock",typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);var mesh=new Mesh{name="Coast rock mesh",vertices=vertices.ToArray(),triangles=tris.ToArray()};mesh.RecalculateNormals();MakeTwoSided(mesh);obj.GetComponent<MeshFilter>().sharedMesh=mesh;var material=Mat("Mountain granite",new Color(.36f,.32f,.27f));material.SetFloat("_Smoothness",.015f);material.SetFloat("_Grain",.35f);obj.GetComponent<MeshRenderer>().sharedMaterial=material;
  }
 }
 public sealed class BadgeMeshCleanup:MonoBehaviour {void OnDestroy(){foreach(var filter in GetComponentsInChildren<MeshFilter>(true))if(filter.sharedMesh&&filter.sharedMesh.name=="Badge mesh")Destroy(filter.sharedMesh);}}
}
