Shader "Coast/Effect" {
 Properties { _Color ("Color", Color) = (1,1,1,1) }
 SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" } Pass {
 Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 fixed4 _Color;
 struct v2f { float4 pos:SV_POSITION; };
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);return o;}
 fixed4 frag(v2f i):SV_Target{return fixed4(_Color.rgb,.7);}
 ENDCG
 } }
}