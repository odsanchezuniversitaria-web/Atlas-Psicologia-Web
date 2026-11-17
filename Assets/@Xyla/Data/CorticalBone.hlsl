TEXTURE2D(_BoneMap); SAMPLER(sampler_BoneMap);
half3 CorticalColor(float3 positionOS, float3 normalOS)
{
    float3 w = pow(abs(normalize(normalOS)), 4);
    w /= max(w.x + w.y + w.z, .0001);
    float3 p = positionOS * _TextureScale;
    half3 surface = SAMPLE_TEXTURE2D(_BoneMap, sampler_BoneMap, p.yz).rgb * w.x
         + SAMPLE_TEXTURE2D(_BoneMap, sampler_BoneMap, p.xz).rgb * w.y
         + SAMPLE_TEXTURE2D(_BoneMap, sampler_BoneMap, p.xy).rgb * w.z;
    return surface * half3(1.0, .91, .77);
}
half BoneLight(float3 normalWS)
{
    float3 n = normalize(normalWS);
    return .55 + .43 * saturate(dot(n, normalize(float3(-.35,.7,-1))))
               + .09 * saturate(dot(n, normalize(float3(.5,.2,1))));
}
