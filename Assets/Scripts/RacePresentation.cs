using System;
using System.Collections.Generic;
using UnityEngine;
using CoastRacer.Core;
namespace CoastRacer
{
    public sealed partial class RaceGame
    {
        [Serializable] public sealed class NameLabel {public string id,name,effect;public float x,y;public bool visible,self;}
        RaceSession session;CoinState[] displayedCoins;string raceId="";
        readonly List<Transform> coinVisuals=new List<Transform>();
        bool showroom;float showroomAngle=35;GameObject showroomFloor;
        void LeaveShowroom(){showroom=false;if(showroomFloor)Destroy(showroomFloor);cam.rect=new Rect(0,0,1,1);cam.backgroundColor=new Color(.67f,.72f,.76f);RenderSettings.fog=true;}
        void ShowCar(string vehicle,string badge)
        {
            vehicle=Vehicles.Get(vehicle).id;badge=Vehicles.Badge(badge);
            if(showroom&&cars.Count==1&&visuals.Count==1&&cars[0].vehicle==vehicle){
                cars[0].badge=badge;var old=visuals[0].Find("Achievement badge");if(old){old.gameObject.SetActive(false);Destroy(old.gameObject);}
                BuildCustomBadge(visuals[0],badge);visuals[0].name=vehicle+"/"+badge;Emit(false);return;
            }
            foreach(var v in visuals)if(v)Destroy(v.gameObject);visuals.Clear();cars.Clear();
            if(showroomFloor)Destroy(showroomFloor);world.SetActive(false);online=false;paused=false;phase="menu";showroom=true;
            cars.Add(new CarState{vehicle=Vehicles.Get(vehicle).id,badge=Vehicles.Badge(badge)});visuals.Add(BuildCar(0,cars[0].vehicle,cars[0].badge));
            showroomFloor=new GameObject("Showroom");
            Shape("Display plinth",PrimitiveType.Cylinder,new Vector3(0,-.12f,0),new Vector3(7,.12f,7),Mat("StudioFloor",new Color(.075f,.08f,.09f)),showroomFloor.transform);
            cam.rect=new Rect(0,0,1,1);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.065f,.073f,.086f);RenderSettings.fog=false;UpdateShowroom(0);Emit(true);
        }
        void UpdateShowroom(float dt)
        {
            ApplyTimeOfDay();
            float a=showroomAngle*Mathf.Deg2Rad;cam.transform.position=new Vector3(Mathf.Sin(a)*8.7f,badgeViewHeight,Mathf.Cos(a)*8.7f);cam.transform.LookAt(new Vector3(0,.65f,0));Vector3 offset=cam.transform.right*2.2f;cam.transform.position-=offset;cam.transform.LookAt(new Vector3(0,.65f,0)-offset);cam.fieldOfView=38;
        }
        void RefreshVisuals()
        {
            for(int i=0;i<cars.Count;i++){
                string stamp=Vehicles.Get(cars[i].vehicle).id+"/"+cars[i].badge;
                if(i>=visuals.Count)visuals.Add(BuildCar(i,cars[i].vehicle,cars[i].badge));
                else if(visuals[i].name!=stamp){Destroy(visuals[i].gameObject);visuals[i]=BuildCar(i,cars[i].vehicle,cars[i].badge);}
            }
        }
        void CustomizeCar(Transform car,string vehicle,string badge,Material body)
        {
            var dark=Mat("Intake",new Color(.018f,.021f,.025f));var chrome=Mat("Steel",new Color(.42f,.45f,.48f));
            Shape("Front intake",PrimitiveType.Cube,new Vector3(0,.47f,2.18f),new Vector3(1.3f,.20f,.025f),dark,car);
            for(int k=-3;k<=3;k++)Shape("Intake grille",PrimitiveType.Cube,new Vector3(k*.16f,.47f,2.20f),new Vector3(.025f,.18f,.02f),chrome,car);
            foreach(int side in new[]{-1,1}){
                Shape("Side skirt",PrimitiveType.Cube,new Vector3(side*.98f,.32f,0),new Vector3(.08f,.12f,2.7f),dark,car);
                Shape("Door handle",PrimitiveType.Cube,new Vector3(side*.97f,.85f,-.35f),new Vector3(.035f,.045f,.2f),chrome,car);
                for(int k=0;k<3;k++)Shape("Fender vent",PrimitiveType.Cube,new Vector3(side*.96f,.69f,.65f-k*.1f),new Vector3(.025f,.14f,.035f),dark,car);
                foreach(float z in new[]{-1.3f,1.3f}){
                    var centre=Shape("Wheel centre",PrimitiveType.Cylinder,new Vector3(side*1.12f,.39f,z),new Vector3(.13f,.018f,.13f),dark,car);centre.transform.localRotation=Quaternion.Euler(0,0,90);
                    for(int spoke=0;spoke<5;spoke++){
                        float angle=spoke*Mathf.PI*2/5;var item=Shape("Wheel spoke",PrimitiveType.Cube,new Vector3(side*1.125f,.39f+Mathf.Sin(angle)*.12f,z+Mathf.Cos(angle)*.12f),new Vector3(.035f,.045f,.25f),chrome,car);
                        item.transform.localRotation=Quaternion.Euler(-angle*Mathf.Rad2Deg,0,0);
                    }
                }
                var exhaust=Shape("Exhaust",PrimitiveType.Cylinder,new Vector3(side*.65f,.34f,-2.24f),new Vector3(.14f,.14f,.14f),chrome,car);exhaust.transform.localRotation=Quaternion.Euler(90,0,0);
            }
            // Each family has its own proportions and silhouette, not merely another paint colour.
            if(vehicle=="swift"){
                car.localScale=new Vector3(.94f,1.06f,.89f);
            }else if(vehicle=="vortex"){
                car.localScale=new Vector3(1.04f,.88f,1.08f);
                Shape("Long aero nose",PrimitiveType.Cube,new Vector3(0,.48f,2.18f),new Vector3(1.7f,.18f,.38f),body,car);
                Shape("Aero fin",PrimitiveType.Cube,new Vector3(0,1.20f,-1.3f),new Vector3(.045f,.4f,1.4f),dark,car);
            }else if(vehicle=="atlas"){
                car.localScale=new Vector3(1.04f,1.12f,1.03f);
                Shape("Wide bumper",PrimitiveType.Cube,new Vector3(0,.43f,2.14f),new Vector3(2.02f,.22f,.18f),body,car);
            }
            BuildCustomBadge(car,badge);
            Shape("Contact shadow",PrimitiveType.Sphere,new Vector3(0,.015f,0),new Vector3(2.2f,.025f,4.6f),Mat("Shadow",new Color(.075f,.078f,.075f)),car);
        }
        void CreateCoins()
        {
            coinVisuals.Clear();
            foreach(var coin in session.coins){
                var obj=BlenderModel("prop_coin",world.transform);obj.position=new Vector3(coin.x,coin.y+1.2f,coin.z);coinVisuals.Add(obj);
            }
        }
        void UpdateCoins()
        {
            if(displayedCoins==null)return;
            for(int i=0;i<Math.Min(coinVisuals.Count,displayedCoins.Length);i++){
                if(!coinVisuals[i])continue;coinVisuals[i].gameObject.SetActive(displayedCoins[i].active);
                coinVisuals[i].rotation=Quaternion.Euler(90,Time.time*85,0);
                var c=displayedCoins[i];coinVisuals[i].position=new Vector3(c.x,c.y+1.2f+Mathf.Sin(Time.time*2+i)*.12f,c.z);
            }
        }
        NameLabel[] Labels()
        {
            if(showroom)return new NameLabel[0];var result=new NameLabel[cars.Count];
            for(int i=0;i<cars.Count;i++){
                Vector3 p=cam.WorldToViewportPoint(visuals[i].position+Vector3.up*2.1f);
                result[i]=new NameLabel{id=cars[i].id,name=cars[i].name,effect=cars[i].specialTime>0?Vehicles.Get(cars[i].vehicle).special:cars[i].jamTime>0?"jam":"",x=p.x,y=1-p.y,visible=p.z>0&&p.z<90&&p.x>0&&p.x<1&&p.y>0&&p.y<1,self=i==0};
            }
            return result;
        }
    }
}