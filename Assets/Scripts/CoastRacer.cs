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
        [Serializable] public sealed class Command { public string action,track,id,vehicle,name,badge;public float orbit;public float assist=1,sensitivity=1;public int quality=1,difficulty=3;public bool paused,rearView,night; }
        [Serializable] public sealed class Snapshot { public string type,track,phase,self,raceId;public float countdown,time;public int difficulty=3;public bool night;public CarState[] cars;public CoinState[] coins; }
        [Serializable] public sealed class Telemetry { public string type="telemetry",track,phase;public float countdown;public CarState[] cars;public Point[] map;public float length;public string raceId;public bool showroom,ready,rearView,night;public int difficulty;public VehicleSpec[] vehicles;public CoinState[] coins;public NameLabel[] labels; }
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
        float cameraAngle,lastWebInput;bool rearView,lastRearView;
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
            if(parent.name=="Track furniture"&&name!="Finish chequer"&&!track.SceneryClear(new Point(p.x,p.y,p.z),Mathf.Sqrt(size.x*size.x+size.z*size.z)*.5f)){
                var omitted=new GameObject("Omitted overlapping "+name);omitted.transform.SetParent(parent,false);return omitted;
            }
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
                // Wide landscape strips must not intersect another road or its terrain, at either elevation.
                if(name=="Landscape ribbon"&&track.ForeignRoadBelow(i,65,float.NegativeInfinity))continue;
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
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();MakeTwoSided(mesh);
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
            var mesh=new Mesh{vertices=vertices,triangles=tris.ToArray()};mesh.RecalculateNormals();MakeTwoSided(mesh);
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
            BlenderModel("course_"+track.id,world.transform);CreateBackdrop();ApplyTimeOfDay();
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
            foreach(var mf in parent.GetComponentsInChildren<MeshFilter>())if((mf.sharedMesh.name=="Coast tree mesh"||mf.sharedMesh.name=="Coast rock mesh"))Destroy(mf.sharedMesh);
            Destroy(parent.gameObject);
        }
        Transform BuildCar(int n,string vehicle=null,string badge="")
        {
            vehicle=Vehicles.Get(vehicle??(n<cars.Count?cars[n].vehicle:"apex")).id;
            var car=BlenderModel("vehicle_"+vehicle);car.name=vehicle+"/"+badge;car.gameObject.AddComponent<BlenderWheels>();BuildCustomBadge(car,badge);return car;
        }
        [UnityEngine.Scripting.Preserve]
        public void SetInput(string json){try{input=JsonUtility.FromJson<DriveInput>(json);lastWebInput=Time.realtimeSinceStartup;}catch{input=new DriveInput();}}
        [UnityEngine.Scripting.Preserve]
        public void CommandFromWeb(string json)
        {
            var c=JsonUtility.FromJson<Command>(json);
            if(c.action=="showcar"){ShowCar(c.vehicle,c.badge);return;}
            if(c.action=="badgeView"){SetBadgeView(c.id);return;}
            if(c.action=="orbit"){showroomAngle+=c.orbit;return;}
            if(c.action=="rearView")rearView=c.rearView;
            if(c.action=="profile"&&cars.Count>0&&!online){cars[0].name=c.name;cars[0].badge=Vehicles.Badge(c.badge);}
            if(c.action=="special"&&!online&&phase=="race"&&!paused){session.Activate(cars[0],cars);}
            if(c.action=="begin"&&!online&&phase=="loading"){phase="countdown";countdown=3;}
            if(c.action=="preview"){online=false;phase="menu";nightMode=c.night;cpuDifficulty=Math.Max(1,Math.Min(5,c.difficulty));SelectTrack(c.track);}
            if(c.action=="start"){
                online=false;self="";nightMode=c.night;cpuDifficulty=Math.Max(1,Math.Min(5,c.difficulty));SelectTrack(c.track);var slots=Simulation.Grid(4,new System.Random());cars[0]=Simulation.Spawn(track,slots[0]);cars[0].cpuLevel=cpuDifficulty;cars[0].night=nightMode;cars[0].name=string.IsNullOrWhiteSpace(c.name)?"ドライバー":c.name;cars[0].id="local";cars[0].vehicle=Vehicles.Get(c.vehicle).id;cars[0].badge=Vehicles.Badge(c.badge);raceId=Guid.NewGuid().ToString("N");
                for(int i=1;i<4;i++){cars.Add(Simulation.Spawn(track,slots[i]));cars[i].cpuLevel=cpuDifficulty;cars[i].night=nightMode;cars[i].bot=true;cars[i].name="GT "+i;cars[i].id="ai"+i;cars[i].vehicle=Vehicles.All[new System.Random(raceId.GetHashCode()+i).Next(Vehicles.All.Length)].id;visuals.Add(BuildCar(i));}
                RefreshVisuals();paused=false;phase="loading";countdown=3;input=new DriveInput{assist=c.assist,sensitivity=c.sensitivity};accumulator=0;
            }
            if(c.action=="menu"){online=false;paused=false;phase="menu";SelectTrack(c.track??track.id);}
            if(c.action=="pause" && !online){paused=c.paused;input.throttle=input.brake=input.steer=0;}
            if(c.action=="quality"){effectQuality=c.quality;cam.farClipPlane=c.quality==0?500:850;QualitySettings.antiAliasing=c.quality==0?0:2;Application.targetFrameRate=c.quality==0?30:60;}
            Emit(true);
        }
        [UnityEngine.Scripting.Preserve]
        public void NetworkSnapshot(string json)
        {
            var snapshot=JsonUtility.FromJson<Snapshot>(json);
            if(snapshot==null||snapshot.cars==null||snapshot.cars.Length==0)return;
            onlineSceneTime=snapshot.time;nightMode=snapshot.night;cpuDifficulty=snapshot.difficulty;ApplyTimeOfDay();if(!online||showroom||track.id!=snapshot.track){SelectTrack(snapshot.track);cars.Clear();foreach(var v in visuals)Destroy(v.gameObject);visuals.Clear();}
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
#if UNITY_WEBGL && !UNITY_EDITOR
            if(Time.realtimeSinceStartup-lastWebInput>.75f){input.throttle=input.steer=0;input.brake=1;}
#endif
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
                        if(!online){for(int i=1;i<cars.Count;i++){session.UseBotSpecial(cars[i],cars);Simulation.StepCar(cars[i],session.TrafficBot(cars[i],cars,Simulation.Difficulty(cpuDifficulty)),track,Simulation.Step);}session.Resolve(cars,Simulation.Step);}
                    }
                    if(!online){
                        Simulation.Rank(cars,track);
                        if(cars[0].finished){Simulation.EstimateBots(cars,track);phase="finished";Debug.Log("COAST_RACER_FINISHED");}
                    }
                }
            }
            if(!paused&&phase=="finished")foreach(var c in cars)Simulation.StepCar(c,new DriveInput(),track,dt);
            ApplyTimeOfDay();RefreshVisuals();UpdateCoins();UpdateSpecialVisuals();
            for(int i=0;i<cars.Count;i++)Place(visuals[i],cars[i],dt);
            UpdateRevisionEightScenery(dt);CameraFollow(false);
            sendTime-=dt;if(sendTime<=0){sendTime=.1f;Emit(false);}
        }
        void Place(Transform model,CarState c,float dt)
        {
            Vector3 target=V(c.Position);target.y=track.At(track.RoadDistance(c.Position,c.index)).y;
            if(c.finished || dt<=0 || Vector3.Distance(model.position,target)>20)model.position=target;
            else model.position=Vector3.Lerp(model.position,target,1-Mathf.Exp(-18*dt));
            Point tangent=track.Tangent(c.index);
            float pitch=-Mathf.Asin(Mathf.Clamp(tangent.y,-.9f,.9f))*Mathf.Rad2Deg*Mathf.Cos(c.yaw-track.Yaw(c.index));
            Quaternion rotation=Quaternion.Euler(pitch,c.yaw*Mathf.Rad2Deg,-c.steering*Mathf.Min(2,c.speed*.06f));
            model.rotation=dt<=0?rotation:Quaternion.Slerp(model.rotation,rotation,1-Mathf.Exp(-12*dt));
            UpdateBlenderCar(model,c,dt);model.GetComponent<BlenderWheels>()?.Ground(track,c.index);
        }
        string finishCameraRace="";float finishCameraStarted=-1;
        void CameraFollow(bool snap)
        {
            if(visuals.Count==0)return;Transform car=visuals[0];
            if(finishCameraRace!=raceId||!cars[0].finished){finishCameraRace=raceId;finishCameraStarted=-1;}
            if(cars[0].finished&&finishCameraStarted<0){finishCameraStarted=Time.unscaledTime;rearView=false;}
            float direction=rearView?-1:1;snap|=lastRearView!=rearView;lastRearView=rearView;
            Vector3 target=car.position-car.forward*(9*direction)+Vector3.up*4;
            Vector3 aim=car.position+car.forward*(10*direction)+Vector3.up*1.1f;
            float blend=finishCameraStarted<0?0:Mathf.SmoothStep(0,1,(Time.unscaledTime-finishCameraStarted-.6f)/1.4f);
            if(blend>0){float angle=Mathf.Lerp(180,45,blend)*Mathf.Deg2Rad;float radius=Mathf.Lerp(9,7,blend);target=car.position+(car.right*Mathf.Sin(angle)+car.forward*Mathf.Cos(angle))*radius+Vector3.up*Mathf.Lerp(4,2.8f,blend);aim=Vector3.Lerp(aim,car.position+Vector3.up*.9f,blend);}
            var origin=car.position+Vector3.up*1.2f;var ray=target-origin;
            if(Physics.SphereCast(origin,.3f,ray.normalized,out var hit,ray.magnitude,1<<8,QueryTriggerInteraction.Ignore))target=origin+ray.normalized*Mathf.Max(1,hit.distance-.2f);
            cam.transform.position=snap?target:Vector3.Lerp(cam.transform.position,target,1-Mathf.Exp(-6*Time.deltaTime));
            cam.transform.LookAt(aim);cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,Mathf.Lerp(58+cars[0].speed*.10f,53,blend),Time.deltaTime*2);
        }
        void Emit(bool includeMap)
        {
            if(track==null)return;
            Point[] map=null;if(includeMap){map=new Point[160];for(int i=0;i<map.Length;i++)map[i]=track.points[i*4];}
            string json=JsonUtility.ToJson(new Telemetry{track=track.id,phase=paused?"paused":phase,countdown=countdown,cars=cars.ToArray(),map=map,length=track.Length,raceId=raceId,showroom=showroom,rearView=rearView,night=nightMode,difficulty=cpuDifficulty,ready=UnityEngine.Rendering.SplashScreen.isFinished,vehicles=includeMap?Vehicles.All:null,coins=displayedCoins,labels=Labels()});
#if UNITY_WEBGL && !UNITY_EDITOR
            RacerEmit(json);
#endif
        }
        void OnApplicationFocus(bool focus){if(!focus){input.throttle=input.brake=input.steer=0;
#if !UNITY_WEBGL || UNITY_EDITOR
            if(!online&&phase=="race")paused=true;
#endif
            // In WebGL the browser owns pause/resume, including focus moving to HTML controls.
        }}
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
                GUI.Label(new Rect(20,20,700,80),$"速度 {cars[0].speed*3.6f:0} km/h  周回 {Math.Min(2,cars[0].lap+1)}/2  順位 {cars[0].rank}\n矢印:操舵・ペダル / R:復帰");
                if(GUI.Button(new Rect(20,110,180,40),paused?"再開":"一時停止"))paused=!paused;
                if(GUI.Button(new Rect(20,160,180,40),"メニュー"))CommandFromWeb("{\"action\":\"menu\"}");
            }
        }
#endif
    }
}