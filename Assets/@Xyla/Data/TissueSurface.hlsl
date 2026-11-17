TEXTURE2D(_TissueMap); SAMPLER(sampler_TissueMap);
TEXTURE2D(_TissueMap2); SAMPLER(sampler_TissueMap2);

half3 AtlasTissueColor(float3 positionOS, float3 normalOS)
{
    float3 axis = abs(normalOS);
    float3 p = positionOS * _TissueScale;
    [branch] if (axis.x >= axis.y && axis.x >= axis.z)
        return SAMPLE_TEXTURE2D(_TissueMap, sampler_TissueMap, p.yz).rgb;
    [branch] if (axis.y >= axis.z)
        return SAMPLE_TEXTURE2D(_TissueMap, sampler_TissueMap, p.xz).rgb;
    return SAMPLE_TEXTURE2D(_TissueMap, sampler_TissueMap, p.xy).rgb;
}

half3 AtlasSecondaryTissueColor(float3 positionOS, float3 normalOS)
{
    float3 axis = abs(normalOS);
    float3 p = positionOS * _TissueScale2;
    [branch] if (axis.x >= axis.y && axis.x >= axis.z)
        return SAMPLE_TEXTURE2D(_TissueMap2, sampler_TissueMap2, p.yz).rgb;
    [branch] if (axis.y >= axis.z)
        return SAMPLE_TEXTURE2D(_TissueMap2, sampler_TissueMap2, p.xz).rgb;
    return SAMPLE_TEXTURE2D(_TissueMap2, sampler_TissueMap2, p.xy).rgb;
}

half AtlasTissueSheen(float3 normalWS, float3 positionWS, half smoothness)
{
    float3 n = normalize(normalWS);
    float3 view = normalize(_WorldSpaceCameraPos.xyz - positionWS);
    float3 light = normalize(float3(-.35, .7, -1));
    float3 halfway = normalize(view + light);
    half spec = saturate(dot(n, halfway));
    half spec2 = spec * spec;
    half spec4 = spec2 * spec2;
    return (.012 + .08 * smoothness) * spec4 * spec4;
}
