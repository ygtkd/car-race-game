Shader "CoastRacer/Surface"
{
 Properties { _Color("Color",Color)=(1,1,1,1) _Metallic("Metallic",Range(0,1))=0 _Smoothness("Smoothness",Range(0,1))=.2 _Grain("Grain",Range(0,1))=0 _Emission("Lamp",Range(0,1))=0 _Shore("Coastal terrain",Float)=0 }
 SubShader {
 Tags {"RenderType"="Opaque"}
 Pass { Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fog
 #include "UnityCG.cginc"
 float _CoastNight,_Emission,_Shore;float4 _CoastHeads[8],_CoastDirections[8];int _CoastLightCount;
 fixed4 _Color;float _Metallic,_Smoothness,_Grain;
 struct Input {float4 vertex:POSITION;float3 normal:NORMAL;};
 struct Output {float4 vertex:SV_POSITION;float3 normal:TEXCOORD0;float3 world:TEXCOORD1;UNITY_FOG_COORDS(2)};
 Output vert(Input v){Output o;o.vertex=UnityObjectToClipPos(v.vertex);o.normal=UnityObjectToWorldNormal(v.normal);o.world=mul(unity_ObjectToWorld,v.vertex).xyz;UNITY_TRANSFER_FOG(o,o.vertex);return o;}
 fixed4 frag(Output i):SV_Target {
 float3 n=normalize(i.normal),v=normalize(_WorldSpaceCameraPos.xyz-i.world),l=normalize(float3(-.8,.30,.52));
 if(dot(n,v)<0)n=-n;float diffuse=max(0,dot(n,l));float3 h=normalize(l+v);
 float spec=pow(max(0,dot(n,h)),lerp(8,160,_Smoothness))*lerp(.02,.75,_Smoothness);
 float fresnel=pow(1-saturate(dot(n,v)),5);float3 r=reflect(-v,n);
 float3 sky=lerp(float3(.09,.11,.13),float3(.64,.74,.86),saturate(r.y*.5+.5));
 float studio=pow(saturate(1-abs(r.y-.35)),35)*.32;
 float noise=.5;if(_Grain>.001)noise=frac(sin(dot(floor(i.world.xz*28),float2(12.9898,78.233)))*43758.5453);
 float3 groundColor=lerp(_Color.rgb,float3(.66,.58,.38),_Shore*(1-smoothstep(-.5,4,i.world.y)));
 float3 baseColor=groundColor*(1+(noise-.5)*_Grain);
 float3 color=baseColor*(.40+.60*diffuse)*(1-.18*_Metallic)+spec*lerp(float3(1,1,1),baseColor,_Metallic);
 color+=sky*(_Smoothness*.12+fresnel*.22)*(_Metallic+.1)+studio*_Smoothness;
 float illumination=0;
 if(_CoastNight>.5){for(int h=0;h<8;h++){if(h>=_CoastLightCount)break;float3 delta=i.world-_CoastHeads[h].xyz;float dist=length(delta);float cone=smoothstep(.85,.98,dot(normalize(delta),normalize(_CoastDirections[h].xyz)))*(1-smoothstep(12,70,dist));illumination+=cone;}}
 float ground=saturate(n.y*.8+.2);color=lerp(color,color*.19+baseColor*saturate(illumination)*ground*1.8+baseColor*_Emission*1.8,_CoastNight);
 fixed4 output=fixed4(color,1);UNITY_APPLY_FOG(i.fogCoord,output);return output;
 }
 ENDCG
 }
 }
}