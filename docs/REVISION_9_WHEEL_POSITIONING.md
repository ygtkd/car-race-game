# 改修9 車輪の配置コード

実装で使用している生成コードとUnityの表示処理を以下に掲載する。基礎走行性能は変更せず、モデルと表示用の接地を修正した。

## 座標系・寸法

原点は車体中央の路面高さ。単位m。UnityではXが右、Yが上、Zが前。Blenderへの変換は `(x, -z, y)`、Unityへ戻すときは `(x, z, -y)`。表の中心座標は車体ローカル座標。基準回転は全輪 `(0,0,0)`、車軸はX方向。左前・右前・左後・右後は `wheel_front_負/正`、`wheel_rear_負/正` に対応する。

タイヤの外周半径と取付高さを合わせ、ハブを側面の内側へ収めた。四輪車の左右トレッドを車体に合わせ、ボディとキャビンを含めてホイールハウスを生成する。二輪・三輪・タイヤなし車は車種固有の構成を維持する。

| 車種ID | 車輪 | 中心 X, Y, Z (m) | 半径 (m) | 半幅 (m) |
|---|---|---|---|---|

| aerobike | wheel_rear_0 | 0.000, 0.550, -1.270 | 0.550 | 0.154 |
| aerobike | wheel_front_0 | 0.000, 0.550, 1.270 | 0.550 | 0.154 |
| apex | wheel_rear_-0.87 | -0.870, 0.380, -1.300 | 0.380 | 0.175 |
| apex | wheel_front_-0.87 | -0.870, 0.380, 1.300 | 0.380 | 0.175 |
| apex | wheel_rear_0.87 | 0.870, 0.380, -1.300 | 0.380 | 0.175 |
| apex | wheel_front_0.87 | 0.870, 0.380, 1.300 | 0.380 | 0.175 |
| atlas | wheel_rear_-0.87 | -0.870, 0.380, -1.339 | 0.380 | 0.175 |
| atlas | wheel_front_-0.87 | -0.870, 0.380, 1.339 | 0.380 | 0.175 |
| atlas | wheel_rear_0.87 | 0.870, 0.380, -1.339 | 0.380 | 0.175 |
| atlas | wheel_front_0.87 | 0.870, 0.380, 1.339 | 0.380 | 0.175 |
| bicycle | wheel_rear_0 | 0.000, 0.550, -1.270 | 0.550 | 0.154 |
| bicycle | wheel_front_0 | 0.000, 0.550, 1.270 | 0.550 | 0.154 |
| kebab | wheel_rear_-0.8 | -0.800, 0.380, -1.220 | 0.380 | 0.175 |
| kebab | wheel_rear_0.8 | 0.800, 0.380, -1.220 | 0.380 | 0.175 |
| kebab | wheel_front_-0.8 | -0.800, 0.380, 1.220 | 0.380 | 0.175 |
| kebab | wheel_front_0.8 | 0.800, 0.380, 1.220 | 0.380 | 0.175 |
| stormbike | wheel_rear_0 | 0.000, 0.550, -1.270 | 0.550 | 0.154 |
| stormbike | wheel_front_0 | 0.000, 0.550, 1.270 | 0.550 | 0.154 |
| swift | wheel_rear_-0.87 | -0.870, 0.380, -1.170 | 0.380 | 0.175 |
| swift | wheel_front_-0.87 | -0.870, 0.380, 1.170 | 0.380 | 0.175 |
| swift | wheel_rear_0.87 | 0.870, 0.380, -1.170 | 0.380 | 0.175 |
| swift | wheel_front_0.87 | 0.870, 0.380, 1.170 | 0.380 | 0.175 |
| tuktuk | wheel_rear_-0.8 | -0.800, 0.380, -1.220 | 0.380 | 0.175 |
| tuktuk | wheel_rear_0.8 | 0.800, 0.380, -1.220 | 0.380 | 0.175 |
| tuktuk | wheel_front_0 | 0.000, 0.380, 1.220 | 0.380 | 0.175 |
| vortex | wheel_rear_-0.87 | -0.870, 0.380, -1.365 | 0.380 | 0.175 |
| vortex | wheel_front_-0.87 | -0.870, 0.380, 1.365 | 0.380 | 0.175 |
| vortex | wheel_rear_0.87 | 0.870, 0.380, -1.365 | 0.380 | 0.175 |
| vortex | wheel_front_0.87 | 0.870, 0.380, 1.365 | 0.380 | 0.175 |

## Blender側の生成コード

出典: `Art/Blender/build_assets.py` の `wheel()`。メッシュの頂点とピボットを同じ中心から生成し、出力時には中心からの相対頂点として保存する。

```python
def wheel(x,y,z,r=.38,thin=.15):
 global GROUP,PIV
 GROUP=('wheel_front_' if z>0 else 'wheel_rear_')+str(x);PIV=(x,y,z)
 # Closed tyre cross-section: Unity X axle; outer radius r; rim inside sidewalls.
 verts=[];faces=[];width=max(thin+.025,r*.28)
 profile=[(-width*.8,r*.62),(-width,r*.84),(-width*.78,r*.97),(-width*.5,r),(width*.5,r),(width*.78,r*.97),(width,r*.84),(width*.8,r*.62)]
 for offset,radius in profile:
  for k in range(32):
   a=k*math.tau/32;verts.append((x+offset,y+math.sin(a)*radius,z+math.cos(a)*radius))
 for j in range(len(profile)):
  for k in range(32):faces.append((j*32+k,j*32+(k+1)%32,((j+1)%len(profile))*32+(k+1)%32,((j+1)%len(profile))*32+k))
 tyre=mesh('Tread',verts,faces,rubber)
 for poly in tyre.data.polygons:poly.use_smooth=True
 beam('Hub',(x-thin*.8,y,z),(x+thin*.8,y,z),r*.24,silver,vertices=16)
 for side in [-1,1]:
  for k in range(7):
   a=k*math.tau/7;beam('Alloy spoke',(x+side*thin*.8,y,z),(x+side*thin*.8,y+math.sin(a)*r*.60,z+math.cos(a)*r*.60),.027,silver,vertices=6)
 GROUP='body';PIV=(0,0,0)
```

四輪の配置に使用している値:

```python
  length=[1,.9,1.05,1.03][index];height=[1,1.10,.9,1.14][index]
   for z in [-1.30*length,1.30*length]:wheel(side*.87,.38,z)
    for side in [-1,1]:wheel(side*.80,.38,z)
```

## Unity側の操舵・回転・接地コード

出典: `Assets/Scripts/RevisionSevenPresentation.cs` の `BlenderWheels`。取付中心は操舵用Transformに保持し、その子でタイヤだけをX軸回転する。半径は実メッシュから求める。接地は現在の道路区間から算出し、22cm以内の表示用補正を行う。

```csharp
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
```

`CoastRacer.cs` の `Place()` では道路中心線の補間高さを使い、車体の表示位置・傾きを適用した後に `UpdateBlenderCar()` と `Ground()` を呼ぶ。座標の二重加算を避け、立体交差では現在区間の近傍だけを参照する。

## 検証

`Art/Blender/validate_wheels.py` で全車種の車輪数、対称性、中心、車軸方向、半径、平地での下端高さを検査。ブラウザーでは正面・側面と左右操舵状態を撮影。実機での確認は未実施。

## Showroom contact

`RacePresentation.cs` places the display cylinder at Y = -0.12 with Y scale 0.12. Unity cylinders have a height of 2, so the top is exactly Y = 0, matching the tyre contact plane. The former centre Y = -0.14 left a 0.02 m gap.
