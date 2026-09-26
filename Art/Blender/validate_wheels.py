from pathlib import Path
import struct,io,json,math
root=Path(__file__).resolve().parents[2];reports=[]
for p in sorted((root/'Assets/Resources/Blender').glob('vehicle_*.bytes')):
 f=io.BytesIO(p.read_bytes());magic,count=struct.unpack('<II',f.read(8));groups={}
 for _ in range(count):
  name=f.read(struct.unpack('<H',f.read(2))[0]).decode();values=struct.unpack('<9f',f.read(36));v,t=struct.unpack('<II',f.read(8));xyz=struct.unpack('<'+'f'*(v*3),f.read(v*12));f.read(v*12+t*4)
  if not name.startswith('wheel_'):continue
  g=groups.setdefault(name,dict(pivot=values[:3],vertices=[]));assert g['pivot']==values[:3]
  g['vertices'].extend(zip(xyz[::3],xyz[1::3],xyz[2::3]))
 wheels=[]
 for name,g in groups.items():
  lo=[min(v[k] for v in g['vertices']) for k in range(3)];hi=[max(v[k] for v in g['vertices']) for k in range(3)];r=max(hi[1],hi[2]);pivot=g['pivot']
  assert all(abs(lo[k]+hi[k])<.001 for k in range(3)),(p,name,'not centred')
  assert abs(hi[1]-hi[2])<.001,(p,name,'wheel axis not X')
  assert abs(pivot[1]+lo[1])<.001,(p,name,'not on ground')
  assert hi[0]<r*.6,(p,name,'axle protrudes')
  wheels.append(dict(group=name,centre=list(pivot),radius=r,halfWidth=hi[0],rotation=[0,0,0],groundClearance=pivot[1]+lo[1]))
 vid=p.stem.removeprefix('vehicle_');expected=0 if vid=='banana' else 2 if vid in ['bicycle','stormbike','aerobike'] else 3 if vid=='tuktuk' else 4
 assert len(wheels)==expected,(vid,len(wheels))
 if expected==4:
  for axle in ['front','rear']:
   pair=[w for w in wheels if axle in w['group']];assert len(pair)==2
   assert abs(pair[0]['centre'][0]+pair[1]['centre'][0])<.001
   assert pair[0]['centre'][1:]==pair[1]['centre'][1:]
 reports.append(dict(vehicle=vid,wheels=wheels));print('PASS wheel geometry',vid,len(wheels))
(root/'Logs/revision9-wheels.json').write_text(json.dumps(reports,indent=2))
