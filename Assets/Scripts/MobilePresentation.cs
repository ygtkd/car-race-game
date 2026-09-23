using UnityEngine;
namespace CoastRacer {
 public sealed partial class RaceGame {
  void NoveltyBody(Transform car,string vehicle,string badge,Material body){
   bool bike=vehicle=="bicycle",boat=vehicle=="banana",van=vehicle=="kebab";
   var yellow=Mat("CarBanana",new Color(.95f,.73f,.08f));
   if(boat){
    for(int side=0;side<=0;side++){Shape("Inflatable hull",PrimitiveType.Capsule,new Vector3(side*.55f,.65f,0),new Vector3(.65f,2.1f,.65f),yellow,car).transform.localRotation=Quaternion.Euler(90,0,0);Shape("Raised bow",PrimitiveType.Sphere,new Vector3(side*.55f,.98f,1.8f),new Vector3(.65f,.85f,.8f),yellow,car);}
    for(int i=0;i<3;i++){Shape("Saddle",PrimitiveType.Cube,new Vector3(0,.94f,-1+i),new Vector3(.7f,.17f,.45f),rubber,car);Shape("Handle",PrimitiveType.Cube,new Vector3(0,1.15f,-.7f+i),new Vector3(.7f,.08f,.08f),steel,car);}
   }else if(bike){
    Beam(car,new Vector3(0,.6f,-1.3f),new Vector3(0,1.3f,0),.09f,body);Beam(car,new Vector3(0,1.3f,0),new Vector3(0,.6f,1.3f),.09f,body);Beam(car,new Vector3(0,.6f,-1.3f),new Vector3(0,.6f,.1f),.09f,body);Beam(car,new Vector3(0,.6f,.1f),new Vector3(0,1.3f,0),.09f,body);Beam(car,new Vector3(0,1.3f,0),new Vector3(0,1.5f,.95f),.09f,body);
    Shape("Bicycle saddle",PrimitiveType.Cube,new Vector3(0,1.48f,-.1f),new Vector3(.45f,.13f,.6f),rubber,car);Shape("Handlebar",PrimitiveType.Cube,new Vector3(0,1.6f,1),new Vector3(.95f,.09f,.09f),steel,car);
   }else{
    Shape("Chassis",PrimitiveType.Cube,new Vector3(0,.55f,0),new Vector3(1.7f,.4f,3.5f),body,car);
    Shape("Cabin",PrimitiveType.Cube,new Vector3(0,1.25f,.95f),new Vector3(1.6f,1.1f,1.1f),body,car);
    Shape("Windshield",PrimitiveType.Cube,new Vector3(0,1.45f,1.52f),new Vector3(1.4f,.6f,.03f),glass,car);
    Shape("Roof",PrimitiveType.Cube,new Vector3(0,2,0),new Vector3(1.9f,.16f,3.6f),van?white:yellow,car);
    if(van){Shape("Kitchen",PrimitiveType.Cube,new Vector3(0,1.2f,-.8f),new Vector3(1.6f,1.25f,1.9f),white,car);Shape("Serving window",PrimitiveType.Cube,new Vector3(-.82f,1.5f,-.65f),new Vector3(.04f,.6f,1.3f),glass,car);Shape("Awning",PrimitiveType.Cube,new Vector3(-1,1.9f,-.7f),new Vector3(.65f,.08f,1.7f),red,car);Shape("Kebab spit",PrimitiveType.Capsule,new Vector3(0,2.65f,-.6f),new Vector3(.6f,.6f,.6f),Mat("Kebab",new Color(.49f,.23f,.09f)),car);}
    else{for(int side=-1;side<=1;side+=2)Shape("Roof pillar",PrimitiveType.Cube,new Vector3(side*.8f,1.35f,-1.4f),new Vector3(.09f,1.25f,.09f),steel,car);Shape("Bench",PrimitiveType.Cube,new Vector3(0,.98f,-.8f),new Vector3(1.45f,.25f,.8f),rubber,car);}
   }
   if(!boat)foreach(float z in new[]{-1.25f,1.25f})foreach(int side in new[]{-1,1}){
    if(bike&&side==1)continue;float x=bike?0:!van&&z>0?0:side*.9f;if(!bike&&!van&&z>0&&side==1)continue;
    var wheel=Shape("Tyre",PrimitiveType.Cylinder,new Vector3(x,bike?.6f:.38f,z),new Vector3(bike?1.15f:.72f,bike?.055f:.13f,bike?1.15f:.72f),rubber,car);wheel.transform.localRotation=Quaternion.Euler(0,0,90);
    var hub=Shape("Hub",PrimitiveType.Cylinder,new Vector3(x,bike?.6f:.38f,z),new Vector3(bike?.95f:.45f,bike?.06f:.135f,bike?.95f:.45f),steel,car);hub.transform.localRotation=Quaternion.Euler(0,0,90);
   }
   if(!string.IsNullOrEmpty(badge))Shape("Achievement badge",PrimitiveType.Sphere,new Vector3(0,boat?1.25f:bike?1.7f:2.15f,.9f),new Vector3(.25f,.08f,.25f),yellow,car);
  }
  void Beam(Transform parent,Vector3 from,Vector3 to,float width,Material material){var item=Shape("Frame",PrimitiveType.Cube,(from+to)*.5f,new Vector3(width,width,Vector3.Distance(from,to)),material,parent);item.transform.localRotation=Quaternion.LookRotation(to-from);}
  void CreateBackdrop(){var sky=new Material(Resources.Load<Shader>("CoastBackdrop"));sky.SetFloat("_Mountain",track.id=="ridge"?1:0);if(RenderSettings.skybox!=null)Destroy(RenderSettings.skybox);RenderSettings.skybox=sky;cam.clearFlags=CameraClearFlags.Skybox;}
  void ExtraScenery(Transform scenery){
   var rock=Mat("Rock",new Color(.30f,.32f,.29f));
   for(int i=12;i<CoastRacer.Core.Track.Samples;i+=24){var p=track.points[i];var t=track.Tangent(i);var right=new Vector3(t.z,0,-t.x);var center=V(p);
    if(track.id=="ridge"){
     for(int side=-1;side<=1;side+=2){Shape("Mountain rock",PrimitiveType.Sphere,center+right*side*42+Vector3.up*3,new Vector3(12,10+(i%5),16),rock,scenery);for(int n=0;n<3;n++){var pos=center+right*side*(23+n*6)+new Vector3(n*2,0,-n*2);Shape("Pine trunk",PrimitiveType.Cylinder,pos+Vector3.up*2,new Vector3(.45f,2,.45f),rubber,scenery);Shape("Pine foliage",PrimitiveType.Capsule,pos+Vector3.up*6,new Vector3(3.5f,5,3.5f),grass,scenery);}}
    }else{
     for(int row=0;row<4;row++){var seat=Shape("Grandstand tier",PrimitiveType.Cube,center+right*(24+row*2)+Vector3.up*(1+row),new Vector3(2,1,12),row%2==0?white:red,scenery);seat.transform.rotation=Quaternion.Euler(0,track.Yaw(i)*Mathf.Rad2Deg,0);}
     Shape("Light mast",PrimitiveType.Cylinder,center-right*21+Vector3.up*5,new Vector3(.15f,5,.15f),steel,scenery);Shape("Floodlight",PrimitiveType.Cube,center-right*21+Vector3.up*10,new Vector3(2,.45f,.45f),white,scenery);
    }
   }
  }
 }
}