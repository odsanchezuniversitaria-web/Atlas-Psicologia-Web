Shader "HumanAtlas/MandibleBone"
{
 Properties {
  _BaseColor("Color",Color)=(0.91,0.88,0.76,1)
  _BoneMap("Superficie cortical",2D)="white" {}
  _TextureStrength("Acabado mejorado",Range(0,1))=1
  _TextureScale("Escala",Float)=35
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
   TEXTURE2D(_BoneMap); SAMPLER(sampler_BoneMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseColor;
    float _TextureStrength;
    float _TextureScale;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionOS:TEXCOORD0; float3 normalOS:TEXCOORD1; float3 normalWS:TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings vert(Attributes v) {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
    o.positionOS=v.positionOS.xyz;
    o.normalOS=v.normalOS;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS);
    return o;
   }
   half4 frag(Varyings i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 w=pow(abs(normalize(i.normalOS)),4);
    w/=max(w.x+w.y+w.z,0.0001);
    float3 p=i.positionOS*_TextureScale;
    half3 sampleColor=SAMPLE_TEXTURE2D(_BoneMap,sampler_BoneMap,p.yz).rgb*w.x
      +SAMPLE_TEXTURE2D(_BoneMap,sampler_BoneMap,p.xz).rgb*w.y
      +SAMPLE_TEXTURE2D(_BoneMap,sampler_BoneMap,p.xy).rgb*w.z;
    half3 albedo=_BaseColor.rgb*lerp(half3(1,1,1),sampleColor*1.16,_TextureStrength);
    float3 n=normalize(i.normalWS);
    float diffuse=saturate(dot(n,normalize(float3(-.35,.7,-1))));
    float soft=saturate(dot(n,normalize(float3(.5,.2,1))));
    return half4(albedo*(.52+.48*diffuse+.10*soft),1);
   }
   ENDHLSL
  }
 }
}