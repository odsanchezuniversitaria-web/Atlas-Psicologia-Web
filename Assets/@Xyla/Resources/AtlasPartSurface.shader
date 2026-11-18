Shader "HumanAtlas/PartSurface"
{
 Properties
 {
  _BaseColor("Color de selección", Color) = (0,0,0,0)
  _PartData("Estado por pieza", 2D) = "white" {}
  _SurfaceRows("Datos de tejido por pieza", 2D) = "black" {}
  _DetailMap("Detalle de tejido (lineal)", 2D) = "gray" {}
  _MuscleMap("Músculo esquelético", 2D) = "white" {}
  _HeartMap("Miocardio", 2D) = "white" {}
  _KidneyMap("Riñón", 2D) = "white" {}
  _LiverMap("Hígado", 2D) = "white" {}
  _SkinMap("Piel", 2D) = "white" {}
  _BoneMap("Hueso cortical", 2D) = "white" {}
  _BrainMap("Superficie cerebral", 2D) = "white" {}
  _MuscleParams("Escala e intensidad muscular", Vector) = (13,.72,0,0)
  _HeartParams("Escala e intensidad cardíaca", Vector) = (13,.72,0,0)
  _KidneyParams("Escala e intensidad renal", Vector) = (13,.72,0,0)
  _LiverParams("Escala e intensidad hepática", Vector) = (13,.72,0,0)
  _SkinParams("Escala e intensidad cutánea", Vector) = (13,.72,0,0)
  _BoneParams("Escala e intensidad ósea", Vector) = (7,1,0,0)
  _BrainParams("Escala e intensidad cerebral", Vector) = (13,1,0,0)
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   Cull Off
   HLSLPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma multi_compile_local _ ATLAS_LOOSE_PIECE
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

   TEXTURE2D(_PartData); SAMPLER(sampler_PartData);
   TEXTURE2D(_SurfaceRows); SAMPLER(sampler_SurfaceRows);
   TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);
   TEXTURE2D(_MuscleMap); SAMPLER(sampler_MuscleMap);
   TEXTURE2D(_HeartMap); SAMPLER(sampler_HeartMap);
   TEXTURE2D(_KidneyMap); SAMPLER(sampler_KidneyMap);
   TEXTURE2D(_LiverMap); SAMPLER(sampler_LiverMap);
   TEXTURE2D(_SkinMap); SAMPLER(sampler_SkinMap);
   TEXTURE2D(_BoneMap); SAMPLER(sampler_BoneMap);
   TEXTURE2D(_BrainMap); SAMPLER(sampler_BrainMap);
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
    float4 _MuscleParams, _HeartParams, _KidneyParams, _LiverParams, _SkinParams, _BoneParams, _BrainParams;
   CBUFFER_END

   struct Attributes {
    float4 pos:POSITION; float3 normal:NORMAL; float2 part:TEXCOORD1; half4 color:COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct Varyings {
    float4 clipPos:SV_POSITION;
    float3 normalWS:TEXCOORD0;
    float3 posOS:TEXCOORD1;
    float3 normalOS:TEXCOORD2;
    half4 tissue:TEXCOORD3;
    float2 plane:TEXCOORD4;
    half3 viewWS:TEXCOORD5;
    float3 stateAoTendon:TEXCOORD6;
    UNITY_VERTEX_OUTPUT_STEREO
   };
   Varyings vert(Attributes v)
   {
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 position = v.pos.xyz;
    float state = 1;
    #ifndef ATLAS_LOOSE_PIECE
     float4 shift = SAMPLE_TEXTURE2D_LOD(_PartData,sampler_PartData,float2(v.part.x,.5),0);
     position += shift.xyz;
     state = shift.w;
    #endif
    o.clipPos = TransformObjectToHClip(position);
    o.posOS = v.pos.xyz;
    o.normalOS = v.normal;
    o.normalWS = TransformObjectToWorldNormal(v.normal);
    o.viewWS = GetWorldSpaceNormalizeViewDir(TransformObjectToWorld(position));
    o.tissue = SAMPLE_TEXTURE2D_LOD(_SurfaceRows,sampler_SurfaceRows,float2(v.part.x,.125),0);
    o.plane = 0;
    o.stateAoTendon = float3(state, v.color.r, 0);
    half code = o.tissue.a;
    if (code == 1 || code == 2 || code == 7 || (code >= 15 && code <= 17) || code == 20)
    {
     float4 wide = SAMPLE_TEXTURE2D_LOD(_SurfaceRows,sampler_SurfaceRows,float2(v.part.x,.375),0);
     float4 along = SAMPLE_TEXTURE2D_LOD(_SurfaceRows,sampler_SurfaceRows,float2(v.part.x,.625),0);
     o.plane = float2(dot(v.pos,wide), dot(v.pos,along));
     if (code == 20) {
      float2 limits = SAMPLE_TEXTURE2D_LOD(_SurfaceRows,sampler_SurfaceRows,float2(v.part.x,.875),0).xy;
      o.stateAoTendon.z = 1 - smoothstep(limits.x, limits.y, dot(v.pos.xyz, along.xyz));
     }
    }
    return o;
   }
   float2 projection(float3 p, float3 n)
   {
    float3 a = abs(n);
    if (a.x >= a.y && a.x >= a.z) return p.yz;
    return a.y >= a.z ? p.xz : p.xy;
   }

   half4 frag(Varyings i):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(i.stateAoTendon.x-.5);
    half code = round(i.tissue.a);
    half3 albedo = i.tissue.rgb;
    half gloss = .018h;
    float2 uv = projection(i.posOS,i.normalOS);
    [branch] if (code == 1 || (code >= 15 && code <= 17) || code == 20)
    {
     float2 fiber = i.plane;
     if (code == 15) fiber.x = atan2(fiber.x, max(.012,fiber.y)) * .075;
     if (code == 16) fiber.x = length(fiber * float2(.8,1.0));
     if (code == 17) fiber.x = abs(fiber.x) - .34 * fiber.y;
     half3 photo = SAMPLE_TEXTURE2D(_MuscleMap,sampler_MuscleMap,fiber.yx * _MuscleParams.x).rgb;
     half4 grain = SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,fiber * 6.5);
     albedo = lerp(albedo, photo, _MuscleParams.y * .82) * (1 + (grain.r - .5) * .39);
     half3 tendon = half3(.69,.62,.49) * (1+(grain.a-.5)*.19);
     albedo = lerp(albedo,tendon,i.stateAoTendon.z);
     gloss = .026;
    }
    else if (code == 11)
    {
     // The existing cortical albedo already contains fine porosity.
     half3 bone = SAMPLE_TEXTURE2D(_BoneMap,sampler_BoneMap,uv*_BoneParams.x).rgb;
     albedo = lerp(albedo, bone * half3(1,.94,.83) * lerp(.72,1,i.stateAoTendon.y),_BoneParams.y);
    }
    else if (code == 12) {
     albedo = lerp(albedo,SAMPLE_TEXTURE2D(_BrainMap,sampler_BrainMap,uv*_BrainParams.x).rgb,_BrainParams.y*.78);
     gloss = .052;
    }
    else if (code == 10) {
     albedo = lerp(albedo,SAMPLE_TEXTURE2D(_HeartMap,sampler_HeartMap,uv*_HeartParams.x).rgb,_HeartParams.y);
     gloss = .042;
    }
    else if (code == 9) {
     albedo = lerp(albedo,SAMPLE_TEXTURE2D(_SkinMap,sampler_SkinMap,uv*_SkinParams.x).rgb,_SkinParams.y);
     half pores = SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,uv*17).b;
     albedo *= 1+(pores-.5)*.12;
     gloss = .013;
    }
    else if (code == 18) {
     albedo = lerp(albedo,SAMPLE_TEXTURE2D(_LiverMap,sampler_LiverMap,uv*_LiverParams.x).rgb,_LiverParams.y);
     gloss = .058;
    }
    else if (code == 19) {
     albedo = lerp(albedo,SAMPLE_TEXTURE2D(_KidneyMap,sampler_KidneyMap,uv*_KidneyParams.x).rgb,_KidneyParams.y);
     gloss = .048;
    }
    else if (code != 0)
    {
     bool fibrous = code == 2 || code == 7;
     half4 detail = SAMPLE_TEXTURE2D(_DetailMap,sampler_DetailMap,fibrous ? i.plane * 9 : uv * 15);
     half grain = fibrous ? detail.a : (code == 6 || code == 8 ? detail.b : detail.g);
     half strength = fibrous ? .25 : (code == 8 ? .035 : (code == 3 || code == 14 ? .105 : .21));
     albedo *= 1 + (grain-.5) * strength;
     gloss = code == 8 ? .09 : (code == 5 || code == 6 || code == 14 ? .052 : .03);
    }
    float3 n = normalize(i.normalWS);
    const float3 key = float3(-.276,.551,-.788);
    half diffuse = .56 + .40*saturate(dot(n,key)) + .11*saturate(dot(n,float3(.44,.18,.88)));
    half spec = saturate(dot(n,normalize(i.viewWS+key)));
    spec *= spec; spec *= spec; spec *= spec; spec *= spec;
    half3 color = albedo * diffuse + gloss * spec;
    #ifdef ATLAS_LOOSE_PIECE
     color = lerp(color,_BaseColor.rgb*diffuse,saturate(_BaseColor.a)*.72);
    #else
     color = lerp(color,half3(.1,.9,.95)*diffuse,saturate(i.stateAoTendon.x-1)*.8);
    #endif
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}

