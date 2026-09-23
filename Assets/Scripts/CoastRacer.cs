using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using CoastRacer.Core;

namespace CoastRacer
{
    public sealed partial class RaceGame : MonoBehaviour
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void RacerEmit(string json);
#endif
        [Serializable] public sealed class Command { public string action,track,id,vehicle,name,badge;public float orbit;public float assist=1,sensitivity=1;public int quality=1;public bool paused; }
        [Serializable] public sealed class Snapshot { public string type,track,phase,self,raceId;public float countdown,time;public CarState[] cars;public CoinState[] coins; }
        [Serializable] public sealed class Telemetry { public string type="telemetry",track,phase;public float countdown;public CarState[] cars;public Point[] map;public float length;public string raceId;public bool showroom,ready;public VehicleSpec[] vehicles;public CoinState[] coins;public NameLabel[] labels; }
        Track track;
        GameObject world;
        Camera cam;
        readonly List<CarState> cars=new List<CarState>();
        readonly List<Transform> visuals=new List<Transform>();
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        DriveInput input=new DriveInput();
        string phase="menu",self="";
        bool online,paused;
        float countdown=3,sendTime,accumulator,recoverCooldown;
        Font editorFont;
        Vector3 correction;
        float cameraAngle;
        Material road,white,red,grass,steel,glass,rubber,paint;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(FindAnyObjectByType<RaceGame>()==null)new GameObject("RaceGame").AddComponent<RaceGame>();}
        void Awake()
        {
            Application.targetFrameRate=60;
#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput=false;
#endif
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))camera.gameObject.SetActive(false);
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))light.gameObject.SetActive(false);
            road=Mat("Asphalt",new Color(.18f,.19f,.20f));white=Mat("Paint",new Color(.84f,.84f,.80f));
            red=Mat("Kerb",new Color(.55f,.10f,.10f));grass=Mat("Grass",new Color(.26f,.32f,.20f));
            steel=Mat("Steel",new Color(.42f,.45f,.48f));glass=Mat("Glass",new Color(.045f,.075f,.10f));
            rubber=Mat("Rubber",new Color(.055f,.055f,.058f));paint=Mat("Body",new Color(.55f,.60f,.65f));
            cam=new GameObject("Chase Camera",typeof(AudioListener)).AddComponent<Camera>();
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.67f,.72f,.76f);
            cam.fieldOfView=58;cam.nearClipPlane=.2f;cam.farClipPlane=850;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=cam.backgroundColor;
            RenderSettings.fogStartDistance=250;RenderSettings.fogEndDistance=800;
            QualitySettings.vSyncCount=0;
#if UNITY_EDITOR
            editorFont=Font.CreateDynamicFontFromOSFont(new[]{"Yu Gothic","Meiryo","Arial"},22);
