import fs from 'node:fs';
const base=process.argv[2]||'http://127.0.0.1:5089';const checks=[];const delay=ms=>new Promise(r=>setTimeout(r,ms));
function check(v,label){if(!v)throw Error(label);checks.push(label);console.log('PASS '+label);}
async function peer(){const ws=new WebSocket(base.replace('http','ws')+'/ws');const queue=[];ws.onmessage=e=>queue.push(JSON.parse(e.data));await new Promise((ok,no)=>{ws.onopen=ok;ws.onerror=no});return{ws,send:data=>ws.send(JSON.stringify(data)),async until(fn,ms=6000){const end=Date.now()+ms;while(Date.now()<end){const n=queue.findIndex(fn);if(n>=0)return queue.splice(n,1)[0];await delay(20);}throw Error('Timed out waiting for message');}};}
const peers=[];try{
 const a=await peer();peers.push(a);a.send({action:'create',name:'Host'});const joined=await a.until(d=>d.type==='joined');const code=joined.code;
 check((await(await fetch(base+'/api/rooms')).json()).some(r=>r.code===code),'Open room listed');
 const b=await peer();peers.push(b);b.send({action:'join',code,name:'Guest'});await b.until(d=>d.type==='joined');
 a.send({action:'configure',track:'suzuka',bots:6});await a.until(d=>d.type==='lobby'&&d.bots===6);
 check(true,'Host chooses six CPUs');b.send({action:'configure',track:'ridge',bots:0});await delay(100);
 let rooms=await(await fetch(base+'/api/rooms')).json();check(!rooms.some(r=>r.code===code),'CPU slots make room full');
 const c=await peer();peers.push(c);c.send({action:'join',code,name:'Full'});check((await c.until(d=>d.type==='error')).code==='ROOM_FULL','Joining reserved CPU slots rejected');c.ws.close();
 a.send({action:'ready',ready:true});a.send({action:'configure',track:'suzuka',bots:5});const reset=await a.until(d=>d.type==='lobby'&&d.bots===5);check(reset.players.every(p=>!p.ready),'Settings change clears readiness');
 a.send({action:'configure',track:'suzuka',bots:6});await a.until(d=>d.type==='lobby'&&d.bots===6);await delay(200);
 a.send({action:'ready',ready:true});b.send({action:'ready',ready:true});
 const load=await a.until(d=>d.type==='state'&&d.phase==='loading');check(load.track==='suzuka'&&load.cars.length===8&&load.cars.filter(x=>x.bot).length===6,'Guest cannot change host setup; eight cars start automatically');
 a.send({action:'loaded',raceId:'wrong'});b.send({action:'loaded',raceId:load.raceId});await delay(150);check((await a.until(d=>d.type==='state'&&d.phase==='loading')).phase==='loading','Invalid load acknowledgement does not start race');
 a.send({action:'loaded',raceId:load.raceId});await a.until(d=>d.type==='state'&&d.phase==='race',6000);check(true,'All loaded starts shared race');
 for(const p of peers)p.ws.close();await delay(21000);
 peers.length=0;const host=await peer();peers.push(host);host.send({action:'create',name:'Eight host'});const room=await host.until(d=>d.type==='joined');
 for(let i=1;i<8;i++){const p=await peer();peers.push(p);p.send({action:'join',code:room.code,name:'Human '+i});await p.until(d=>d.type==='joined');}
 check(peers.length===8,'Eight human connections accepted');
 for(const p of peers)p.send({action:'ready',ready:true});
 const state=await host.until(d=>d.type==='state'&&d.phase==='loading');check(state.cars.length===8&&state.cars.every(c=>!c.bot),'Eight human room starts automatically');
 for(const p of peers)p.send({action:'loaded',raceId:state.raceId});
 const raced=await host.until(d=>d.type==='state'&&d.phase==='race',6000);check(raced.cars.length===8,'Eight human snapshots remain consistent');
 const bytes=Buffer.byteLength(JSON.stringify(raced));check(bytes<20000,'Eight-car state payload stays below 20 KB');console.log('Measured snapshot bytes '+bytes);
 for(const p of peers)p.ws.close(1000,'Done');await delay(1000);peers.length=0;
 const idle=await peer();peers.push(idle);await delay(17000);check(idle.ws.readyState===3,'Unjoined idle connection released after timeout');
 fs.writeFileSync('Logs/flow-room-checks.json',JSON.stringify({checks},null,2));
}finally{for(const p of peers)p.ws.close();}
