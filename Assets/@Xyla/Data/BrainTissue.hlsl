TEXTURE2D(_BrainMap); SAMPLER(sampler_BrainMap);

half3 BrainTissueColor(float3 positionOS, float3 normalOS)
{
    float3 axis = abs(normalOS);
    float3 p = positionOS * _BrainScale;
    [branch] if (axis.x >= axis.y && axis.x >= axis.z)
        return SAMPLE_TEXTURE2D(_BrainMap, sampler_BrainMap, p.yz).rgb;
    [branch] if (axis.y >= axis.z)
        return SAMPLE_TEXTURE2D(_BrainMap, sampler_BrainMap, p.xz).rgb;
    return SAMPLE_TEXTURE2D(_BrainMap, sampler_BrainMap, p.xy).rgb;
}

half BrainTissueSheen(float3 normalWS, float3 positionWS)
{
    float3 n = normalize(normalWS);
    float3 view = normalize(_WorldSpaceCameraPos.xyz - positionWS);
    float3 light = normalize(float3(-.35, .7, -1));
    float3 halfway = normalize(view + light);
    half spec = saturate(dot(n, halfway));
    half spec2 = spec * spec;
    half spec4 = spec2 * spec2;
    return .075 * spec4 * spec4 * spec4;
}
