import bpy,json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(__file__).resolve().parents[2];reports=[]
for track in json.loads((root/'Art/Blender/tracks.json').read_text()):
 bpy.ops.wm.open_mainfile(filepath=str(root/'Art/Blender/Models'/('course_'+track['id']+'.blend')))
 vertices=[];triangles=[];owners=[]
 for o in bpy.context.scene.objects:
  if o.type!='MESH' or not any(x in o.name for x in ['Continuous landscape','Graded road verge']):continue
  me=o.data;me.calc_loop_triangles();offset=len(vertices);vertices.extend(o.matrix_world@v.co for v in me.vertices);triangles.extend(tuple(offset+i for i in tri.vertices) for tri in me.loop_triangles);owners.extend([o.name]*len(me.loop_triangles))
 bvh=BVHTree.FromPolygons(vertices,triangles,all_triangles=True);penetrations=[];clearance=[]
 for i,p in enumerate(track['points']):
  a=track['points'][(i-1)%640];c=track['points'][(i+1)%640];t=Vector((c['x']-a['x'],c['z']-a['z'])).normalized()
  for side in [-15,0,15]:
   x=p['x']+t.y*side;z=p['z']-t.x*side;hit=bvh.ray_cast(Vector((x,-z,p['y']+150)),Vector((0,0,-1)))
   if hit[0] is None:raise RuntimeError('Terrain gap '+track['id'])
   excess=hit[0].z-p['y'];clearance.append(-excess)
   if excess>.10:penetrations.append({'sample':i,'side':side,'metres':round(excess,3),'object':owners[hit[2]]})
 reports.append({'track':track['id'],'samples':1920,'penetrations':penetrations,'maxClearance':max(clearance)})
 print('TERRAIN_CHECK',track['id'],len(penetrations),penetrations[:8],flush=True)
(root/'Logs/revision7-terrain.json').write_text(json.dumps(reports,indent=2))

if any(r["penetrations"] for r in reports):raise RuntimeError("Terrain overlaps road; inspect Logs/revision7-terrain.json")
