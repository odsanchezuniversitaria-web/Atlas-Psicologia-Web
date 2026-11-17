Shader "HumanAtlas/BrainTissue"
{
 Properties {
  _BaseColor("Tinte del tejido",Color)=(1,1,1,1)
  _BrainMap("Albedo cortical",2D)="white" {}
  _BrainScale("Repeticiones por metro",Float)=13
  _BrainStrength("Intensidad del acabado",Range(0,1))=1
 }
 SubShader {
 Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
 Pass {
 Tags { "LightMode"="UniversalForward" }
 Cull Off
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma target 3.0
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor;
 float _BrainScale;
 float _BrainStrength;
 CBUFFER_END
 #include "BrainTissue.hlsl"
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct Varyings {
  float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0;
  float3 normalOS:TEXCOORD1; float3 normalWS:TEXCOORD2;
  float3 positionWS:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO
 };
 Varyings vert(Attributes v) {
  Varyings o;
  UNITY_SETUP_INSTANCE_ID(v);
  UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
  o.positionOS=v.positionOS.xyz; o.normalOS=v.normalOS;
  o.normalWS=TransformObjectToWorldNormal(v.normalOS);
  o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
  return o;
 }
 half4 frag(Varyings i):SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  half3 color=lerp(half3(.72,.51,.48),BrainTissueColor(i.positionOS,i.normalOS),_BrainStrength);
  half light=.52+.45*saturate(dot(normalize(i.normalWS),normalize(float3(-.35,.7,-1))));
  return half4(color*_BaseColor.rgb*light+BrainTissueSheen(i.normalWS,i.positionWS),1);
 }
 ENDHLSL
 }
 }
}