#endif
            SelectTrack("ridge");
        }
        Material Mat(string name,Color color)
        {
            if(materials.TryGetValue(name,out var m))return m;
            m=new Material(Resources.Load<Shader>("CoastSurface")){name=name,color=color};
            m.SetFloat("_Metallic",name.StartsWith("Car")||name=="Steel"?.65f:name=="Glass"?.4f:0);
            m.SetFloat("_Smoothness",name.StartsWith("Car")?.82f:name=="Glass"?.96f:name=="Steel"?.72f:.18f);
            m.SetFloat("_Grain",name=="Asphalt"?.18f:name=="Grass"?.28f:0);
            materials[name]=m;return m;
        }
        static Vector3 V(Point p)=>new Vector3(p.x,p.y,p.z);
        GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material material,Transform parent)
        {
            var obj=GameObject.CreatePrimitive(type);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=p;obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            Destroy(obj.GetComponent<Collider>());return obj;
        }
        void Ribbon(string name,float inner,float outer,float lift,Material m,bool alternating=false)
        {
            var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(int i=0;i<Track.Samples;i++){
                if(alternating && i%8>=4)continue;
                int j=(i+1)%Track.Samples;int start=verts.Count;
                Point a=track.points[i],b=track.points[j],ta=track.Tangent(i),tb=track.Tangent(j);
                Vector3 ra=new Vector3(ta.z,0,-ta.x),rb=new Vector3(tb.z,0,-tb.x);
                verts.Add(V(a)+ra*inner+Vector3.up*lift);verts.Add(V(a)+ra*outer+Vector3.up*lift);
                verts.Add(V(b)+rb*inner+Vector3.up*lift);verts.Add(V(b)+rb*outer+Vector3.up*lift);
                uv.Add(new Vector2(inner,track.distance[i]));uv.Add(new Vector2(outer,track.distance[i]));
                uv.Add(new Vector2(inner,track.distance[j]));uv.Add(new Vector2(outer,track.distance[j]));
                tris.AddRange(new[]{start,start+2,start+1,start+1,start+2,start+3});
            }
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(world.transform);
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=m;
        }
        void TerrainSurface()
        {
            float minX=float.MaxValue,maxX=float.MinValue,minZ=minX,maxZ=maxX;
            foreach(var p in track.points){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
            minX-=160;maxX+=160;minZ-=160;maxZ+=160;
            const int nx=64,nz=40;var vertices=new Vector3[(nx+1)*(nz+1)];var tris=new List<int>();
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){
                float px=Mathf.Lerp(minX,maxX,(float)x/nx),pz=Mathf.Lerp(minZ,maxZ,(float)z/nz);
                float best=float.MaxValue,height=0,low=float.MaxValue;
                for(int k=0;k<Track.Samples;k+=4){
                    var p=track.points[k];float d=(p.x-px)*(p.x-px)+(p.z-pz)*(p.z-pz);
                    if(d<best){best=d;height=p.y;}
                    if(d<6400)low=Mathf.Min(low,p.y);
                }
                if(low<float.MaxValue)height=low;
                vertices[z*(nx+1)+x]=new Vector3(px,height-2.8f,pz);
                if(x<nx&&z<nz){int a=z*(nx+1)+x;tris.AddRange(new[]{a,a+nx+1,a+1,a+1,a+nx+1,a+nx+2});}
            }
            var go=new GameObject("Rolling landscape",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(world.transform);
            var mesh=new Mesh{vertices=vertices,triangles=tris.ToArray()};mesh.RecalculateNormals();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=grass;
        }
        void SculptedBody(Transform parent,Material body)
        {
            // Bevelled cross-sections: tapered nose/tail, wheel shoulders, and a low GT silhouette.
            float[] zs={-2.2f,-1.85f,-1.25f,.55f,1.5f,2.15f};
            float[] widths={.78f,.96f,.98f,.96f,.89f,.77f};
            float[] heights={.70f,.90f,.97f,.89f,.78f,.60f};
            var verts=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<zs.Length;i++){
                float w=widths[i],h=heights[i],z=zs[i];
                verts.AddRange(new[]{new Vector3(-w*.8f,.30f,z),new Vector3(-w,.42f,z),new Vector3(-w,h-.13f,z),new Vector3(-w*.78f,h,z),new Vector3(w*.78f,h,z),new Vector3(w,h-.13f,z),new Vector3(w,.42f,z),new Vector3(w*.8f,.30f,z)});
                if(i==0)continue;
                for(int j=0;j<8;j++){int a=(i-1)*8+j,b=(i-1)*8+(j+1)%8,c=i*8+j,d=i*8+(j+1)%8;triangles.AddRange(new[]{a,c,b,b,c,d});}
            }
            for(int j=1;j<7;j++){triangles.AddRange(new[]{0,j,j+1});int a=(zs.Length-1)*8;triangles.AddRange(new[]{a,a+j+1,a+j});}
            var go=new GameObject("Sculpted GT body",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=body;
            // Sloped glazing instead of a rectangular cabin.
            var cabin=new GameObject("Swept glazing",typeof(MeshFilter),typeof(MeshRenderer));cabin.transform.SetParent(parent,false);
            var v=new[]{new Vector3(-.80f,.88f,-1.15f),new Vector3(.80f,.88f,-1.15f),new Vector3(-.77f,.88f,.90f),new Vector3(.77f,.88f,.90f),new Vector3(-.66f,1.40f,-.67f),new Vector3(.66f,1.40f,-.67f),new Vector3(-.65f,1.40f,.24f),new Vector3(.65f,1.40f,.24f)};
            if(parent.name.StartsWith("swift")){v[0].z=v[1].z=-1.65f;v[4].z=v[5].z=-1.16f;}
            if(parent.name.StartsWith("atlas")){v[0].z=v[1].z=-1.58f;v[4].z=v[5].z=-1.03f;}
            var cabinMesh=new Mesh{vertices=v,triangles=new[]{0,4,1,1,4,5,2,3,6,3,7,6,0,2,4,2,6,4,1,5,3,3,5,7,4,6,5,5,6,7}};cabinMesh.RecalculateNormals();
            cabin.GetComponent<MeshFilter>().sharedMesh=cabinMesh;cabin.GetComponent<MeshRenderer>().sharedMaterial=glass;
            foreach(int s in new[]{-1,1})Shape("Mirror",PrimitiveType.Cube,new Vector3(s*1.02f,1.05f,.48f),new Vector3(.25f,.13f,.32f),body,parent);
        }

        void SelectTrack(string id)
        {
            LeaveShowroom();
            if(world!=null)Destroy(world);
            foreach(var v in visuals)if(v)Destroy(v.gameObject);visuals.Clear();cars.Clear();
            track=new Track(id);session=new RaceSession(track);displayedCoins=session.coins;world=new GameObject("Circuit "+track.id);
            TerrainSurface();
            Ribbon("Landscape ribbon",-45,45,-.3f,grass);
            Ribbon("Runoff",-16,16,-.05f,grass);
            Ribbon("Racing surface",-7,7,0,road);
            foreach(int side in new[]{-1,1}){
                float a=side<0?-8.1f:7.1f,b=side<0?-7.1f:8.1f;
                Ribbon("White curb",a,b,.025f,white);Ribbon("Red curb",a,b,.04f,red,true);
                Ribbon("Track edge",side<0?-6.9f:6.7f,side<0?-6.7f:6.9f,.03f,white);
            }
            var scenery=new GameObject("Track furniture").transform;scenery.SetParent(world.transform);
            for(int i=0;i<Track.Samples;i+=6){
                Point p=track.points[i],t=track.Tangent(i);Vector3 r=new Vector3(t.z,0,-t.x);
                float heading=track.Yaw(i)*Mathf.Rad2Deg;
                for(int s=-1;s<=1;s+=2){
                    var rail=Shape("Guardrail",PrimitiveType.Cube,V(p)+r*s*16.5f+Vector3.up*.7f,new Vector3(.22f,1.1f,6),steel,scenery);
                    rail.transform.rotation=Quaternion.Euler(0,heading,0);
                    if(i%18==0){
                        Vector3 tree=V(p)+r*s*32;
                        Shape("Tree trunk",PrimitiveType.Cylinder,tree+Vector3.up*2,new Vector3(.6f,2,.6f),rubber,scenery);
                        Shape("Tree canopy",PrimitiveType.Sphere,tree+Vector3.up*5,new Vector3(5,7,5),grass,scenery);
                    }
                }
                if(track.TargetSpeed(i)<24 && i%24==0){
                    var sign=Shape("Braking marker",PrimitiveType.Cube,V(p)+r*11+Vector3.up*1.5f,new Vector3(1.5f,2,.15f),white,scenery);
                    sign.transform.rotation=Quaternion.Euler(0,heading,0);
                }
            }
            Point start=track.points[0],tan=track.Tangent(0);Vector3 right=new Vector3(tan.z,0,-tan.x);
            for(int i=0;i<14;i++)for(int j=0;j<2;j++){
                var tile=Shape("Finish chequer",PrimitiveType.Cube,V(start)+right*(i-6.5f)+V(tan)*(j-.5f)+Vector3.up*.05f,new Vector3(1,.05f,1),(i+j)%2==0?white:rubber,scenery);
                tile.transform.rotation=Quaternion.Euler(0,track.Yaw(0)*Mathf.Rad2Deg,0);
            }
            for(int i=0;i<8;i++){
                Vector3 p=V(track.At(i*10))+right*28;
                Shape("Pit garages",PrimitiveType.Cube,p+Vector3.up*3,new Vector3(15,6,9),steel,scenery);
                Shape("Pit glass",PrimitiveType.Cube,p-right*7.6f+Vector3.up*3,new Vector3(.1f,2.7f,7),glass,scenery);
            }
            // Bake scenery per material, keeping draw calls low without generating a separate prefab per object.
            ExtraScenery(scenery);CreateBackdrop();CombineScenery(scenery);
            cars.Add(Simulation.Spawn(track));visuals.Add(BuildCar(0));
            Place(visuals[0],cars[0],0);
            CreateCoins();CameraFollow(true);Emit(true);
        }
        void CombineScenery(Transform parent)
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var mf in parent.GetComponentsInChildren<MeshFilter>()){
                var m=mf.GetComponent<MeshRenderer>().sharedMaterial;
                if(!groups.ContainsKey(m))groups[m]=new List<CombineInstance>();
                groups[m].Add(new CombineInstance{mesh=mf.sharedMesh,transform=mf.transform.localToWorldMatrix});
            }
            foreach(var pair in groups){
                var go=new GameObject("Batched "+pair.Key.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(world.transform);
                var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray());
                go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            Destroy(parent.gameObject);
        }
        Transform BuildCar(int n,string vehicle=null,string badge="")
        {
            vehicle=Vehicles.Get(vehicle??(n<cars.Count?cars[n].vehicle:"apex")).id;
            var car=new GameObject(vehicle+"/"+badge).transform;
            Color[] colors={new Color(.68f,.71f,.73f),new Color(.55f,.13f,.1f),new Color(.12f,.25f,.38f),new Color(.61f,.50f,.19f)};
            int vehicleIndex=Array.FindIndex(Vehicles.All,x=>x.id==vehicle);
            Material body=Mat("Car"+vehicle,colors[vehicleIndex%colors.Length]);
            if(vehicleIndex>=4){NoveltyBody(car,vehicle,badge,body);return car;}SculptedBody(car,body);
            bool hatch=vehicle=="swift",touring=vehicle=="atlas";
            Shape("Roof",PrimitiveType.Cube,new Vector3(0,1.425f,hatch?-.42f:touring?-.36f:-.215f),new Vector3(1.31f,.04f,hatch?1.48f:touring?1.34f:.96f),body,car);
            Shape("Front splitter",PrimitiveType.Cube,new Vector3(0,.30f,2.12f),new Vector3(1.95f,.1f,.32f),rubber,car);
            Shape("Rear diffuser",PrimitiveType.Cube,new Vector3(0,.30f,-2.1f),new Vector3(1.7f,.18f,.35f),rubber,car);
            for(int s=-1;s<=1;s+=2){
                Shape("Spoiler support",PrimitiveType.Cube,new Vector3(s*.6f,1.07f,-1.95f),new Vector3(.08f,.42f,.1f),rubber,car);
                Shape("Headlamp",PrimitiveType.Cube,new Vector3(s*.62f,.76f,2.18f),new Vector3(.48f,.1f,.03f),white,car);
                Shape("Tail lamp",PrimitiveType.Cube,new Vector3(s*.61f,.75f,-2.18f),new Vector3(.53f,.08f,.03f),red,car);
                foreach(float z in new[]{-1.3f,1.3f}){
                    var wheel=Shape("Tyre",PrimitiveType.Cylinder,new Vector3(s*.96f,.39f,z),new Vector3(.76f,.14f,.76f),rubber,car);
                    wheel.transform.localRotation=Quaternion.Euler(0,0,90);
                    var hub=Shape("Alloy wheel",PrimitiveType.Cylinder,new Vector3(s*1.105f,.39f,z),new Vector3(.5f,.012f,.5f),steel,car);
                    hub.transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            Shape("Rear wing",PrimitiveType.Cube,new Vector3(0,1.29f,-1.95f),new Vector3(2.05f,.09f,.42f),rubber,car);
            CustomizeCar(car,vehicle,badge,body);return car;
        }
        [UnityEngine.Scripting.Preserve]
        public void SetInput(string json){try{input=JsonUtility.FromJson<DriveInput>(json);}catch{}}
        [UnityEngine.Scripting.Preserve]
        public void CommandFromWeb(string json)
        {
            var c=JsonUtility.FromJson<Command>(json);
            if(c.action=="showcar"){ShowCar(c.vehicle,c.badge);return;}
            if(c.action=="orbit"){showroomAngle+=c.orbit;return;}
            if(c.action=="profile"&&cars.Count>0&&!online){cars[0].name=c.name;cars[0].badge=Vehicles.Badge(c.badge);}
            if(c.action=="special"&&!online&&phase=="race"&&!paused){session.Activate(cars[0],cars);}
            if(c.action=="begin"&&!online&&phase=="loading"){phase="countdown";countdown=3;}
            if(c.action=="preview"){online=false;phase="menu";SelectTrack(c.track);}
            if(c.action=="start"){
                online=false;self="";SelectTrack(c.track);cars[0].name=string.IsNullOrWhiteSpace(c.name)?"ドライバー":c.name;cars[0].id="local";cars[0].vehicle=Vehicles.Get(c.vehicle).id;cars[0].badge=Vehicles.Badge(c.badge);raceId=Guid.NewGuid().ToString("N");
                for(int i=1;i<4;i++){cars.Add(Simulation.Spawn(track,i));cars[i].bot=true;cars[i].name="GT "+i;cars[i].id="ai"+i;cars[i].vehicle=Vehicles.All[i%4].id;visuals.Add(BuildCar(i));}
                RefreshVisuals();paused=false;phase="loading";countdown=3;input=new DriveInput{assist=c.assist,sensitivity=c.sensitivity};accumulator=0;
            }
            if(c.action=="menu"){online=false;paused=false;phase="menu";SelectTrack(c.track??track.id);}
            if(c.action=="pause" && !online){paused=c.paused;input.throttle=input.brake=input.steer=0;}
            if(c.action=="quality"){cam.farClipPlane=c.quality==0?500:850;QualitySettings.antiAliasing=c.quality==0?0:2;Application.targetFrameRate=c.quality==0?30:60;}
            Emit(true);
        }
        [UnityEngine.Scripting.Preserve]
        public void NetworkSnapshot(string json)
        {
            var snapshot=JsonUtility.FromJson<Snapshot>(json);
            if(snapshot==null||snapshot.cars==null||snapshot.cars.Length==0)return;
            if(!online||track.id!=snapshot.track){SelectTrack(snapshot.track);cars.Clear();foreach(var v in visuals)Destroy(v.gameObject);visuals.Clear();}
            raceId=snapshot.raceId;displayedCoins=snapshot.coins??session.coins;online=true;self=snapshot.self;phase=snapshot.phase;countdown=snapshot.countdown;paused=false;
            var ordered=new List<CarState>(snapshot.cars);
            ordered.Sort((a,b)=>a.id==self?-1:b.id==self?1:string.CompareOrdinal(a.id,b.id));
            while(visuals.Count<ordered.Count)visuals.Add(BuildCar(visuals.Count));
            // Authoritative snapshots correct client prediction; camera smoothing hides small corrections.
            cars.Clear();cars.AddRange(ordered);
            for(int i=0;i<visuals.Count;i++)visuals[i].gameObject.SetActive(i<cars.Count);
        }
        void Update()
        {
            if(cars.Count==0)return;
            float dt=Mathf.Min(Time.deltaTime,.05f);recoverCooldown-=dt;
            if(showroom){UpdateShowroom(dt);sendTime-=dt;if(sendTime<=0){sendTime=.1f;Emit(false);}return;}
#if !UNITY_WEBGL || UNITY_EDITOR
            input.steer=Input.GetAxisRaw("Horizontal");
            input.throttle=Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)?1:0;
            input.brake=Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)||Input.GetKey(KeyCode.Space)?1:0;
#endif
            if(!paused){
                if(!online && phase=="countdown"){countdown-=dt;if(countdown<=0){phase="race";Debug.Log("COAST_RACER_GO");}}
                if(phase=="race"){
                    accumulator+=dt;
                    while(accumulator>=Simulation.Step){
                        accumulator-=Simulation.Step;
                        Simulation.StepCar(cars[0],input,track,Simulation.Step);
                        if(!online){for(int i=1;i<cars.Count;i++){session.UseBotSpecial(cars[i],cars);Simulation.StepCar(cars[i],session.Bot(cars[i],.90f),track,Simulation.Step);}session.Resolve(cars,Simulation.Step);}
                    }
                    if(!online){
                        Simulation.Rank(cars,track);
                        if(cars[0].finished){Simulation.EstimateBots(cars,track);phase="finished";Debug.Log("COAST_RACER_FINISHED");}
                    }
                }
            }
            RefreshVisuals();UpdateCoins();
            for(int i=0;i<cars.Count;i++)Place(visuals[i],cars[i],dt);
            CameraFollow(false);
            sendTime-=dt;if(sendTime<=0){sendTime=.1f;Emit(false);}
        }
        void Place(Transform model,CarState c,float dt)
        {
            Vector3 target=V(c.Position);
            if(dt<=0 || Vector3.Distance(model.position,target)>20)model.position=target;
            else model.position=Vector3.Lerp(model.position,target,1-Mathf.Exp(-18*dt));
            Point tangent=track.Tangent(c.index);
            float pitch=-Mathf.Asin(Mathf.Clamp(tangent.y,-.9f,.9f))*Mathf.Rad2Deg*Mathf.Cos(c.yaw-track.Yaw(c.index));
            Quaternion rotation=Quaternion.Euler(pitch,c.yaw*Mathf.Rad2Deg,-c.steering*Mathf.Min(2,c.speed*.06f));
            model.rotation=dt<=0?rotation:Quaternion.Slerp(model.rotation,rotation,1-Mathf.Exp(-12*dt));
        }
        void CameraFollow(bool snap)
        {
            if(visuals.Count==0)return;
            Transform car=visuals[0];
            Vector3 target=car.position-car.forward*9+Vector3.up*4;
            cam.transform.position=snap?target:Vector3.Lerp(cam.transform.position,target,1-Mathf.Exp(-6*Time.deltaTime));
            cam.transform.LookAt(car.position+car.forward*10+Vector3.up*1.1f);
            cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,58+cars[0].speed*.10f,Time.deltaTime*2);
        }
        void Emit(bool includeMap)
        {
            if(track==null)return;
            Point[] map=null;if(includeMap){map=new Point[160];for(int i=0;i<map.Length;i++)map[i]=track.points[i*4];}
            string json=JsonUtility.ToJson(new Telemetry{track=track.id,phase=paused?"paused":phase,countdown=countdown,cars=cars.ToArray(),map=map,length=track.Length,raceId=raceId,showroom=showroom,ready=UnityEngine.Rendering.SplashScreen.isFinished,vehicles=includeMap?Vehicles.All:null,coins=displayedCoins,labels=Labels()});
#if UNITY_WEBGL && !UNITY_EDITOR
            RacerEmit(json);
#endif
        }
        void OnApplicationFocus(bool focus){if(!focus){input.throttle=input.brake=input.steer=0;if(!online&&phase=="race")paused=true;}}
