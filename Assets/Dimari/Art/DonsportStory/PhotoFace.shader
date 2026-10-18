Shader "Dimari/PhotoFaceDepth"
{
 Properties { _MainTex("Face",2D)="white" {} }
 SubShader
 {
  Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
  Pass
  {
   ZTest LEqual ZWrite Off Cull Back Offset -1,-1
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4x4 _FaceWorldToLocal;
   struct appdata { float4 vertex:POSITION; };
   struct v2f { float4 pos:SV_POSITION; float3 face:TEXCOORD0; };
   v2f vert(appdata v) { v2f o; float4 world=mul(unity_ObjectToWorld,v.vertex); o.pos=UnityObjectToClipPos(v.vertex); o.face=mul(_FaceWorldToLocal,world).xyz; return o; }
   fixed4 frag(v2f i):SV_Target
   {
    float2 face=i.face.xy/float2(0.09,0.125);
    clip(1-dot(face,face)); clip(i.face.z-0.015); clip(0.16-i.face.z);
    return tex2D(_MainTex,face*0.5+0.5);
   }
   ENDCG
  }
 }
}
