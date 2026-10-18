Shader "Dimari/PhotoFaceDepth"
{
 Properties { [MainTexture] _MainTex("Photograph",2D)="white" {} }
 SubShader
 {
  Tags { "Queue"="Geometry" "RenderType"="Opaque" }
  Pass
  {
   ZTest LEqual ZWrite On Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; return o; }
   fixed4 frag(v2f i):SV_Target { return tex2D(_MainTex,i.uv); }
   ENDCG
  }
 }
}
