Shader "Coast/Backdrop" {
 Properties { _Mountain ("Mountains", Float) = 1 }
 SubShader { Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" } Cull Off ZWrite Off
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float _Mountain,_CoastNight;
 struct appdata {float4 vertex:POSITION;};
 struct v2f {float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 float stars(float2 uv,float scale){uv.x*=2;float2 cell=floor(uv*scale);float rnd=hash(cell);float2 centre=.2+.6*float2(hash(cell+71),hash(cell+13));float dist=length(frac(uv*scale)-centre);float radius=lerp(.018,.055,rnd*rnd);float aa=max(fwidth(dist),.012);return (1-smoothstep(radius-aa,radius+aa,dist))*step(.94,rnd)*lerp(.25,1,rnd);}
 fixed4 frag(v2f i):SV_Target{
 float3 d=normalize(i.dir);float h=max(0,d.y);float night=saturate(_CoastNight);
 float3 day=lerp(float3(.76,.83,.87),float3(.12,.35,.64),sqrt(h));
 float3 col=lerp(day,lerp(float3(.025,.037,.07),float3(.003,.009,.029),sqrt(h)),night);
 float angle=atan2(d.x,d.z);float2 uv=float2(angle/6.2831853+.5,asin(clamp(d.y,-1,1))/3.1415927+.5);
 if(night<.5){
  float sunAngle=length(d-normalize(float3(-.8,.30,.52)));float sunAA=max(fwidth(sunAngle),.0005);
  float disc=1-smoothstep(.019-sunAA,.019+sunAA,sunAngle);float halo=exp(-sunAngle*21)*.22;
  col+=float3(1,.68,.32)*halo;col=lerp(col,lerp(float3(1,.78,.40),float3(1,.97,.83),saturate(1-sunAngle/.019)),disc);
 }else{
  col+=float3(.66,.79,1)*stars(uv,260)*smoothstep(.06,.25,h);
  float3 moonDir=normalize(float3(.38,.26,-.75));float md=length(d-moonDir);float radius=.034;float maa=max(fwidth(md),.0005);
  float moon=1-smoothstep(radius-maa,radius+maa,md);
  if(md<radius+maa){
   float2 moonUV=float2(dot(d,normalize(cross(float3(0,1,0),moonDir))),dot(d,normalize(cross(moonDir,cross(float3(0,1,0),moonDir)))))/radius;
   float craters=.83+.17*noise(moonUV*9);float light=lerp(.27,1,smoothstep(-.8,.65,moonUV.x));
   col=lerp(col,float3(.90,.93,1)*craters*light,moon);
  }
  col+=float3(.17,.23,.40)*exp(-md*42)*.18;
 }
 if(d.y>.09){float2 p=d.xz/(d.y+.25)*3;float n=noise(p)*.6+noise(p*2)*.27+noise(p*4)*.13;float cloud=smoothstep(.54,.73,n)*smoothstep(.09,.22,d.y);col=lerp(col,lerp(float3(.95,.96,.94),float3(.036,.050,.079),night),cloud*.85);}
 float ridge=.045+_Mountain*(.06+.065*sin(angle*5)+.025*sin(angle*13));col=lerp(col,lerp(float3(.36,.46,.48),float3(.016,.025,.038),night),1-smoothstep(ridge-.007,ridge+.007,d.y));
 float front=.025+_Mountain*(.025+.025*sin(angle*7+2));col=lerp(col,lerp(float3(.25,.36,.34),float3(.009,.018,.023),night),1-smoothstep(front-.004,front+.004,d.y));return fixed4(col,1);
 }
 ENDCG }
 }
}
