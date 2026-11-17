Shader "HumanAtlas/Anatomy"
{
 Properties {
  _Tint("Color",Color)=(1,1,1,1)
  _PartData("Part data",2D)="white" {}
  _BoneMask("Bone mask",2D)="black" {}
  _BoneMap("Superficie cortical",2D)="white" {}
  _TextureStrength("Acabado",Range(0,1))=1
  _TextureScale("Repeticiones por metro",Float)=7
  _BrainMask("Máscara de tejido cerebral",2D)="black" {}
  _BrainMap("Albedo cortical cerebral",2D)="white" {}
  _BrainStrength("Acabado cerebral",Range(0,1))=1
  _BrainScale("Repeticiones cerebrales por metro",Float)=13
  _PartColor("Color por pieza",2D)="white" {}
  _TissueMap("Albedo de tejido",2D)="white" {}
  _TissueMap2("Segundo albedo de lote",2D)="white" {}
  _TissueStrength("Acabado de tejido",Range(0,1))=.75
  _TissueStrength2("Acabado segundo tejido",Range(0,1))=.75
  _TissueScale("Repeticiones de tejido por metro",Float)=13
  _TissueScale2("Repeticiones segundo tejido por metro",Float)=13
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
 #pragma multi_compile_local _ ATLAS_BONE_SURFACE ATLAS_BRAIN_SURFACE ATLAS_TISSUE_SURFACE
 #pragma target 3.0
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_PartData); SAMPLER(sampler_PartData);
 TEXTURE2D(_BoneMask); SAMPLER(sampler_BoneMask);
 TEXTURE2D(_BrainMask); SAMPLER(sampler_BrainMask);
 TEXTURE2D(_PartColor); SAMPLER(sampler_PartColor);
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint;
 float _TextureScale;
 float _TextureStrength;
 float _BrainScale;
 float _BrainStrength;
 float _TissueScale;
 float _TissueScale2;
 float _TissueStrength;
 float _TissueStrength2;
 CBUFFER_END
 #include "CorticalBone.hlsl"
 #include "BrainTissue.hlsl"
 #include "TissueSurface.hlsl"
 struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 part:TEXCOORD1; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct Varyings {
  float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float state:TEXCOORD1;
  #if defined(ATLAS_BONE_SURFACE) || defined(ATLAS_BRAIN_SURFACE) || defined(ATLAS_TISSUE_SURFACE)
  float3 positionOS:TEXCOORD2; float3 normalOS:TEXCOORD3; float surfaceMask:TEXCOORD4;
  #endif
  #if defined(ATLAS_BONE_SURFACE)
  half ao:TEXCOORD5;
  #endif
  #if defined(ATLAS_BRAIN_SURFACE)
  float3 positionWS:TEXCOORD5;
  #endif
  #if defined(ATLAS_TISSUE_SURFACE)
  half4 partColor:TEXCOORD5;
  #endif
  UNITY_VERTEX_OUTPUT_STEREO
 };
 Varyings vert(Attributes v) {
  Varyings o;
  UNITY_SETUP_INSTANCE_ID(v);
  UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  float4 p=SAMPLE_TEXTURE2D_LOD(_PartData,sampler_PartData,float2(v.part.x,.5),0);
  o.positionCS=TransformObjectToHClip(v.positionOS.xyz+p.xyz);
  o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.state=p.w;
  #if defined(ATLAS_BONE_SURFACE) || defined(ATLAS_BRAIN_SURFACE) || defined(ATLAS_TISSUE_SURFACE)
  o.positionOS=v.positionOS.xyz; o.normalOS=v.normalOS;
  #endif
  #if defined(ATLAS_BONE_SURFACE)
  o.ao=v.color.r;
  o.surfaceMask=SAMPLE_TEXTURE2D_LOD(_BoneMask,sampler_BoneMask,float2(v.part.x,.5),0).r;
  #endif
  #if defined(ATLAS_BRAIN_SURFACE)
  o.positionWS=TransformObjectToWorld(v.positionOS.xyz+p.xyz);
  o.surfaceMask=SAMPLE_TEXTURE2D_LOD(_BrainMask,sampler_BrainMask,float2(v.part.x,.5),0).r;
  #endif
  #if defined(ATLAS_TISSUE_SURFACE)
  o.partColor=SAMPLE_TEXTURE2D_LOD(_PartColor,sampler_PartColor,float2(v.part.x,.5),0);
  o.surfaceMask=o.partColor.a;
  #endif
  return o;
 }
 half4 frag(Varyings i):SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  clip(i.state-.5);
  half3 baseColor=_Tint.rgb;
  #if defined(ATLAS_BONE_SURFACE)
  if (i.surfaceMask > .5) baseColor=lerp(baseColor,CorticalColor(i.positionOS,i.normalOS)*i.ao,_TextureStrength);
  #endif
  half sheen=0;
  #if defined(ATLAS_BRAIN_SURFACE)
  if (i.surfaceMask > .5) {
   baseColor=lerp(baseColor,BrainTissueColor(i.positionOS,i.normalOS),_BrainStrength);
   sheen=BrainTissueSheen(i.normalWS,i.positionWS)*_BrainStrength;
  }
  #endif
  #if defined(ATLAS_TISSUE_SURFACE)
  baseColor=i.partColor.rgb;
  if (i.surfaceMask > .75)
   baseColor=lerp(baseColor,AtlasSecondaryTissueColor(i.positionOS,i.normalOS),_TissueStrength2);
  else if (i.surfaceMask > .25)
   baseColor=lerp(baseColor,AtlasTissueColor(i.positionOS,i.normalOS),_TissueStrength);
  #endif
  baseColor=lerp(baseColor,half3(.1,.9,.95),saturate(i.state-1));
  return half4(baseColor*BoneLight(i.normalWS)+sheen,1);
 }
 ENDHLSL
 }
 }
}
