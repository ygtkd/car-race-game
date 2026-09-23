(()=>{
'use strict';
const $=id=>document.getElementById(id);
const dict={
ja:{single:'シングルプレイ',courses:'コース選択',languageLabel:'言語',close:'閉じる',loading:'ゲームを読み込んでいます',reload:'再読み込み',settings:'設定',ridge:'山岳サーキット',suzuka:'鈴鹿サーキット',distance:'コース全長',best:'自己ベスト',solo:'スタート',online:'オンライン対戦',sensitivity:'ハンドル感度',assist:'運転アシスト',strong:'標準',medium:'弱め',off:'なし',quality:'描画品質',low:'軽量',high:'標準',back:'戻る',name:'表示名',create:'ルームを作成',roomCode:'ルームコード',join:'ルームに参加',ready:'準備完了',notReady:'準備を解除',start:'レース開始',leave:'退室',raceComplete:'レース終了',again:'もう一度走る',menu:'メニューに戻る',paused:'一時停止',resume:'再開',position:'順位',lap:'周回',lapTime:'ラップタイム',bestLap:'ベストラップ',pause:'一時停止',steering:'操舵',brake:'ブレーキ',throttle:'アクセル',recover:'コース復帰 ＋3秒',rotate:'スマホを横向きにしてください',driver:'ドライバー',loadFailed:'読み込めませんでした。ほかのタブを閉じて再読み込みしてください。',connecting:'接続中…',connected:'オンライン接続中',reconnecting:'再接続中…（最大20秒）',connectionLost:'接続を復元できませんでした。レースを退出しました。',serverUnavailable:'対戦サーバーに接続できません。サーバーの起動状態、または満員でないか確認してください。',ROOM_NOT_FOUND:'ルームが見つかりません。',ROOM_FULL:'ルームが満員です。',SERVER_FULL:'サーバーが満員です。',RACE_RUNNING:'レース進行中です。終了後に参加してください。',SESSION_EXPIRED:'再接続期限が切れたか、サーバーが再起動しました。もう一度参加してください。',NOT_READY:'2人以上が準備完了になると開始できます。',SAVE_FAILED:'結果をサーバーに保存できませんでした。今回の結果は画面で確認できます。',INVALID_MESSAGE:'通信データを処理できませんでした。',JOIN_FIRST:'先にルームへ参加してください。',owner:'作成者',waiting:'準備中',disconnected:'切断中',dnf:'未完走',newBest:'自己ベストを更新しました。',bestSaved:'記録はこのブラウザに保存しています。',storageFailed:'ブラウザへの記録保存が利用できません。',onlineResult:'サーバーが確定した対戦結果です。',rematch:'再戦の準備へ',wrongWay:'逆走しています',offroad:'コースに戻ってください',onlineNoPause:'オンライン対戦は一時停止できません。',recovered:'コースに復帰しました（＋3秒）。',finish:'完走',go:'スタート',error:'処理に失敗しました。',recordsUnavailable:'記録を取得できませんでした。'},
en:{single:'Single player',courses:'Circuits',languageLabel:'Language',close:'Close',loading:'Loading the circuit',reload:'Reload',settings:'Settings',ridge:'Highland Circuit',suzuka:'Suzuka Circuit',distance:'CIRCUIT LENGTH',best:'PERSONAL BEST',solo:'Start',online:'Race online',sensitivity:'Steering sensitivity',assist:'Driving assists',strong:'Standard',medium:'Reduced',off:'Off',quality:'Graphics',low:'Low',high:'Standard',back:'Back',name:'Display name',create:'Create room',roomCode:'ROOM CODE',join:'Join room',ready:'Ready',notReady:'Not ready',start:'Start race',leave:'Leave room',raceComplete:'RACE COMPLETE',again:'Drive again',menu:'Return to menu',paused:'Paused',resume:'Resume',position:'POSITION',lap:'LAP',lapTime:'LAP TIME',bestLap:'BEST LAP',pause:'Pause',steering:'STEERING',brake:'BRAKE',throttle:'THROTTLE',recover:'Recover +3s',rotate:'Rotate your phone',driver:'Driver',loadFailed:'Unable to load. Close other tabs and reload.',connecting:'Connecting…',connected:'Online',reconnecting:'Reconnecting… (up to 20 seconds)',connectionLost:'Connection could not be restored. You left the race.',serverUnavailable:'Cannot connect. Check whether the server is running or full.',ROOM_NOT_FOUND:'Room not found.',ROOM_FULL:'Room is full.',SERVER_FULL:'Server is full.',RACE_RUNNING:'Race in progress. Join when it finishes.',SESSION_EXPIRED:'Session expired or server restarted. Please join again.',NOT_READY:'At least two ready players are required.',SAVE_FAILED:'Server could not save the result. Results are still visible here.',INVALID_MESSAGE:'Invalid network message.',JOIN_FIRST:'Join a room first.',owner:'Owner',waiting:'Not ready',disconnected:'Disconnected',dnf:'DNF',newBest:'New personal best.',bestSaved:'Records are saved in this browser.',storageFailed:'Browser record storage is unavailable.',onlineResult:'Results confirmed by the server.',rematch:'Prepare for rematch',wrongWay:'WRONG WAY',offroad:'RETURN TO THE TRACK',onlineNoPause:'Online races cannot be paused.',recovered:'Recovered to the circuit (+3s).',finish:'Finished',go:'GO',error:'The operation failed.',recordsUnavailable:'Records unavailable.'}
};
Object.assign(dict.ja,Club.words.ja);Object.assign(dict.en,Club.words.en);
const read=(k,d)=>{try{return JSON.parse(localStorage.getItem(k))??d}catch{return d}};
let lang=read('cr.lang','ja');if(!dict[lang])lang='ja';
const save=(k,v)=>{try{localStorage.setItem(k,JSON.stringify(v));return true}catch{return false}};
const t=k=>dict[lang][k]||dict.en[k]||dict[lang].error;
let mode='solo',settingsOpen=false,settingsPaused=false,settingsFocus=null;
let unity,screen='menu',track='ridge',map=[],telemetry=null;
let settings=read('cr.settings',{assist:1,sensitivity:1,quality:navigator.maxTouchPoints?0:1});
let ws=null,token='',self='',room='',owner='',ready=false,online=false,closing=false,retryStart=0,retryTimer=null,lastNetworkPhase='';
let input={steer:0,throttle:0,brake:0},keys=new Set(),wheelPointer=null,wheelDirection=0,toastTimer;
const inputReleases=[];
const panels=['menu','courses','connect','lobby','results','pause','vehicles','gallery','records'];
const time=v=>!v||v<=0?'—':Math.floor(v/60).toString().padStart(2,'0')+':'+(v%60).toFixed(2).padStart(5,'0');
const send=(action,rest={})=>{if(ws?.readyState===1)ws.send(JSON.stringify({action,...rest}));};
const command=(action,rest={})=>unity?.SendMessage('RaceGame','CommandFromWeb',JSON.stringify({action,...rest}));
function toast(key){$('toast').textContent=t(key);$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,6500);}
function clearInput(){
 for(const release of inputReleases)release();
 input={steer:0,throttle:0,brake:0};keys.clear();wheelPointer=null;wheelDirection=0;document.querySelectorAll('.pressed').forEach(e=>e.classList.remove('pressed'));if(online)send('input',{...input,brake:1,assist:settings.assist,sensitivity:settings.sensitivity});}
let pendingScreen='',screenTransition=Promise.resolve(),transitionCount=0,hasShown=false;
function show(name){
 if(name===screen&&hasShown&&!pendingScreen)return Promise.resolve();if(name===pendingScreen)return screenTransition;
 const token=++transitionCount;pendingScreen=name;clearInput();document.body.classList.add('transitioning');
 const previous=$(screen);if(hasShown&&previous)previous.animate([{opacity:1},{opacity:0}],{duration:120});
 screenTransition=(async()=>{if(hasShown)await new Promise(r=>setTimeout(r,120));if(token!==transitionCount)return;
 showNow(name);hasShown=true;pendingScreen='';document.body.classList.remove('transitioning');
 const next=$(name);if(next)next.animate([{opacity:0},{opacity:1}],{duration:160});})();return screenTransition;
}
function showNow(name){
 screen=name;
 $('game').style.visibility=['race','pause','results','menu','vehicles','gallery'].includes(name)?'visible':'hidden';
 panels.forEach(id=>$(id).hidden=id!==name);
 $('hud').hidden=name!=='race';document.body.classList.toggle('racing',name==='race');
 if(name!=='race')clearInput();
 if(name==='menu')updateBest();Club.onScreen(name);
}
function localize(){
 document.documentElement.lang=lang;document.title='COAST RACER';
 document.querySelectorAll('[data-i18n]').forEach(e=>e.textContent=t(e.dataset.i18n));
 $('closeSettings').setAttribute('aria-label',t('close'));$('language').value=lang;$('name').placeholder=t('driver');
 $('wheel').setAttribute('aria-label',t('steering'));$('language').setAttribute('aria-label',lang==='ja'?'言語':'Language');
 $('again').textContent=t(online?'rematch':'again');
 Club.localize();updateBest();
}
function updateBest(){$('lobbyTrack').textContent=t(track);$('best').textContent=time(read('cr.best.2lap.'+track,0));}
function drawMap(canvas,cars=[]){
 if(!map.length)return;const ctx=canvas.getContext('2d');const w=canvas.width,h=canvas.height;
 const xs=map.map(p=>p.x),zs=map.map(p=>p.z),minX=Math.min(...xs),maxX=Math.max(...xs),minZ=Math.min(...zs),maxZ=Math.max(...zs);
 const scale=Math.min((w-24)/(maxX-minX||1),(h-24)/(maxZ-minZ||1));
 const px=p=>(p.x-(maxX+minX)/2)*scale+w/2,pz=p=>h/2-(p.z-(maxZ+minZ)/2)*scale;
 ctx.clearRect(0,0,w,h);ctx.lineWidth=canvas.id==='courseMap'?4:3;ctx.strokeStyle='#acb5be';ctx.lineJoin='round';
 ctx.beginPath();map.forEach((p,i)=>i?ctx.lineTo(px(p),pz(p)):ctx.moveTo(px(p),pz(p)));ctx.closePath();ctx.stroke();
 ctx.fillStyle='#d6a966';ctx.fillRect(px(map[0])-3,pz(map[0])-3,6,6);
 cars.forEach((c,i)=>{ctx.beginPath();ctx.fillStyle=i===0?'#eebc72':'#e2e5e8';ctx.arc(px(c),pz(c),i===0?4:2.5,0,Math.PI*2);ctx.fill();});
}
function results(cars){
 show('results');const me=online?cars.find(c=>c.id===self):cars[0];$('resultTime').textContent=me?.dnf?t('dnf'):time(me?.finishTime);
 $('resultRows').replaceChildren();
 [...cars].sort((a,b)=>(a.rank||99)-(b.rank||99)).forEach(c=>{
  const row=document.createElement('div');row.className='resultRow';const name=document.createElement('span'),result=document.createElement('strong');
  name.textContent=(c.rank||'—')+'  '+c.name;result.textContent=c.dnf?t('dnf'):c.finished?time(c.finishTime):t('waiting');row.append(name,result);$('resultRows').append(row);
 });
 $('again').textContent=t(online?'rematch':'again');
 if(!online&&me?.finished){const previous=read('cr.best.2lap.'+track,0);const best=!previous||me.finishTime<previous;const ok=!best||save('cr.best.2lap.'+track,me.finishTime);$('recordNotice').textContent=t(!ok?'storageFailed':best?'newBest':'bestSaved');}
 else $('recordNotice').textContent=t('onlineResult');
 Club.record(cars,{online,self,track,raceId:currentRaceId||telemetry?.raceId});
}
function receive(data){
 telemetry=data;window.Racer.telemetry=data;Club.receive(data);
 if(unity&&data.ready&&$('app').hidden){$('loading').hidden=true;$('app').hidden=false;show('menu');}
 if(data.map?.length){map=data.map;track=data.track;$('length').textContent=(data.length/1000).toFixed(2)+' km';drawMap($('courseMap'));updateBest();}
 const me=data.cars?.[0];if(!me)return;
 $('speed').textContent=Math.round(me.speed*3.6);$('gear').textContent=me.gear||1;
 $('position').textContent=(me.rank||1)+' / '+data.cars.length;$('lap').textContent=Math.min(2,me.lap+1)+' / 2';
 $('lapTime').textContent=time(Math.max(.01,me.elapsed-me.lapStart));$('bestLap').textContent=time(me.bestLap);
 $('centerMessage').textContent=data.phase==='countdown'?Math.max(1,Math.ceil(data.countdown)):data.phase==='race'&&me.elapsed<.8?t('go'):'';
 $('drivingWarning').textContent=me.recoveryRemaining>0?(t('offroad')+' '+Math.ceil(me.recoveryRemaining)):me.wrongWay?t('wrongWay'):me.offroad?t('offroad'):'';
 drawMap($('miniMap'),data.cars);
 if(!online && screen==='race' && data.phase==='finished')results(data.cars);
 if(!online && screen==='race' && !settingsOpen && data.phase==='paused')show('pause');
}
let starting=false,currentRaceId='';
async function startSolo(){
 if(starting)return;starting=true;online=false;lastNetworkPhase='';currentRaceId='';
 try{await Club.load(()=>command('start',{track,...Club.choice(),assist:settings.assist,sensitivity:settings.sensitivity}));command('begin');await show('race');}finally{starting=false;}
}
function lobby(data){
 room=data.code;owner=data.owner;track=data.track;$('codeDisplay').textContent=room;$('lobbyTrack').textContent=t(track);
 $('roster').replaceChildren();
 for(const p of data.players){
  const row=document.createElement('div');row.className='person';const name=document.createElement('span'),status=document.createElement('small');
  name.textContent=p.name+(p.id===owner?' · '+t('owner'):'');status.textContent=t(!p.connected?'disconnected':p.ready?'ready':'waiting');row.append(name,status);$('roster').append(row);
  if(p.id===self)ready=p.ready;
 }
 $('ready').textContent=t(ready?'notReady':'ready');$('startOnline').hidden=self!==owner;
 $('startOnline').disabled=data.players.filter(p=>p.connected).length<2||data.players.some(p=>p.connected&&!p.ready);
 if(data.phase==='lobby'){Club.network(data);show('lobby');}
}
function endConnection(){
 closing=true;clearTimeout(retryTimer);ws?.close();ws=null;online=false;token='';room='';lastNetworkPhase='';$('connectionStatus').hidden=true;
}
function connect(action){
 closing=false;$('connectionStatus').hidden=false;$('connectionStatus').textContent=t(action==='resume'?'reconnecting':'connecting');
 let socket;try{socket=new WebSocket((location.protocol==='https:'?'wss:':'ws:')+'//'+location.host+'/ws');}catch{toast('serverUnavailable');return;}
 ws=socket;
 socket.onopen=()=>{
  if(action==='resume')send('resume',{token});
  else send(action,{...Club.choice(),track,code:$('roomCode').value.trim().toUpperCase()});
 };
 socket.onmessage=e=>{
  let data;try{data=JSON.parse(e.data)}catch{return;}
  if(data.type==='joined'){token=data.token;self=data.id;room=data.code;online=true;retryStart=0;$('connectionStatus').textContent=t('connected');}
  if(data.type==='lobby')lobby(data);
  if(data.type==='state'){
   if(!online)return;
   currentRaceId=data.raceId;Club.network(data);track=data.track;unity?.SendMessage('RaceGame','NetworkSnapshot',JSON.stringify(data));
   if(data.phase==='countdown'||data.phase==='race'){if(screen!=='race')show('race');}
   if(data.phase==='finished'&&lastNetworkPhase!=='finished')results(data.cars);
   lastNetworkPhase=data.phase;
  }
  if(data.type==='error'){
   toast(data.code);
   if(data.code==='SESSION_EXPIRED'){endConnection();command('menu',{track});show('connect');}
   else if(!online){closing=true;socket.close();$('connectionStatus').hidden=true;}
  }
 };
 socket.onclose=()=>{
  if(closing||socket!==ws)return;
  if(token){
   if(!retryStart)retryStart=Date.now();
   if(Date.now()-retryStart<20000){$('connectionStatus').textContent=t('reconnecting');clearInput();retryTimer=setTimeout(()=>connect('resume'),1500);}
   else{endConnection();command('menu',{track});show('connect');toast('connectionLost');}
  }else{$('connectionStatus').hidden=true;toast('serverUnavailable');}
 };
 socket.onerror=()=>{};
}
$('language').onchange=()=>{lang=$('language').value;save('cr.lang',lang);localize();};
document.querySelectorAll('[data-track]').forEach(button=>button.onclick=()=>{
 track=button.dataset.track;document.querySelectorAll('[data-track]').forEach(b=>b.classList.toggle('active',b===button));command('preview',{track});updateBest();
});
$('singleMode').onclick=()=>{mode='solo';show('vehicles');};
$('onlineMode').onclick=()=>{mode='online';show('vehicles');};
$('courseBack').onclick=()=>show('vehicles');
$('solo').onclick=()=>mode==='online'?show('connect'):startSolo();
$('createRoom').onclick=()=>{endConnection();token='';connect('create');};
$('joinRoom').onclick=()=>{endConnection();token='';connect('join');};
$('ready').onclick=()=>send('ready',{ready:!ready});$('startOnline').onclick=()=>send('start');
$('leave').onclick=()=>{endConnection();command('menu',{track});show('menu');};
$('again').onclick=()=>online?show('lobby'):startSolo();
function exitRace(){endConnection();command('menu',{track});show('menu');}
$('resultMenu').onclick=exitRace;$('pauseMenu').onclick=()=>Club.confirmExit();
$('pauseButton').onclick=()=>{clearInput();if(online){Club.confirmExit();return;}command('pause',{paused:true});show('pause');};
$('resume').onclick=()=>{command('pause',{paused:false});show('race');};
function openSettings(){
 if(settingsOpen)return;settingsOpen=true;settingsFocus=document.activeElement;clearInput();
 settingsPaused=!online&&screen==='race';if(settingsPaused)command('pause',{paused:true});
 $('settingsOverlay').hidden=false;
 for(const child of $('app').children)if(child.id!=='settingsOverlay')child.inert=true;
 $('closeSettings').focus();
}
function closeSettings(){
 if(!settingsOpen)return;settingsOpen=false;clearInput();$('settingsOverlay').hidden=true;
 for(const child of $('app').children)child.inert=false;
 if(settingsPaused&&screen==='race')command('pause',{paused:false});settingsPaused=false;
 settingsFocus?.focus();
}
$('settingsButton').onclick=$('menuSettings').onclick=$('raceSettings').onclick=openSettings;
$('closeSettings').onclick=closeSettings;
$('settingsOverlay').onclick=e=>{if(e.target===$('settingsOverlay'))closeSettings();};
document.querySelectorAll('.close').forEach(e=>e.onclick=()=>show('courses'));
for(const key of ['assist','sensitivity','quality']){
 $(key).value=settings[key];$(key).onchange=()=>{settings[key]=Number($(key).value);save('cr.settings',settings);command('quality',{quality:settings.quality});};
}
$('reload').onclick=()=>location.reload();
for(const pedal of ['throttle','brake']){
 const element=$(pedal);const pointers=new Set();
 inputReleases.push(()=>{for(const id of pointers){if(element.hasPointerCapture(id))element.releasePointerCapture(id);}pointers.clear();});
 element.onpointerdown=e=>{if(screen!=='race'||settingsOpen)return;e.preventDefault();element.setPointerCapture(e.pointerId);pointers.add(e.pointerId);input[pedal]=1;element.classList.add('pressed');};
 const release=e=>{pointers.delete(e.pointerId);if(!pointers.size){input[pedal]=0;element.classList.remove('pressed');}};
 element.onpointerup=release;element.onpointercancel=release;element.onlostpointercapture=release;
 element.onpointermove=e=>{const r=element.getBoundingClientRect();if(e.clientX<r.left-12||e.clientX>r.right+12||e.clientY<r.top-12||e.clientY>r.bottom+12)release(e);};
}
function touchDirection(e){const r=$('wheel').getBoundingClientRect();wheelDirection=e.clientX<r.left+r.width/2?-1:1;}
inputReleases.push(()=>{if(wheelPointer!==null&&$('wheel').hasPointerCapture(wheelPointer))$('wheel').releasePointerCapture(wheelPointer);});
$('wheel').onpointerdown=e=>{if(screen!=='race'||settingsOpen||wheelPointer!==null)return;e.preventDefault();wheelPointer=e.pointerId;touchDirection(e);$('wheel').setPointerCapture(e.pointerId);};
$('wheel').onpointermove=e=>{if(e.pointerId===wheelPointer)touchDirection(e);};
const releaseWheel=e=>{if(e.pointerId===wheelPointer){wheelPointer=null;wheelDirection=0;}};
$('wheel').onpointerup=releaseWheel;$('wheel').onpointercancel=releaseWheel;$('wheel').onlostpointercapture=releaseWheel;window.addEventListener('keydown',e=>{
 if(!$('confirmExit').hidden)return;
 if(settingsOpen){if(e.key==='Escape'){e.preventDefault();closeSettings();}return;}
 if(/INPUT|SELECT|TEXTAREA/.test(document.activeElement?.tagName)||screen!=='race')return;
 if(['ArrowLeft','ArrowRight','ArrowUp','ArrowDown',' ','a','d','w','s','r','Escape'].includes(e.key))e.preventDefault();
 keys.add(e.key.toLowerCase());if(e.key.toLowerCase()==='e'&&!e.repeat)$('specialButton').click();if(e.key==='Escape'&&!e.repeat)$('pauseButton').click();
});
window.addEventListener('keyup',e=>keys.delete(e.key.toLowerCase()));
function blur(){clearInput();if(!online&&screen==='race'&&!settingsOpen){command('pause',{paused:true});show('pause');}}
window.addEventListener('blur',blur);document.addEventListener('visibilitychange',()=>{if(document.hidden)blur();});
let lastFrame=performance.now();
function animate(now){
 const dt=Math.min(.05,(now-lastFrame)/1000);lastFrame=now;
 input.steer+=(wheelDirection-input.steer)*(1-Math.exp(-dt*(wheelDirection?4:10)));
 $('wheelFace').style.transform='rotate('+(input.steer*80)+'deg)';$('wheel').setAttribute('aria-valuenow',Math.round(input.steer*100));
 requestAnimationFrame(animate);
}
requestAnimationFrame(animate);
setInterval(()=>{
 const active=screen==='race'&&!settingsOpen&&!pendingScreen&&$('confirmExit').hidden&&!document.hidden;
 const drive={steer:active?Math.max(-1,Math.min(1,input.steer+(keys.has('arrowright')||keys.has('d')?1:0)-(keys.has('arrowleft')||keys.has('a')?1:0))):0,throttle:active?Math.max(input.throttle,keys.has('arrowup')||keys.has('w')?1:0):0,brake:active?Math.max(input.brake,keys.has('arrowdown')||keys.has('s')||keys.has(' ')?1:0):(online?1:0),assist:settings.assist,sensitivity:settings.sensitivity};
 window.Racer.drive=drive;unity?.SendMessage('RaceGame','SetInput',JSON.stringify(drive));
 if(online)send('input',drive);
},50);
window.Racer={
 telemetry:null,receive,
 loaded(instance){unity=instance;command('quality',{quality:settings.quality});console.info('COAST_RACER_READY');},
 loadError(error){$('loadError').textContent=t('loadFailed');$('reload').hidden=false;console.error(error);},
 get screen(){return screen},get online(){return online}
};
Club.init({command,send,show,t,time,clearInput,toast,exit:exitRace,screen:()=>screen,online:()=>online,track:()=>track});
localize();
})();