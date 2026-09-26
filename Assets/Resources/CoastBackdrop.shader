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
 fixed4 frag(v2f i):SV_Target{
 float3 d=normalize(i.dir);float h=max(0,d.y);float3 col=lerp(float3(.70,.77,.82),float3(.22,.43,.66),sqrt(h));
 float angle=atan2(d.x,d.z);float ridge=.045+_Mountain*(.06+.065*sin(angle*5)+.025*sin(angle*13));
 col=lerp(col,float3(.36,.46,.48),1-smoothstep(ridge-.007,ridge+.007,d.y));
 float front=.025+_Mountain*(.025+.025*sin(angle*7+2));col=lerp(col,float3(.25,.36,.34),1-smoothstep(front-.004,front+.004,d.y));
 if(d.y>.09){float2 p=d.xz/(d.y+.25)*3;float n=noise(p)*.6+noise(p*2)*.27+noise(p*4)*.13;float cloud=smoothstep(.52,.7,n)*smoothstep(.09,.22,d.y);col=lerp(col,float3(.94,.95,.94),cloud*.87);}
 col=lerp(col,col*.065+float3(.006,.009,.023),_CoastNight);float stars=step(.997,hash(floor(d.xz/(abs(d.y)+.1)*700)))*step(.1,d.y);col+=stars*_CoastNight*.5;return fixed4(col,1);
 }
 ENDCG }
 }
}
