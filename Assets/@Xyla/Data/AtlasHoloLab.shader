Shader "HumanAtlas/HoloLab"
{
 Properties { _Dots("Soft dots",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
   ZWrite Off Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
   struct V {float4 positionCS:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;float3 positionWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
   CBUFFER_START(UnityPerMaterial)
   float _Dots;
   CBUFFER_END
   V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;o.uv=i.uv;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);return o;}
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half2 p=abs(i.uv-.5);
    half diamond=p.x+p.y;
    half hologram=saturate(1-abs(diamond-.30)*18)+.35*saturate(1-diamond*2.2);
    half dot=saturate(1-length(i.uv-.5)*2);
    half shape=_Dots>1.5?saturate(hologram):lerp(1,dot,_Dots);
    half pulse=.8+.2*sin(_Time.y*.85+i.uv.x*6.28);
    half scan=pow(saturate(1-abs(frac(i.positionWS.y*.32-_Time.y*.055)-.5)*8),3);
    pulse+=scan*.35;
    return half4(i.color.rgb*1.15,i.color.a*shape*shape*pulse);
   }
   ENDHLSL
  }
 }
}