using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer {
 public sealed partial class RaceGame {
  static void MakeTwoSided(Mesh mesh){
   var source=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var front=mesh.triangles;int count=source.Length;
   var vertices=new Vector3[count*2];var bothNormals=new Vector3[count*2];var bothUV=new Vector2[count*2];var indices=new int[front.Length*2];
   for(int i=0;i<count;i++){vertices[i]=vertices[i+count]=source[i];bothNormals[i]=normals[i];bothNormals[i+count]=-normals[i];if(uv.Length==count)bothUV[i]=bothUV[i+count]=uv[i];}
   for(int i=0;i<front.Length;i+=3){indices[i]=front[i];indices[i+1]=front[i+1];indices[i+2]=front[i+2];int back=i+front.Length;indices[back]=front[i+2]+count;indices[back+1]=front[i+1]+count;indices[back+2]=front[i]+count;}
   mesh.Clear();mesh.vertices=vertices;mesh.normals=bothNormals;mesh.uv=bothUV;mesh.triangles=indices;mesh.RecalculateBounds();
  }
  void CreateFinishBannerAndGround(){
   var parent=new GameObject("Finish and foundation").transform;parent.SetParent(world.transform,false);
   var origin=V(track.points[0]);var rotation=Quaternion.Euler(0,track.Yaw(0)*Mathf.Rad2Deg,0);var right=rotation*Vector3.right;
   for(int side=-1;side<=1;side+=2)Shape("Finish post",PrimitiveType.Cube,origin+right*(side*(Track.GrassEdge+1))+Vector3.up*3.5f,new Vector3(.4f,7,.4f),steel,parent);
   for(int x=0;x<(int)Track.GrassEdge*2;x++)for(int y=0;y<2;y++){
    var tile=Shape("Finish tape",PrimitiveType.Cube,origin+right*(x-Track.GrassEdge+.5f)+Vector3.up*(6.3f+y*.45f),new Vector3(1,.45f,.16f),(x+y)%2==0?white:rubber,parent);tile.transform.rotation=rotation;
   }
   float minX=float.MaxValue,minZ=float.MaxValue,minY=float.MaxValue,maxX=float.MinValue,maxZ=float.MinValue;
   foreach(var p in track.points){minX=Mathf.Min(minX,p.x);minZ=Mathf.Min(minZ,p.z);minY=Mathf.Min(minY,p.y);maxX=Mathf.Max(maxX,p.x);maxZ=Mathf.Max(maxZ,p.z);}
   Shape("Opaque terrain foundation",PrimitiveType.Cube,new Vector3((minX+maxX)*.5f,minY-6,(minZ+maxZ)*.5f),new Vector3(maxX-minX+400,4,maxZ-minZ+400),grass,parent);
  }
 }
}
