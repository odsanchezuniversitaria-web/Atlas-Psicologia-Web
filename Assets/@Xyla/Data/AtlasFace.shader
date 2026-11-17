Shader "HumanAtlas/AssistantFace"
{
 Properties {
  _FaceColor("Neon",Color)=(0.02,0.92,1,1)
  _Mood("Expression",Float)=1
  _Talk("Speech",Range(0,1))=0
  _Blink("Blink",Range(0,1))=0
  _Aspect("Aspect",Float)=2.04
  _Clock("Animation",Float)=0
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
  Pass {
   Blend SrcAlpha OneMinusSrcAlpha
   Cull Off ZWrite Off ZTest LEqual
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #pragma target 3.0
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _FaceColor;
   float _Mood, _Talk, _Blink, _Aspect, _Clock;
   CBUFFER_END
   struct A { float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex=TransformObjectToHClip(v.vertex.xyz);o.uv=v.uv;return o; }
   float sdSegment(float2 p,float2 a,float2 b) {
    float2 pa=p-a,ba=b-a;return length(pa-ba*saturate(dot(pa,ba)/max(dot(ba,ba),.0001)));
   }
   float eye(float2 p,float side) {
    float2 q=p-float2(side*.76,.23);
    q.y/=max(.08,1-_Blink*.94);
    if(_Mood<.5) return sdSegment(q,float2(-.14,0),float2(.14,-.01))-.025;
    if(abs(_Mood-5)<.4) { // >w<
     return min(sdSegment(q,float2(side*.14,.13),float2(-side*.10,0)),
      sdSegment(q,float2(-side*.10,0),float2(side*.14,-.13)))-.036;
    }
    if(abs(_Mood-7)<.4) { // smiling arches
     return min(sdSegment(q,float2(-.17,0),float2(0,.13)),sdSegment(q,float2(0,.13),float2(.17,0)))-.04;
    }
    if(abs(_Mood-8)<.4) return sdSegment(q,float2(-.14,-side*.065),float2(.14,side*.065))-.035;
    if(abs(_Mood-9)<.4 && side<0) return sdSegment(q,float2(-.16,0),float2(.16,.02))-.032;
    if(abs(_Mood-3)<.4 && side<0) return sdSegment(q,float2(-.14,-.015),float2(.14,.045))-.032;
    float radius=abs(_Mood-2)<.4 ? .22 : abs(_Mood-10)<.4 ? .235 : .18;
    if(abs(_Mood-6)<.4) { q.y-=side*.075;radius=side>0 ? .13 : .20; }
    q.x-=sin(_Clock*.85)*.02;
    return length(q/float2(1,1.14))*1.0-radius;
   }
   float omega(float2 p) {
    float d=100;float2 left=float2(-.31,-.16),right=float2(0,-.27);
    [unroll] for(int k=1;k<=6;k++) {
     float t=k/6.0,u=1-t;
     float2 a=u*u*float2(-.31,-.16)+2*u*t*float2(-.34,-.64)+t*t*float2(0,-.27);
     float2 b=u*u*float2(0,-.27)+2*u*t*float2(.34,-.64)+t*t*float2(.31,-.16);
     d=min(d,min(sdSegment(p,left,a),sdSegment(p,right,b)));left=a;right=b;
    }
    return d-.036;
   }
   half4 frag(V i):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float2 p=(i.uv-.5)*2*float2(_Aspect,1);
    float mouth=omega(p);
    if(abs(_Mood-3)<.4) mouth=abs(length((p-float2(.10,-.30))*float2(1,1.6))-.07)-.026;
    if(abs(_Mood-6)<.4) mouth=sdSegment(p,float2(-.14,-.31),float2(.16,-.26))-.032;
    if(abs(_Mood-8)<.4) mouth=min(sdSegment(p,float2(-.21,-.37),float2(0,-.23)),sdSegment(p,float2(0,-.23),float2(.21,-.37)))-.03;
    float opening=_Talk;
    if(abs(_Mood-10)<.4) opening=max(opening,.65);
    float2 q=p-float2(0,-.31);
    float2 radii=float2(.16+opening*.09,.055+opening*.22);
    float oval=(length(q/radii)-1)*min(radii.x,radii.y)-.027;
    mouth=lerp(mouth,oval,smoothstep(.08,.27,opening));
    float d=min(min(eye(p,-1),eye(p,1)),mouth);
    if(abs(_Mood-4)<.4) {
     [unroll] for(int j=0;j<9;j++) {
      float h=.015+opening*(.035+.06*abs(sin(_Clock*8+j*1.7)));
      d=min(d,sdSegment(p,float2((j-4)*.093,-.73-h),float2((j-4)*.093,-.73+h))-.011);
     }
    }
    float aa=max(fwidth(d),.0025);
    float body=1-smoothstep(-aa,aa,d);
    float glow=exp2(-max(d,0)*23)*.48;
    float core=1-smoothstep(-.065,-.005,d);
    half3 bg=half3(.003,.012,.025);
    float vignette=saturate(1-length((i.uv-.5)*1.3));
    half3 color=bg+_FaceColor.rgb*(body+glow)*( .90+.06*sin(_Clock*1.4) );
    color=lerp(color,lerp(_FaceColor.rgb,half3(1,1,1),.78),core*.48);
    color+=_FaceColor.rgb*vignette*.014;
    if(_Mood<.5)color=lerp(bg,color,.36);
    return half4(color,1);
   }
   ENDHLSL
  }
 }
}