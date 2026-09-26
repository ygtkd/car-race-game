"""COAST RACER authored Blender assets. Run blender -b --python Art/Blender/build_assets.py.
Native .blend sources remain editable; evaluated meshes are exported to a compact Unity JSON format.
Coordinates are Unity metres (X right, Y up, Z forward), converted at the Blender boundary.
"""
import bpy, math, json, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Resources/Blender';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'Art/Blender/Models';SOURCE.mkdir(exist_ok=True)
random.seed(719)
M={}; GROUP='body'; PIV=(0,0,0)
def b(p): return (p[0],-p[2],p[1])
def u(p): return (p[0],p[2],-p[1])
def mat(name,color,metal=0,rough=.5):
 if name in M:return M[name]
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 bs=next((n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled');m['cr_metal']=metal;m['cr_rough']=rough;bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough;M[name]=m;return m
paint=mat('Ceramic silver',(.5,.59,.64),.65,.22);black=mat('Carbon',(.035,.043,.052),.2,.4);rubber=mat('Rubber',(.022,.026,.03),0,.85);glass=mat('Glass',(.04,.13,.19),.55,.12);silver=mat('Machined alloy',(.64,.7,.75),.85,.22);white=mat('Headlamp',(.92,.94,1),.1,.12);red=mat('Tail lamp',(.7,.025,.015),.2,.2);gold=mat('Gold',(.95,.58,.08),.6,.3);grass=mat('Grass',(.18,.29,.105),0,.96);rock=mat('Granite',(.28,.29,.27),0,.9);wood=mat('Bark',(.18,.095,.045),0,.9);leaves=mat('Leaves',(.09,.23,.065),0,.9);sand=mat('Sand',(.66,.58,.38),0,.85);concrete=mat('Concrete',(.40,.43,.45),0,.9)
def reset():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 for mesh in list(bpy.data.meshes):
  if mesh.users==0:bpy.data.meshes.remove(mesh)
def tag(o,name,m):
 o.name=name;o.data.materials.append(m);o['group']=GROUP;o['pivot']=PIV
 return o
def box(name,p,size,m,bev=.05):
 x,y,z=p;dx,dy,dz=[n*.5 for n in size]
 v=[(x+a*dx,y+b*dy,z+c*dz) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
 o=mesh(name,v,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)],m)
 # Keep an object-local origin so subsequent gantry rotation pivots at its centre.
 origin=Vector(b(p))
 for vertex in o.data.vertices:vertex.co-=origin
 o.location=origin
 if bev:
  mod=o.modifiers.new('Crafted edge bevel','BEVEL');mod.width=min(bev,min(size)*.24);mod.segments=2
 return o

def sphere(name,p,size,m,segments=20,rings=10):
 v=[];f=[]
 for j in range(rings+1):
  a=math.pi*j/rings
  for k in range(segments):
   t=k*math.tau/segments;v.append((p[0]+math.sin(a)*math.cos(t)*size[0],p[1]+math.cos(a)*size[1],p[2]+math.sin(a)*math.sin(t)*size[2]))
 for j in range(rings):
  for k in range(segments):f.append((j*segments+k,j*segments+(k+1)%segments,(j+1)*segments+(k+1)%segments,(j+1)*segments+k))
 o=mesh(name,v,f,m)
 for poly in o.data.polygons:poly.use_smooth=True
 return o

def beam(name,a,c,r,m,r2=None,vertices=10):
 a=Vector(a);c=Vector(c);d=(c-a).normalized();u=d.cross(Vector((0,1,0)) if abs(d.y)<.9 else Vector((1,0,0))).normalized();v=d.cross(u);points=[]
 for centre,radius in [(a,r),(c,r if r2 is None else r2)]:
  for k in range(vertices):t=k*math.tau/vertices;points.append(tuple(centre+(u*math.cos(t)+v*math.sin(t))*radius))
 faces=[tuple(reversed(range(vertices))),tuple(range(vertices,vertices*2))]
 for k in range(vertices):faces.append((k,(k+1)%vertices,(k+1)%vertices+vertices,k+vertices))
 return mesh(name,points,faces,m)

def mesh(name,vertices,faces,m):
 data=bpy.data.meshes.new(name);data.from_pydata([b(p) for p in vertices],[],faces);data.update();o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);return tag(o,name,m)
