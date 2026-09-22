(()=>{
 let context,bgmGain,seGain,current,scene='menu',revision=0,unlocked=false,paused=false;
 const cache=new Map();let levels={bgm:.45,se:.65,muted:false};
 const get=async name=>{if(!cache.has(name))cache.set(name,fetch('audio/'+name+'.wav').then(r=>{if(!r.ok)throw Error('Audio '+r.status);return r.arrayBuffer()}).then(b=>context.decodeAudioData(b)));return cache.get(name);};
 const gain=()=>{if(!context)return;bgmGain.gain.setTargetAtTime(levels.muted?0:levels.bgm,context.currentTime,.05);seGain.gain.setTargetAtTime(levels.muted?0:levels.se,context.currentTime,.02);};
 async function play(){if(!unlocked||paused)return;const rev=++revision;const name=scene;try{const buffer=await get(name);if(rev!==revision||paused)return;const source=context.createBufferSource(),fade=context.createGain();source.buffer=buffer;source.loop=true;source.connect(fade);fade.connect(bgmGain);fade.gain.setValueAtTime(0,context.currentTime);fade.gain.linearRampToValueAtTime(1,context.currentTime+.5);source.start();if(current){current.fade.gain.cancelScheduledValues(context.currentTime);current.fade.gain.setTargetAtTime(0,context.currentTime,.15);current.source.stop(context.currentTime+.8);}current={source,fade};}catch(e){console.warn('Audio unavailable',e.message);}}
 async function unlock(){if(!context){context=new (window.AudioContext||window.webkitAudioContext)();bgmGain=context.createGain();seGain=context.createGain();bgmGain.connect(context.destination);seGain.connect(context.destination);gain();}await context.resume();if(!unlocked){unlocked=true;play();}}
 async function effect(name='click'){try{await unlock();const buffer=await get(name);if(paused)return;const s=context.createBufferSource();s.buffer=buffer;s.connect(seGain);s.start();}catch{}}
 document.addEventListener('pointerdown',()=>unlock().catch(()=>{}),{passive:true});
 document.addEventListener('click',e=>{if(e.target.closest('button')&&!e.target.closest('#pedals'))effect();},true);
 document.addEventListener('visibilitychange',()=>{paused=document.hidden;if(!context)return;if(paused)context.suspend();else context.resume().then(()=>{if(!current)play();}).catch(()=>{});});
 window.ClubAudio={effect,setScene(value){if(value===scene)return;scene=value;play();},settings(value){levels=value;gain();},get status(){return{unlocked,scene,state:context?.state,levels:{...levels},decoded:cache.size};}};
})();
