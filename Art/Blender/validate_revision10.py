import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(__file__).resolve().parents[2];track=next(t for t in json.loads((root/'Art/Blender/tracks.json').read_text()) if t['id']=='shonan')
bpy.ops.wm.open_mainfile(filepath=str(root/'Art/Blender/Models/course_shonan.blend'))
def tree(match):
 v=[];t=[];owner=[]
 for o in bpy.context.scene.objects:
  if o.type!='MESH' or not match(o.name):continue
  me=o.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh();me.calc_loop_triangles();offset=len(v);v.extend(o.matrix_world@p.co for p in me.vertices);t.extend(tuple(offset+i for i in f.vertices) for f in me.loop_triangles);owner.extend([o.name]*len(me.loop_triangles))
 return BVHTree.FromPolygons(v,t,all_triangles=True),owner
terrain,names=tree(lambda name:any(s in name for s in ['Continuous landscape','Graded road verge']))
roof,_=tree(lambda name:'Iwaya cave vault' in name)
errors=[];clearances=[]
for i,p in enumerate(track['points']):
 a=track['points'][(i-1)%640];b=track['points'][(i+1)%640];d=Vector((b['x']-a['x'],b['z']-a['z'])).normalized();cave=track['sections'][i]==1
 for side in [-15,0,15]:
  x,z=p['x']+d.y*side,p['z']-d.x*side;hit=terrain.ray_cast(Vector((x,-z,p['y']+(1.5 if cave else 150))),Vector((0,0,-1)))
  if hit[0] is not None and hit[0].z>p['y']+.1:errors.append(dict(sample=i,side=side,excess=hit[0].z-p['y'],name=names[hit[2]]))
 if cave:
  for side in [-6,0,6]:
   x,z=p['x']+d.y*side,p['z']-d.x*side;hit=roof.ray_cast(Vector((x,-z,p['y']+1)),Vector((0,0,1)),40)
   if hit[0] is None:errors.append(dict(sample=i,side=side,error='Missing cave roof'))
   else:
    height=hit[0].z-p['y'];clearances.append(height)
    if height<8:errors.append(dict(sample=i,side=side,error='Low roof',height=height))
# Eye/vehicle-height rays catch scenery blocking the road or a chase camera.
objects,objectnames=tree(lambda name:not any(x in name for x in ['Road','kerb','Edge line','Grey shoulder','Continuous landscape','Graded road verge','Ocean','Finish line','Expressway lane dash']))
for i,p in enumerate(track['points']):
 a=track['points'][(i-1)%640];b=track['points'][(i+1)%640];d=Vector((b['x']-a['x'],b['z']-a['z'])).normalized()
 for height in [1.3,4]:
  for side in [-5,0,5]:
   x,z=p['x']+d.y*side,p['z']-d.x*side
   for sign in [-1,1]:
    hit=objects.ray_cast(Vector((x,-z,p['y']+height)),Vector((d.x*sign,-d.y*sign,0)),7)
    if hit[0] is not None:errors.append(dict(sample=i,side=side,height=height,name=objectnames[hit[2]],error='Scenery obstructs road'))
report=dict(samples=1920,caveRoofProbes=len(clearances),minRoof=min(clearances),errors=errors)
(root/'Logs/revision10-terrain.json').write_text(json.dumps(report,indent=2));print(json.dumps(report)[:4000])
if errors:raise RuntimeError('Terrain/roof failures')
