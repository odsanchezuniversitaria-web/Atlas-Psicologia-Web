Shader "HumanAtlas/CorticalBone"
{
 Properties {
  _BaseColor("Color",Color)=(1,1,1,1)
  _BoneMap("Superficie cortical",2D)="white" {}
  _TextureStrength("Acabado",Range(0,1))=1
  _TextureScale("Repeticiones por metro",Float)=7
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
    float _TextureStrength;
    float _TextureScale;
   CBUFFER_END
   #include "CorticalBone.hlsl"
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalOS:TEXCOORD1; float3 normalWS:TEXCOORD2; half ao:TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings vert(Attributes v) {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    o.positionOS=v.positionOS.xyz; o.normalOS=v.normalOS; o.ao=v.color.r;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS);
    return o;
   }
   half4 frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half3 albedo = lerp(half3(.91,.88,.76), CorticalColor(i.positionOS,i.normalOS), _TextureStrength);
    return half4(albedo * _BaseColor.rgb * BoneLight(i.normalWS) * lerp(1,i.ao,_TextureStrength),1);
   }
   ENDHLSL
  }
 }
}