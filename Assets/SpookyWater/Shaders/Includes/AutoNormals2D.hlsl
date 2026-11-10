#ifndef SPOOKYWATER_AUTO_NORMALS_2D_INCLUDED
#define SPOOKYWATER_AUTO_NORMALS_2D_INCLUDED

half AutoNormalHeight(half4 c)
{
    return dot(c.rgb, half3(0.299h, 0.587h, 0.114h)) * c.a;
}

half3 AutoNormalTS(TEXTURE2D_PARAM(tex, samp), float2 uv, float2 texel, half edgeWidth, half edgeBevel, half detailWidth, half detailBumps)
{
    float2 e = texel * max(edgeWidth, 0.5h);
    half edgeBias = max(log2(max(edgeWidth, 1.0h)) - 1.0h, 0.0h);
    half ax = SAMPLE_TEXTURE2D_BIAS(tex, samp, uv + float2(e.x, 0), edgeBias).a - SAMPLE_TEXTURE2D_BIAS(tex, samp, uv - float2(e.x, 0), edgeBias).a;
    half ay = SAMPLE_TEXTURE2D_BIAS(tex, samp, uv + float2(0, e.y), edgeBias).a - SAMPLE_TEXTURE2D_BIAS(tex, samp, uv - float2(0, e.y), edgeBias).a;

    float2 d = texel * max(detailWidth, 0.5h);
    half lx = AutoNormalHeight(SAMPLE_TEXTURE2D(tex, samp, uv + float2(d.x, 0))) - AutoNormalHeight(SAMPLE_TEXTURE2D(tex, samp, uv - float2(d.x, 0)));
    half ly = AutoNormalHeight(SAMPLE_TEXTURE2D(tex, samp, uv + float2(0, d.y))) - AutoNormalHeight(SAMPLE_TEXTURE2D(tex, samp, uv - float2(0, d.y)));

    half2 slope = half2(ax, ay) * edgeBevel + half2(lx, ly) * detailBumps;
    return normalize(half3(-slope, 1.0h));
}

half3 BlendAutoNormal(half3 autoNormal, half3 mapNormal, half mapWeight)
{
    half3 n = half3(autoNormal.xy + mapNormal.xy * mapWeight, autoNormal.z * lerp(1.0h, mapNormal.z, mapWeight));
    return normalize(n);
}

#endif
