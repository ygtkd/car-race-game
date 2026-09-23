(()=>{
'use strict';
const $=id=>document.getElementById(id);
document.addEventListener('selectstart',e=>{if(!e.target.closest('input,textarea'))e.preventDefault();});
document.addEventListener('contextmenu',e=>{if(!e.target.closest('input,textarea'))e.preventDefault();});
document.addEventListener('dragstart',e=>e.preventDefault());
document.addEventListener('dblclick',e=>{if(!e.target.closest('input,textarea'))e.preventDefault();},{passive:false});
for(const id of ['courseBack','vehicleBack','galleryBack','recordsBack','leave']){const b=$(id);if(!b)continue;b.removeAttribute('data-i18n');b.textContent='＜';b.classList.add('cornerBack');b.setAttribute('aria-label','戻る');b.closest('.panel')?.prepend(b);}
const back=document.querySelector('#connect .close');if(back){back.removeAttribute('data-i18n');back.textContent='＜';back.classList.add('cornerBack');back.setAttribute('aria-label','戻る');$('connect').prepend(back);}
const lights=document.createElement('div');lights.id='startLights';lights.setAttribute('aria-label','スタート信号');lights.innerHTML='<i></i><i></i><i></i>';$('hud').append(lights);
let previous='';
setInterval(()=>{const d=window.Racer?.telemetry;if(!d)return;const c=d.cars?.[0];if(!c)return;const racing=document.body.dataset.screen==='race',starting=d.phase==='countdown',go=d.phase==='race'&&c.elapsed<1;lights.hidden=!racing||(!starting&&!go);const count=Math.max(1,Math.ceil(d.countdown||0)),key=d.raceId+':'+(starting?count:go?'go':'');if(racing&&(starting||go)&&key!==previous){window.ClubAudio?.startTone?.(go);previous=key;}[...lights.children].forEach((n,i)=>{n.className=go?'green':i<4-count?'red':'';});if(starting)$('centerMessage').textContent='';window.ClubAudio?.engine?.(racing&&d.phase==='race'&&!c.finished,c.speed||0,c.gear||1,c.vehicle,window.Racer.drive?.throttle||0);},80);
})();