#ifndef WATER_REFLECTION_INCLUDED
#define WATER_REFLECTION_INCLUDED

TEXTURE2D(_PlanarReflectionTexture); SAMPLER(sampler_PlanarReflectionTexture);
float _WaterViewedFromBelow;

half4 SampleReflectionBlurred(float2 uv, float radius)
{
    half4 c = SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv);
    c += SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv + float2(radius, 0));
    c += SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv - float2(radius, 0));
    c += SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv + float2(0, radius));
    c += SAMPLE_TEXTURE2D(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv - float2(0, radius));
    return c * (1.0 / 5.0);
}

half4 SampleReflectionSmeared(float2 uv, float2 halfSize, float radius)
{
    half4 c = SampleReflectionBlurred(uv, radius);
    [branch] if (halfSize.x + halfSize.y > 1e-5)
    {
        c *= 0.2;
        [unroll] for (int i = -2; i <= 2; i++)
            if (i != 0) c += 0.2 * SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture,
                uv + float2(halfSize.x * i * 0.5, halfSize.y * (abs(i) == 1 ? -0.6 : 0.6)), 0);
    }
    return c;
}

#endif
