"""Validate runtime CRB7 meshes and refresh the reproducible asset manifest."""
from pathlib import Path
import struct, json, hashlib, io
root=Path(__file__).resolve().parents[2]
assets=[]
for path in sorted((root/'Assets/Resources/Blender').glob('*.bytes')):
 data=path.read_bytes();f=io.BytesIO(data);magic,count=struct.unpack('<II',f.read(8));assert magic==0x37425243
 vertices=triangles=0
 for _ in range(count):
  size=struct.unpack('<H',f.read(2))[0];f.read(size+36);v,t=struct.unpack('<II',f.read(8));vertices+=v;triangles+=t//3;f.seek(v*24+t*4,1)
 assert f.tell()==len(data),path
 assets.append(dict(id=path.stem,parts=count,vertices=vertices,triangles=triangles,bytes=len(data),sha256=hashlib.sha256(data).hexdigest()))
(root/'Art/Blender/manifest.json').write_text(json.dumps(dict(format='CRB7',blender='5.2.2 LTS',assets=assets),indent=2)+'\n',encoding='utf-8')
print('Validated',len(assets),'assets')