#if UNITY_EDITOR
        void OnGUI()
        {
            GUI.skin.font=editorFont;GUI.skin.button.fontSize=20;GUI.skin.label.fontSize=20;
            if(phase=="menu"){
                GUI.Box(new Rect(20,20,480,210),"");
                GUI.Label(new Rect(40,35,440,40),"コーストレーサー / 開発プレビュー");
                if(GUI.Button(new Rect(40,85,420,45),"山岳サーキットで開始"))CommandFromWeb("{\"action\":\"start\",\"track\":\"ridge\"}");
                if(GUI.Button(new Rect(40,140,420,45),"鈴鹿風サーキットで開始"))CommandFromWeb("{\"action\":\"start\",\"track\":\"suzuka\"}");
            }else{
                GUI.Label(new Rect(20,20,700,80),$"速度 {cars[0].speed*3.6f:0} km/h  周回 {Math.Min(3,cars[0].lap+1)}/3  順位 {cars[0].rank}\n矢印:操舵・ペダル / R:復帰");
                if(GUI.Button(new Rect(20,110,180,40),paused?"再開":"一時停止"))paused=!paused;
                if(GUI.Button(new Rect(20,160,180,40),"メニュー"))CommandFromWeb("{\"action\":\"menu\"}");
            }
        }
#endif
    }
}