def hull(name,sections,m):
 verts=[]
 for z,w,h in sections:
  verts.extend([(-w*.83,.27,z),(-w,.43,z),(-w,h-.12,z),(-w*.76,h,z),(w*.76,h,z),(w,h-.12,z),(w,.43,z),(w*.83,.27,z)])
 faces=[tuple(reversed(range(8)))];n=len(sections)
 for k in range(n-1):
  for j in range(8):faces.append((k*8+j,k*8+(j+1)%8,(k+1)*8+(j+1)%8,(k+1)*8+j))
 faces.append(tuple(range((n-1)*8,n*8)));o=mesh(name,verts,faces,m);mod=o.modifiers.new('Coachwork bevel','BEVEL');mod.width=.07;mod.segments=3
 for poly in o.data.polygons:poly.use_smooth=True
 return o
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
def write_runtime(name,parts):
 import struct,array
 with (OUT/(name+'.bytes')).open('wb') as output:
  output.write(struct.pack('<II',0x37425243,len(parts)))
  for part in parts:
   label=part['name'].encode('utf-8');output.write(struct.pack('<H',len(label)));output.write(label)
   output.write(struct.pack('<9f',*part['pivot'],*part['color'],part['metal'],part['smooth']))
   output.write(struct.pack('<II',len(part['v'])//3,len(part['t'])))
   for key,kind in [('v','f'),('n','f'),('t','I')]:array.array(kind,part[key]).tofile(output)

def export(name):
 bpy.context.preferences.filepaths.save_version=0
 bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/(name+'.blend')),compress=True)
 deps=bpy.context.evaluated_depsgraph_get();groups={}
 for o in bpy.context.scene.objects:
  if o.type!='MESH':continue
  evaluated=o.evaluated_get(deps);me=evaluated.to_mesh();me.calc_loop_triangles();group=o.get('group','body');pivot=tuple(o.get('pivot',(0,0,0)))
  normals=o.matrix_world.to_3x3().inverted().transposed()
  for mi,m in enumerate(me.materials):
   if m is None:m=paint
   key=(group,m.name);g=groups.setdefault(key,dict(name=group,pivot=list(pivot),color=list(m.diffuse_color),metal=float(m.get('cr_metal',0)),smooth=1-float(m.get('cr_rough',.5)),v=[],n=[],t=[]));lookup={}
   # Export face-corner normals: cube faces stay flat and bevels stay smooth.
   for tri in me.loop_triangles:
    if tri.material_index!=mi:continue
    indices=[]
    for li in tri.loops:
     vi=me.loops[li].vertex_index;p=u(o.matrix_world@me.vertices[vi].co);normal=u((normals@me.corner_normals[li].vector).normalized())
     vertex=tuple(round(p[i]-pivot[i],5) for i in range(3));normal=tuple(round(n,5) for n in normal);vk=(vertex,normal)
     if vk not in lookup:lookup[vk]=len(g['v'])//3;g['v'].extend(vertex);g['n'].extend(normal)
     indices.append(lookup[vk])
    g['t'].extend([indices[0],indices[2],indices[1]])
  evaluated.to_mesh_clear()
 result=dict(parts=list(groups.values()));write_runtime(name,result['parts']);print('ASSET_OK',name,sum(len(g['t'])//3 for g in groups.values()),flush=True)
def vehicle(vid,index):
 global GROUP,PIV
 reset();color=[(.54,.63,.69),(.60,.07,.025),(.035,.15,.29),(.36,.29,.10),(.64,.19,.04),(.95,.65,.03),(.02,.43,.34),(.49,.06,.035),(.34,.06,.49),(.025,.40,.62)][index];body=mat('Paint '+vid,color,.65,.2)
 if index<4:
  length=[1,.9,1.05,1.03][index];height=[1,1.10,.9,1.14][index]
  hull('Sculpted body',[(-2.15*length,.78,.70),(-1.75*length,.98,.96),(-1.0*length,.97,1.0),(.6*length,.94,.88),(1.5*length,.90,.81),(2.15*length,.78,.62)],body)
  hull('Cabin silhouette',[(-1.2*length,.72,1.02),(-.72*length,.69,1.48*height),(.35*length,.65,1.44*height),(.96*length,.75,.86)],glass)
  box('Roof panel',(0,1.46*height,-.22*length),(1.27,.04,1.0*length),body)
  for side in [-1,1]:
   box('Side sill',(side*.84,.30,0),(.09,.12,2.35),black);box('Mirror',(side*1.04,1.05,.65),(.27,.14,.35),body)
   for z in [-1.30*length,1.30*length]:wheel(side*.87,.38,z)
   box('Headlamp',(side*.62,.74,2.11*length),(.45,.12,.09),white);box('Tail lamp',(side*.64,.75,-2.11*length),(.48,.10,.07),red)
   beam('Exhaust',(side*.65,.36,-2),(side*.65,.36,-2.25),.07,silver)
   box('Door handle',(side*.977,.86,-.28),(.025,.045,.22),silver)
  box('Front grille',(0,.48,2.12*length),(1.3,.20,.06),black);box('Splitter',(0,.27,2.05*length),(1.60,.08,.3),black)
  for side in [-1,1]:box('Wing mount',(side*.6,1.0,-1.86*length),(.07,.45,.13),black)
  box('Aerodynamic wing',(0,1.23,-1.9*length),(2.02,.09,.44),body)
 elif vid=='banana':
  # One curved inflatable hull, raised ends, moulded seats and rope handles.
  verts=[]
  for j in range(25):
   z=-2.2+j*4.4/24;end=abs(z)/2.2;r=.46*max(.10,1-end**6);cy=.63+end**4*.8
   for k in range(20):a=k*math.tau/20;verts.append((math.cos(a)*r,cy+math.sin(a)*r,z))
  faces=[]
  for j in range(24):
   for k in range(20):faces.append((j*20+k,j*20+(k+1)%20,(j+1)*20+(k+1)%20,(j+1)*20+k))
  o=mesh('Single banana hull',verts,faces,body)
  for poly in o.data.polygons:poly.use_smooth=True
  for z in [-1,0,1]:box('Grip saddle',(0,1.02,z),(.57,.15,.48),black);beam('Handle',( -.34,1.2,z+.22),(.34,1.2,z+.22),.045,silver)
  box('Bow headlamp',(0,.99,1.84),(.20,.11,.07),white);box('Stern lamp',(0,.99,-1.84),(.16,.10,.07),red)
  for side in [-1,1]:beam('Safety rope',(side*.47,.69,-1.5),(side*.47,.69,1.5),.03,black)
 elif vid in ['bicycle','stormbike','aerobike']:
  motor=vid!='bicycle';wheel(0,.55,-1.27,.55,.10);wheel(0,.55,1.27,.55,.10)
  for a,c in [((0,.58,-1.27),(0,1.32,-.1)),((0,1.32,-.1),(0,.48,.15)),((0,.48,.15),(0,.58,-1.27)),((0,.48,.15),(0,1.4,.88)),((0,1.4,.88),(0,1.32,-.1))]:beam('Tubular frame',a,c,.075,body)
  box('Saddle',(0,1.42,-.4),(.40,.13,.68),black)
  GROUP='steer_front';PIV=(0,.55,1.27)
  for side in [-1,1]:beam('Fork',(side*.14,.55,1.27),(side*.14,1.55,.80),.055,silver)
  beam('Handlebar',(-.48,1.63,.88),(.48,1.63,.88),.04,black)
  GROUP='body';PIV=(0,0,0)
  if motor:
   sphere('Fuel tank',(0,1.1,.18),(.38,.34,.55),body);box('Engine',(0,.64,-.1),(.54,.43,.66),silver)
   for h in range(5):box('Cooling fin',(0,.50+h*.06,-.12),(.60,.025,.64),black,0)
   beam('Exhaust muffler',(.37,.58,-.38),(.37,.69,-1.23),.115,silver)
   sphere('Front fairing',(0,1.18,.84),(.37,.40,.34),body);box('Windscreen',(0,1.52,.92),(.50,.4,.035),glass)
   if vid=='aerobike':
    for side in [-1,1]:mesh('Aerodynamic side fairing',[(side*.16,.45,.95),(side*.40,.7,.42),(side*.34,1.1,.7),(side*.17,1.5,1.07)],[(0,1,2,3)],body)
   else:
    for side in [-1,1]:box('Storm saddle bag',(side*.38,.90,-.80),(.27,.35,.65),black,.09)
  sphere('Headlamp',(0,1.3,1.18),(.16,.12,.05),white);box('Tail lamp',(0,1.22,-1),(.28,.08,.07),red)
 else:
  van=vid=='kebab';box('Chassis',(0,.52,0),(1.48,.32,3.2),body);box('Cabin',(0,1.1,1),(1.58,1.25,1.12),body,.17);box('Windshield',(0,1.32,1.58),(1.32,.64,.04),glass)
  if van:
   box('Kitchen',(0,1.31,-.8),(1.72,1.50,2),mat('Kitchen enamel',(.74,.69,.51)),.13);box('Serving hatch',(-.88,1.46,-.62),(.035,.69,1.34),glass)
   for j in range(7):box('Awning stripe',(-1.15,1.97,-1.45+j*.25),(.69,.065,.25),red if j%2 else white)
   beam('Rotisserie',(0,2.03,-.68),(0,3.05,-.68),.045,silver);sphere('Roasted kebab',(0,2.51,-.68),(.35,.45,.35),mat('Kebab roast',(.37,.13,.045)))
  else:
   box('Bench',(0,.98,-.75),(1.44,.24,.74),black);box('Roof canopy',(0,2.01,-.15),(1.83,.15,3.22),mat('Canopy',(.81,.62,.13)),.12)
   for side in [-1,1]:beam('Canopy support',(side*.77,.66,-1.42),(side*.77,1.99,-1.42),.045,silver)
  for z in [-1.22,1.22]:
   if not van and z>0:wheel(0,.38,z)
   else:
    for side in [-1,1]:wheel(side*.80,.38,z)
  for side in [-1,1]:box('Headlamp',(side*.59,.94,1.59),(.26,.19,.07),white);box('Tail lamp',(side*.63,.76,-1.75),(.2,.17,.06),red)
 # Cut real wheel wells into the lower body rather than covering tires with a solid hull.
 if index<4 or vid=='kebab':
  targets=[o for o in bpy.context.scene.objects if o.type=='MESH' and (o.name.startswith(('Sculpted body','Cabin silhouette')) or o.name in ['Chassis','Kitchen','Cabin'])]
  wheel_pivots={tuple(o.get('pivot',(0,0,0))) for o in bpy.context.scene.objects if str(o.get('group','')).startswith('wheel_')}
  for pivot in wheel_pivots:
   x,y,z=pivot
   bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=.48,depth=.85,location=b((x,y,z)),rotation=(0,math.pi/2,0));cutter=bpy.context.object;cutter.data.materials.append(body)
   for target in targets:
    bpy.context.view_layer.objects.active=target;mod=target.modifiers.new('Wheel arch clearance','BOOLEAN');mod.operation='DIFFERENCE';mod.object=cutter;bpy.ops.object.modifier_apply(modifier=mod.name)
   bpy.data.objects.remove(cutter,do_unlink=True)
 export('vehicle_'+vid)
def tree(p,seed,palm=False,crowns=9):
 rng=random.Random(seed);x,y,z=p;h=rng.uniform(7,12);beam('Tree trunk',(x,y,z),(x+.3,y+h,z),.28,wood,.08)
 if palm:
  for j in range(7):
   a=j*math.tau/7;dx=math.cos(a);dz=math.sin(a);mesh('Palm frond',[(x,y+h,z),(x+dx*2-dz*.5,y+h+.7,z+dz*2+dx*.5),(x+dx*5,y+h-.5,z+dz*5),(x+dx*2+dz*.5,y+h+.7,z+dz*2-dx*.5)],[(0,1,2,3)],leaves)
 else:
  for j in range(crowns):
   a=j*2.399;d=rng.uniform(1.2,2.4);tip=(x+math.cos(a)*d,y+h*(.50+j*.36/max(1,crowns-1)),z+math.sin(a)*d)
   beam('Branch',(x,y+h*.38,z),tip,.10,wood,.018)
   foliage=mat('Foliage '+str(j%4),[(.14,.25,.075),(.18,.29,.085),(.105,.21,.055),(.21,.31,.10)][j%4],0,1)
   o=sphere('Irregular leaf canopy',tip,(rng.uniform(1.8,2.6),rng.uniform(1.6,2.5),rng.uniform(1.8,2.6)),foliage,10 if crowns==9 else 8,6 if crowns==9 else 4);centre=Vector(b(tip))
   for v in o.data.vertices:v.co=centre+(v.co-centre)*rng.uniform(.84,1.12)
   for poly in o.data.polygons:poly.use_smooth=False

def course(track):
 global GROUP,PIV
 print('BUILD_COURSE',track['id'],flush=True)
 reset();pts=[(p['x'],p['y'],p['z']) for p in track['points']];N=len(pts);bridges=[any(track['bridges'][(i+d)%N] for d in range(-3,4)) for i in range(N)];id=track['id'];asphalt=mat('Asphalt',(.13,.145,.16),0,.92);runoff=mat('Runoff',(.33,.34,.32),0,.91)
 if id=='shonan':bridges=[(-395<p[2]<-85 and abs(p[0])<15) for p in pts]
 def right(i):
  a=pts[(i-1)%N];c=pts[(i+1)%N];d=Vector((c[0]-a[0],0,c[2]-a[2])).normalized();return Vector((d.z,0,-d.x))
 def strip(name,lo,hi,lift,m,sel=None):
  v=[];f=[]
  for i in range(N):
   if sel and not sel(i):continue
   j=(i+1)%N;k=len(v)
   for q,r in [(i,lo),(i,hi),(j,lo),(j,hi)]:
    if id=='shonan' and bridges[q]:r=max(-8.5,min(8.5,r))
    v.append(tuple(Vector(pts[q])+right(q)*r+Vector((0,lift,0))))
   f.extend([(k,k+2,k+1),(k+1,k+2,k+3)])
  return mesh(name,v,f,m)
 chalk=mat('Road paint white',(.85,.86,.84),0,.96);stripe=mat('Road paint red',(.59,.05,.035),0,.95);ink=mat('Road paint black',(.035,.039,.044),0,.97)
 strip('Road',-7,7,0,asphalt)
 for side in [-1,1]:
  lo,hi=(-16,-8.1) if side<0 else (8.1,16);strip('Grey shoulder',lo,hi,-.055,runoff)
  lo,hi=(-8.1,-7) if side<0 else (7,8.1);strip('White kerb',lo,hi,.012,chalk);strip('Red kerb',lo,hi,.027,stripe,lambda i:i%8<4)
  strip('Edge line',side*6.84-.06,side*6.84+.06,.025,chalk)
 # One continuous, non-overlapping terrain surface. Lower roads take priority at crossings.
 import functools
 @functools.lru_cache(maxsize=100000)
 def road_sample(x,z):
  nearest=sorted(range(N),key=lambda i:(pts[i][0]-x)**2+(pts[i][2]-z)**2)[:24]
  candidates=[]
  for i in nearest:
   for k in [(i-1)%N,i]:
    a=pts[k];c=pts[(k+1)%N];dx=c[0]-a[0];dz=c[2]-a[2];f=max(0,min(1,((x-a[0])*dx+(z-a[2])*dz)/max(.001,dx*dx+dz*dz)))
    d=math.hypot(x-a[0]-f*dx,z-a[2]-f*dz);h=a[1]+f*(c[1]-a[1]);candidates.append((d,h,k))
  candidates.sort();d,h,k=candidates[0]
  lower=[r for r in candidates if r[0]<30 and not bridges[r[2]]]
  if lower:d,h,k=min(lower,key=lambda r:r[1])
  return d,h,k
 def raw_height(x,z):
  d,h,k=road_sample(x,z)
  if bridges[k]:
   ground=[p for i,p in enumerate(pts) if not bridges[i] and p[1]<h-4]
   h=min(ground,key=lambda p:(p[0]-x)**2+(p[2]-z)**2)[1] if ground else h-8
  # A broad verge meets the road; the distant landscape changes gradually.
  blend=max(0,min(1,(d-28)/100));blend=blend*blend*(3-2*blend)
  if id=='shonan':
   island=(x/210)**2+((z+615)/180)**2
   far=78*math.exp(-island*1.15)-5 if z<-80 else 4
   hillblend=max(0,min(1,(d-24)/55));hillblend=hillblend*hillblend*(3-2*hillblend)
   result=(h-.75)*(1-hillblend)+far*hillblend
   channel=min((z+430)/20,(-65-z)/20);water=max(0,min(1,channel));water=water*water*(3-2*water)
   return result*(1-water)-4*water
  hill=(9+9*math.sin(x*.012)*math.cos(z*.015)) if id=='ridge' else -2
  return h-.75+hill*blend
 minx=min(p[0] for p in pts)-150;maxx=max(p[0] for p in pts)+150;minz=min(p[2] for p in pts)-150;maxz=max(p[2] for p in pts)+150
 spacing=13 if id=="shonan" else 7;nx=math.ceil((maxx-minx)/spacing);nz=math.ceil((maxz-minz)/spacing);sx=(maxx-minx)/nx;sz=(maxz-minz)/nz
 v=[];f=[]
 for iz in range(nz+1):
  for ix in range(nx+1):
   x=minx+sx*ix;z=minz+sz*iz;v.append((x,raw_height(x,z),z))
   if ix<nx and iz<nz:k=iz*(nx+1)+ix;f.extend([(k,k+nx+1,k+1),(k+1,k+nx+1,k+nx+2)])
 print('TERRAIN_READY',id,len(v),flush=True)
 terrain=mesh('Continuous landscape',v,f,grass)
 if id=='shonan':terrain.data.materials.append(sand)
 for poly in terrain.data.polygons:
  poly.use_smooth=True
  # Beach colour is blended continuously by world height in the Unity material.
 def terrain_height(x,z):
  fx=max(0,min(nx-.00001,(x-minx)/sx));fz=max(0,min(nz-.00001,(z-minz)/sz));ix=int(fx);iz=int(fz);u=fx-ix;w=fz-iz;k=iz*(nx+1)+ix
  a,b,c,d=[v[n][1] for n in [k,k+1,k+nx+1,k+nx+2]]
  return a+(b-a)*u+(c-a)*w if u+w<=1 else d+(c-d)*(1-u)+(b-d)*(1-w)
 for side in [-1,1]:
  verts=[];faces=[]
  for i in range(N):
   j=(i+1)%N
   if bridges[i] or bridges[j]:continue
   k=len(verts)
   for q in [i,j]:
    for width in [16,21,28]:
     p=Vector(pts[q])+right(q)*width*side;d,h,foreign=road_sample(p.x,p.z)
     p.y=pts[q][1]-.08 if width==16 else terrain_height(p.x,p.z)-.03
     # Never lay an upper approach embankment over a lower road.
     if width>16 and d<17 and h<pts[q][1]-3:p.y=min(p.y,h-.9)
     verts.append(tuple(p))
   for r in range(2):faces.append((k+r,k+3+r,k+4+r,k+1+r))
  def covers_road(point):
   x,y,z=point;nearest=sorted(range(N),key=lambda k:(pts[k][0]-x)**2+(pts[k][2]-z)**2)[:24]
   for k in set(nearest+[(n-1)%N for n in nearest]):
    a=pts[k];c=pts[(k+1)%N];dx=c[0]-a[0];dz=c[2]-a[2];u=max(0,min(1,((x-a[0])*dx+(z-a[2])*dz)/max(.001,dx*dx+dz*dz)))
    if (x-a[0]-dx*u)**2+(z-a[2]-dz*u)**2<16.25**2 and y>a[1]+(c[1]-a[1])*u-.04:return True
   return False
  trimmed=[]
  for face in faces:
   corners=[verts[k] for k in face];probes=[]
   for u in [0,.25,.5,.75,1]:
    for w in [0,.25,.5,.75,1]:probes.append(tuple(corners[0][a]*(1-u)*(1-w)+corners[1][a]*u*(1-w)+corners[2][a]*u*w+corners[3][a]*(1-u)*w for a in range(3)))
   if not any(covers_road(p) for p in probes):trimmed.append(face)
  mesh('Graded road verge',verts,trimmed,grass)
 for i in range(N):
  if bridges[i]:
   j=(i+1)%N;a=Vector(pts[i]);c=Vector(pts[j]);r=right(i)*(8.5 if id=='shonan' else 16);mesh('Concrete bridge deck',[tuple(a-r),tuple(a+r),tuple(c-r),tuple(c+r),tuple(a-r-Vector((0,1.1,0))),tuple(a+r-Vector((0,1.1,0))),tuple(c-r-Vector((0,1.1,0))),tuple(c+r-Vector((0,1.1,0)))],[(4,5,7,6),(0,4,6,2),(1,3,7,5)],concrete)
   if i%8==0 and id!='shonan':
    for side in [-1,1]:
     p=a+right(i)*side*12
     if all(q[1]>a.y-4 or (p.x-q[0])**2+(p.z-q[2])**2>24**2 for q in pts):
      ground=terrain_height(p.x,p.z)-.5;top=a.y-1.1;box('Bridge pier',(p.x,(ground+top)/2,p.z),(1.6,top-ground,1.8),concrete)
  if i%4==0:
   for side in [-1,1]:
    p=Vector(pts[i])+right(i)*side*(8.5 if id=='shonan' and bridges[i] else 17.4)
    if any(abs(k-i)>14 and abs(k-i)<N-14 and (p.x-q[0])**2+(p.z-q[2])**2<18**2 and abs(p.y-q[1])<4 for k,q in enumerate(pts)):continue
    q=Vector(pts[(i+4)%N])+right((i+4)%N)*side*(8.5 if id=='shonan' and bridges[i] else 17.4)
    beam('Safety rail',tuple(p+Vector((0,.75,0))),tuple(q+Vector((0,.75,0))),.12,silver,vertices=6)
    beam('Rail post',tuple(p-Vector((0,.8,0))),tuple(p+Vector((0,.82,0))),.09,concrete,vertices=6)
 def clear(x,z,r=8):return all((x-p[0])**2+(z-p[2])**2>(24+r)**2 for p in pts[::2])
 for i in range(0,N,12):
  for side in [-1,1]:
   p=Vector(pts[i])+right(i)*side*42
   if not clear(p.x,p.z,6):continue
   p.y=terrain_height(p.x,p.z)-.5
   if p.y<-1:continue
   tree(tuple(p),i+side*31,id=='shonan')
   if id=='ridge':
    p+=right(i)*side*15;p.y=terrain_height(p.x,p.z)-1.2;sphere('Weathered rock',tuple(p+Vector((0,2,0))),(5,3,4),rock,9,5)
  if id=='suzuka' and i%36==0:
   p=Vector(pts[i])+right(i)*57
   if clear(p.x,p.z,25):
    p.y=min(terrain_height(p.x+dx,p.z+dz) for dx in [-10,10] for dz in [-7,7])-.5
    box('Grandstand foundation',(p.x,p.y+.2,p.z+4),(22,2,15),concrete)
    for k in range(5):box('Grandstand seats',(p.x,p.y+1+k*.6,p.z+k*2),(18,1,2),red if k%2 else white)
    for x in [-9,9]:beam('Canopy column',(p.x+x,p.y,p.z+5),(p.x+x,p.y+7,p.z+5),.18,silver)
    box('Grandstand roof',(p.x,p.y+7,p.z+4),(21,.4,13),silver)
 if id=='shonan':
  box('Ocean',((minx+maxx)/2,-2,(minz+maxz)/2),(maxx-minx+600,.15,maxz-minz+600),mat('Sea',(.03,.27,.34),.1,.18),0)
  # Enoshima-inspired tower at the centre of the island; route surrounds it.
  x,z=0,-615;y=terrain_height(x,z)
  box('Sea Candle plaza',(x,y+.3,z),(32,.6,32),concrete)
  beam('Sea Candle shaft',(x,y,z),(x,y+42,z),2.4,white,1.6,20)
  beam('Sea Candle observation',(x,y+36,z),(x,y+42,z),7,glass,6.5,24)
  for h,r in [(35,7.5),(42,7.5),(44,4)]:beam('Sea Candle gallery',(x,y+h,z),(x,y+h+.7,z),r,white,vertices=24)
  beam('Sea Candle lantern',(x,y+44,z),(x,y+50,z),1.7,gold,1,16)
  for k in range(8):
   a=k*math.tau/8;beam('Sea Candle bracing',(math.cos(a)*6,y+3,z+math.sin(a)*6),(math.cos(a)*2,y+36,z+math.sin(a)*2),.20,white)
  # Common median separates the two traversals of the same bridge.
  box('Bridge central divider',(0,7.38,-240),(1.8,.76,310),concrete,.08)
  for zz in range(-380,-90,40):
   box('Bridge crossbeam',(0,5.5,zz),(37,1.4,2.0),concrete)
   for xx in [-15,15]:box('Sea bridge pier',(xx,1.5,zz),(2,8,2.4),concrete)
  # Tapered angular Eboshi rock silhouette offshore of the westbound seafront.
  x,z=-465,-165
  mesh('Eboshi rock',[(x-13,-2,z-8),(x+13,-2,z-8),(x+9,-2,z+9),(x-10,-2,z+11),(x-6,9,z-6),(x+5,12,z-4),(x+2,25,z),(x-5,22,z+3)],[(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],rock)
  for j in range(18):
   x=-600+j*32;z=385+(j%2)*35
   if clear(x,z,13):
    y=terrain_height(x,z);box('Shonan townhouse',(x,y+4,z),(17,8,13),mat('Coast house '+str(j%3),[(.75,.70,.59),(.51,.60,.61),(.67,.42,.30)][j%3]));box('House roof',(x,y+8.3,z),(19,.6,15),red)
    for q in [-1,1]:box('Townhouse window',(x+q*4,y+4,z-6.6),(3,3,.1),glass)
  for j in range(0,N,10):
   if bridges[j]:continue
   for side in [-1,1]:
    p=Vector(pts[j])+right(j)*side*(36+(j%3)*9)
    if not clear(p.x,p.z,5):continue
    ground=terrain_height(p.x,p.z)
    if ground<1:continue
    if p.z<-430:
     if j%20==0:tree((p.x,ground-.25,p.z),900+j+side,False,crowns=3)
     else:
      sphere('Island shrub',(p.x,ground+.5,p.z),(2.2,1.2,1.8),leaves,8,4)
      box('Island stone wall',(p.x,ground+.5,p.z+3),(6,1.6,1),rock,0)
    elif p.z>40:
     bottom=min(terrain_height(p.x+dx,p.z+dz) for dx in [-4,4] for dz in [-3,3])-.3
     box('Coastal shop foundation',(p.x,(ground+bottom)/2,p.z),(9,max(.5,ground-bottom),7),concrete,0)
     box('Coastal shop',(p.x,ground+2.5,p.z),(8,5,6),mat('Shop plaster '+str(j%3),[(.78,.69,.50),(.65,.72,.71),(.72,.48,.34)][j%3]),0)
     box('Shop roof',(p.x,ground+5.15,p.z),(9,.3,7),concrete,0)
     box('Shop window',(p.x,ground+2,p.z-3.03),(4,2,.08),glass,0)
  # Scenic railway is separate from the racing road. Cars animate together as one pivot group.
  for zz in [363.5,366.5]:beam('Enoden rail',(-605,11,zz),(-180,11,zz),.09,silver,vertices=6)
  for xx in range(-605,-175,5):
   ground=min(terrain_height(xx,362),terrain_height(xx,368));box('Railway embankment',(xx,(ground+10.65)/2,365),(5.1,max(.3,10.65-ground),6),rock,0);box('Railway sleeper',(xx,10.8,365),(.8,.3,4.5),wood,0)
  GROUP='train';PIV=(-440,11,365)
  for xx in [-450,-432]:
   box('Enoden green carriage',(xx,13,365),(16,3.4,3.2),mat('Enoden green',(.045,.25,.13)),.3)
   box('Enoden cream waist',(xx,13.1,365),(16.03,.65,3.25),mat('Enoden cream',(.83,.78,.57)),.04)
   box('Enoden silver roof',(xx,14.9,365),(16.2,.4,3.3),silver,.15)
   for side in [-1,1]:
    for k in range(6):box('Enoden window',(xx-6+k*2.4,14,365+side*1.63),(1.8,1.15,.04),glass,.05)
   for offset in [-5,5]:beam('Train axle',(xx+offset,11.5,363.6),(xx+offset,11.5,366.4),.5,black,vertices=10)
  GROUP='body';PIV=(0,0,0)
 if id=='ridge':
  # Short open-ended tunnel on the existing climb; no changes to the road itself.
  verts=[];faces=[]
  for index in range(36,61):
   a=Vector(pts[index]);r=right(index)
   for k in range(13):
    angle=k*math.pi/12;verts.append(tuple(a+r*(math.cos(angle)*19)+Vector((0,math.sin(angle)*10,0))))
  for j in range(24):
   for k in range(12):n=j*13+k;faces.append((n,n+1,n+14,n+13))
  shell=mesh('Mountain tunnel vault',verts,faces,concrete);mod=shell.modifiers.new('Tunnel shell thickness','SOLIDIFY');mod.thickness=.65
  for index in range(38,60,5):
   a=Vector(pts[index]);r=right(index)
   for side in [-1,1]:box('Tunnel lamp',tuple(a+r*side*14+Vector((0,6,0))),(1,.25,2),white)
 # Start arch and checkered tape share the exact simulation finish plane.
 a=Vector(pts[0]);r=right(0);forward=Vector(( -r.z,0,r.x))
 for side in [-1,1]:p=a+r*side*18;beam('Finish gantry',tuple(p),tuple(p+Vector((0,7,0))),.35,silver)
 for k in range(36):
  p=a+r*(k-17.5)+Vector((0,7,0));o=box('Finish tape',tuple(p),(1,.9,.25),chalk if k%2 else ink,0);o.rotation_euler[2]=math.atan2(-r.z,r.x)
 for k in range(32):
  for j in range(2):p=a+r*(k-15.5)+forward*(j-.5)+Vector((0,.048,0));o=box('Finish line',tuple(p),(1,.025,1),chalk if (k+j)%2 else ink,0);o.rotation_euler[2]=math.atan2(-r.z,r.x)
 print('SCENERY_READY',id,len(bpy.context.scene.objects),flush=True)
 export('course_'+id)

def item_asset(item):
 reset();i=item['index'];id=item['id'];kind=item['name'];accent=mat('Reward enamel '+id,[(.9,.58,.10),(.12,.47,.72),(.62,.12,.10),(.12,.58,.35)][i%4],.55,.25)
 box('Mount',(0,.025,0),(.30,.05,.30),black,.04)
 if any(x in kind for x in ['翼','ウイング','フィン']):
  for side in [-1,1]:
   for k in range(5):
    x=side*(.25+k*.12);mesh('Feather',[(side*.05,.1,-.2),(x,.12+k*.10,-.23),(side*(.85-k*.09),.25+k*.1,.20),(side*.13,.1,.24)],[(0,1,2,3)],accent)
  if id=='nightExpert':beam('Lighthouse',(0,.05,0),(0,.70,0),.08,white,.05);sphere('Beacon',(0,.73,0),(.13,.09,.13),gold)
 elif any(x in kind for x in ['旗','フラッグ']):
  beam('Flagpole',(0,.03,0),(0,1.12,0),.025,silver)
  mesh('Waving cloth',[(0,1.1,0),(.65,1.02,.06),(.63,.63,-.03),(0,.67,0)],[(0,1,2,3)],accent)
  for k in range(3):box('Flag emblem',(.16+k*.16,.87,.05),(.09,.12,.035),white,0)
 elif id=='specials20':
  beam('Antenna stem',(0,.03,0),(0,.70,0),.025,silver)
  mesh('Lightning antenna',[(0,.95,0),(-.20,.62,0),(-.02,.62,0),(-.08,.32,0),(.25,.75,0),(.06,.75,0)],[(0,1,2,3,4,5)],gold)
 elif 'ロケット' in kind or 'ジェット' in kind:
  beam('Rocket body',(0,.1,0),(0,.73,0),.17,accent,vertices=16);beam('Nose',(0,.73,0),(0,.99,0),.17,silver,0,16)
  for side in [-1,1]:mesh('Rocket fin',[(side*.1,.12,0),(side*.4,.08,0),(side*.14,.5,0)],[(0,1,2)],red)
 elif 'タコ' in kind:
  sphere('Octopus head',(0,.49,0),(.35,.38,.30),accent)
  for k in range(8):
   a=k*math.tau/8;beam('Tentacle',(0,.25,0),(math.cos(a)*.65,.08,math.sin(a)*.65),.085,accent,.02)
  for side in [-1,1]:sphere('Eye',(side*.12,.55,.27),(.075,.09,.035),white);sphere('Pupil',(side*.12,.55,.3),(.027,.04,.02),black)
 elif '冠' in kind:
  beam('Crown band',(0,.06,0),(0,.18,0),.32,gold,vertices=20)
  for k in range(7):a=k*math.tau/7;beam('Crown point',(math.cos(a)*.28,.16,math.sin(a)*.28),(math.cos(a)*.31,.50,math.sin(a)*.31),.08,gold,0,6)
 elif 'トロフィー' in kind:
  beam('Trophy stem',(0,.1,0),(0,.55,0),.06,gold);sphere('Trophy cup',(0,.65,0),(.30,.23,.30),gold)
  for side in [-1,1]:beam('Cup handle',(side*.2,.52,0),(side*.4,.78,0),.04,gold)
 elif '雲' in kind:
  for x in [-.25,0,.25]:sphere('Storm cloud',(x,.4,0),(.24,.20,.20),silver)
  mesh('Lightning',[(0,.35,0),(-.1,.1,0),(.02,.1,0),(-.03,-.05,0),(.22,.22,0),(.08,.22,0)],[(0,1,2,3,4,5)],gold)
 elif '帽' in kind or 'キャップ' in kind:
  beam('Hat',(0,.06,0),(.12,.65,0),.32,accent,.03,20);sphere('Pom pom',(.12,.67,0),(.10,.1,.1),white)
 elif 'ヘルメット' in kind:
  sphere('Helmet',(0,.25,0),(.30,.28,.34),accent);box('Visor',(0,.27,.29),(.49,.15,.06),glass,.07)
 elif 'サーフ' in kind:
  sphere('Surfboard',(0,.10,0),(.24,.06,.8),accent);box('Deck stripe',(0,.16,0),(.065,.015,1.35),white,.02)
 elif '箱' in kind or 'トランク' in kind or '袋' in kind:
  box('Reward chest',(0,.23,0),(.65,.4,.4),accent,.12)
  for side in [-1,1]:box('Metal strap',(side*.22,.24,0),(.055,.42,.42),gold,.015)
  box('Lock',(0,.25,.22),(.10,.12,.04),gold)
 elif 'マント' in kind:
  mesh('Flowing cape',[(-.22,.5,0),(.22,.5,0),(.68,.07,-.85),(-.68,.07,-.85)],[(0,1,2,3)],accent)
  for k in range(5):sphere('Star',(math.sin(k)*.3,.22,-.4),(.035,.025,.035),gold,8,4)
 else:
  # Distinct medal silhouettes and motifs: compass, shell, moon, star, checkers, thunder.
  beam('Enamel medallion',(0,.04,0),(0,.09,0),.32,gold,vertices=6+i%5)
  beam('Inset',(0,.09,0),(0,.105,0),.27,accent,vertices=6+i%5)
  count=4+i%6
  for k in range(count):
   a=k*math.tau/count;mesh('Raised emblem',[(0,.125,0),(math.cos(a)*.23,.125,math.sin(a)*.23),(math.cos(a+.30)*.11,.125,math.sin(a+.30)*.11)],[(0,1,2)],white)
  if id=='night1':
   for k in range(14):
    a=math.pi*.25+k*math.pi*1.5/13;box('Crescent moon',(math.cos(a)*.19,.15,math.sin(a)*.19),(.085,.05,.085),gold,.02)
  elif id=='explorer':
   for k in range(4):
    a=k*math.pi/2;mesh('Compass needle',[(0,.155,0),(math.cos(a)*.25,.155,math.sin(a)*.25),(math.cos(a+.4)*.1,.155,math.sin(a+.4)*.1)],[(0,1,2)],red if k==0 else white)
  elif id=='shonan1':
   for k in range(9):
    a=(k/8-.5)*math.pi*.7;beam('Scallop shell rib',(0,.17,-.2),(math.sin(a)*.25,.17,math.cos(a)*.25),.025,white,vertices=6)
  elif id=='disrupt1':
   for x in [-.12,.12]:sphere('Mischief eyes',(x,.155,.09),(.06,.025,.08),white);sphere('Pupil',(x,.18,.1),(.025,.01,.035),black)
   box('Mischief grin',(0,.16,-.10),(.22,.04,.05),white)
  if id=='finish':
   for x in range(4):
    for z in range(4):
     if (x+z)%2==0:box('Checker',(x*.1-.15,.14,z*.1-.15),(.09,.02,.09),black,0)
 export('item_'+id)
def coin_asset():
 reset();beam('Coin rim',(0,-.065,0),(0,.065,0),.48,gold,vertices=32);beam('Coin inset',(0,-.075,0),(0,.075,0),.40,mat('Coin enamel',(.46,.25,.025),.55,.4),vertices=32)
 for side in [-1,1]:
  for a,c in [((-.17,.09*side,-.20),(-.17,.09*side,.20)),((-.17,.09*side,.20),(.15,.09*side,.20)),((-.17,.09*side,-.20),(.15,.09*side,-.20))]:beam('Raised C monogram',a,c,.04,gold,vertices=6)
 export('prop_coin')

if __name__=='__main__':
 import sys
 if '--export-existing' in sys.argv:
  source=Path(sys.argv[sys.argv.index('--export-existing')+1]).resolve();bpy.ops.wm.open_mainfile(filepath=str(source));export(source.stem)
 else:
  only=next((v for v in sys.argv if v in ['--items-only','--vehicles-only','--courses-only']),None)
  if only in [None,'--vehicles-only']:
   IDS=['apex','swift','vortex','atlas','kebab','banana','tuktuk','bicycle','stormbike','aerobike']
   for index,vid in enumerate(IDS):vehicle(vid,index)
   coin_asset()
  if only in [None,'--courses-only']:
   for track in json.loads((ROOT/'Art/Blender/tracks.json').read_text()):
    if '--suzuka-only' in sys.argv and track['id']!='suzuka':continue
    if '--shonan-only' in sys.argv and track['id']!='shonan':continue
    course(track)
  if only in [None,'--items-only']:
   for item in json.loads((ROOT/'Art/Blender/items.json').read_text(encoding='utf-8')):item_asset(item)
 print('BLENDER_REVISION7_COMPLETE',flush=True)
