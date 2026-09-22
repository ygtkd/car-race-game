import fs from 'node:fs';import path from 'node:path';import {brotliCompressSync,constants} from 'node:zlib';
const root=path.resolve(process.argv[2]||'Builds/site');
function compress(dir){for(const entry of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,entry.name);if(entry.isDirectory())compress(p);else if(/\.(wasm|data|js)$/.test(p)){const bytes=fs.readFileSync(p);const compressed=brotliCompressSync(bytes,{params:{[constants.BROTLI_PARAM_QUALITY]:6}});fs.writeFileSync(p+'.br',compressed);console.log(path.relative(root,p),bytes.length,'->',compressed.length);}}}
compress(root);
