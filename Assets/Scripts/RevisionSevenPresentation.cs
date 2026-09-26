using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer {
 public sealed partial class RaceGame {
  [Serializable] sealed class BlenderAsset {public BlenderPart[] parts;}
  [Serializable] sealed class BlenderPart {public string name;public float[] pivot,color,v,n;public int[] t;public float metal,smooth;}
  sealed class ModelPart {public string name;public Vector3 pivot;public Mesh mesh;public Material material;}
  static readonly Dictionary<string,List<ModelPart>> blenderCache=new Dictionary<string,List<ModelPart>>();
  int cpuDifficulty=3;bool nightMode;readonly Vector4[] headPositions=new Vector4[8],headDirections=new Vector4[8];
  Transform BlenderModel(string resource,Transform parent=null){
   if(!blenderCache.TryGetValue(resource,out var parts)){
    var asset=Resources.Load<TextAsset>("Blender/"+resource);if(!asset)throw new InvalidOperationException("Missing Blender asset: "+resource);
    using(var reader=new BinaryReader(new MemoryStream(asset.bytes))){
    if(reader.ReadUInt32()!=0x37425243)throw new InvalidDataException("Unsupported Blender asset: "+resource);
    int count=reader.ReadInt32();parts=new List<ModelPart>();int index=0;
    for(int part=0;part<count;part++){
     var p=new BlenderPart{name=Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadUInt16())),pivot=new float[3],color=new float[4]};
     for(int k=0;k<3;k++)p.pivot[k]=reader.ReadSingle();for(int k=0;k<4;k++)p.color[k]=reader.ReadSingle();p.metal=reader.ReadSingle();p.smooth=reader.ReadSingle();
     int vertices=reader.ReadInt32(),indices=reader.ReadInt32();if(vertices<0||vertices>1000000||indices<0||indices>3000000)throw new InvalidDataException("Invalid Blender mesh size");
     p.v=new float[vertices*3];p.n=new float[vertices*3];p.t=new int[indices];for(int k=0;k<p.v.Length;k++)p.v[k]=reader.ReadSingle();for(int k=0;k<p.n.Length;k++)p.n[k]=reader.ReadSingle();for(int k=0;k<indices;k++)p.t[k]=reader.ReadInt32();
     var v=new Vector3[p.v.Length/3];var n=new Vector3[v.Length];for(int i=0;i<v.Length;i++){v[i]=new Vector3(p.v[i*3],p.v[i*3+1],p.v[i*3+2]);n[i]=new Vector3(p.n[i*3],p.n[i*3+1],p.n[i*3+2]).normalized;}
     var mesh=new Mesh{name=resource+"/"+index,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=v;mesh.triangles=p.t;mesh.normals=n;mesh.RecalculateBounds();
     var mat=Mat("Blender "+resource+index,new Color(p.color[0],p.color[1],p.color[2],1));mat.SetFloat("_Metallic",p.metal);mat.SetFloat("_Smoothness",p.smooth);
     if(resource.StartsWith("vehicle_")&&((p.color[0]>.9f&&p.color[2]>.9f)||(p.color[0]>.65f&&p.color[1]<.04f)))mat.SetFloat("_Emission",1);
     if(resource.StartsWith("course_")&&((p.color[0]>.9f&&p.color[2]>.9f)||(p.color[0]>.9f&&p.color[1]>.5f&&p.color[2]<.15f)))mat.SetFloat("_Emission",.7f);
     if(resource=="course_shonan"&&Mathf.Abs(p.color[0]-.18f)<.001f&&Mathf.Abs(p.color[1]-.29f)<.001f)mat.SetFloat("_Shore",1);
     parts.Add(new ModelPart{name=p.name,pivot=new Vector3(p.pivot[0],p.pivot[1],p.pivot[2]),mesh=mesh,material=mat});index++;
    }}blenderCache[resource]=parts;Resources.UnloadAsset(asset);
   }
   var root=new GameObject(resource).transform;root.SetParent(parent,false);var pivots=new Dictionary<string,Transform>();
   foreach(var part in parts){
    if(!pivots.TryGetValue(part.name,out var pivot)){pivot=new GameObject(part.name).transform;pivot.SetParent(root,false);pivot.localPosition=part.pivot;pivots[part.name]=pivot;}
    var child=new GameObject("Mesh",typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(pivot,false);child.GetComponent<MeshFilter>().sharedMesh=part.mesh;child.GetComponent<MeshRenderer>().sharedMaterial=part.material;
     if(resource.StartsWith("course_")&&part.name!="train"){child.layer=8;child.AddComponent<MeshCollider>().sharedMesh=part.mesh;}
   }
   return root;
  }
  void ApplyTimeOfDay(){Shader.SetGlobalFloat("_CoastNight",showroom?0:nightMode?1:0);if(!showroom){RenderSettings.fogColor=nightMode?new Color(.018f,.026f,.05f):new Color(.67f,.72f,.76f);RenderSettings.fogStartDistance=nightMode?110:250;RenderSettings.fogEndDistance=nightMode?420:800;}}
  void UpdateBlenderCar(Transform model,CarState c,float dt){
   var wheels=model.GetComponent<BlenderWheels>();if(wheels)wheels.Tick(c.steering,c.speed,dt);
   var decoration=model.GetComponentInChildren<DecorationVisibility>();if(decoration)decoration.UpdateView(cam,model.position+model.forward*(rearView?-16:16)+Vector3.up*.5f,cars.IndexOf(c)==0&&!showroom);
   int slot=cars.IndexOf(c);if(slot>=0&&slot<8){var origin=model.position+Vector3.up*.8f+model.forward*1.5f;headPositions[slot]=new Vector4(origin.x,origin.y,origin.z,1);headDirections[slot]=model.forward;Shader.SetGlobalVectorArray("_CoastHeads",headPositions);Shader.SetGlobalVectorArray("_CoastDirections",headDirections);Shader.SetGlobalInt("_CoastLightCount",Math.Min(8,cars.Count));}
  }
 }
 public sealed class BlenderWheels:MonoBehaviour {
  sealed class Wheel {public Transform rotor,steer,mount;public Vector3 rest;public float radius,spin;}
  readonly List<Wheel> wheels=new List<Wheel>();Transform fork;bool initialized;
  public void Tick(float steering,float speed,float dt){
   if(!initialized){
    initialized=true;foreach(var t in GetComponentsInChildren<Transform>()){
     if(t.name=="steer_front"){fork=t;continue;}if(!t.name.StartsWith("wheel_"))continue;
     var mount=new GameObject("Steering pivot").transform;mount.SetParent(t.parent,false);mount.localPosition=t.localPosition;t.SetParent(mount,false);t.localPosition=Vector3.zero;
     float radius=0;foreach(var mesh in t.GetComponentsInChildren<MeshFilter>())radius=Mathf.Max(radius,mesh.sharedMesh.bounds.extents.y,mesh.sharedMesh.bounds.extents.z);
     wheels.Add(new Wheel{rotor=t,mount=mount,rest=mount.localPosition,steer=t.name.StartsWith("wheel_front")?mount:null,radius=Mathf.Max(.1f,radius)});
    }
   }
   if(fork)fork.localRotation=Quaternion.Euler(0,steering*28,0);
   foreach(var wheel in wheels){wheel.spin=(wheel.spin+speed*dt/wheel.radius*Mathf.Rad2Deg)%360;wheel.rotor.localRotation=Quaternion.Euler(wheel.spin,0,0);if(wheel.steer)wheel.steer.localRotation=Quaternion.Euler(0,steering*28,0);}
  }
  public void Ground(Track track,int hint){
   foreach(var wheel in wheels){
    wheel.mount.localPosition=wheel.rest;
    var world=wheel.mount.position;var p=new Point(world.x,world.y-wheel.radius,world.z);
    int road=track.Nearest(p,hint,12);float height=track.At(track.RoadDistance(p,road)).y;
    // Bounded visual suspension follows our road, never the other deck at a crossing.
    float up=Mathf.Max(.6f,transform.up.y);
    float shift=Mathf.Clamp((height+.008f+wheel.radius-world.y)/up,-.22f,.22f);
    wheel.mount.localPosition=wheel.rest+Vector3.up*shift;
   }
  }

 }
 public sealed class DecorationVisibility:MonoBehaviour {
  Renderer[] parts;
  public void UpdateView(Camera camera,Vector3 target,bool protect){
   if(parts==null)parts=GetComponentsInChildren<Renderer>();
   var delta=target-camera.transform.position;var ray=new Ray(camera.transform.position,delta.normalized);
   foreach(var part in parts){bool obstructs=protect&&part.bounds.size.magnitude>.8f&&part.bounds.IntersectRay(ray,out float d)&&d<delta.magnitude;part.forceRenderingOff=obstructs;}
  }
 }
}
