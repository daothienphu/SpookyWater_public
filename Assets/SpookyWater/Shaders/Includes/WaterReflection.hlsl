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

// Soft disc blur: 16 taps on a golden-angle spiral with Gaussian falloff, rotated per pixel so the
// taps never line up into ghost copies. Radius is a fraction of screen height (round on any aspect).
half4 SampleReflectionDiscBlur(float2 uv, float radius)
{
    [branch] if (radius < 1e-5)
        return SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture, uv, 0);

    float2 pixel = uv * _ScreenParams.xy;
    float rotation = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715)))) * 6.2831853;
    float2 scale = float2(radius * _ScreenParams.y / _ScreenParams.x, radius);

    half4 sum = 0;
    half weightSum = 0;
    [unroll] for (int i = 0; i < 16; i++)
    {
        float r = sqrt((i + 0.5) / 16.0);
        float angle = i * 2.39996323 + rotation;
        half w = exp(-2.0 * r * r);
        sum += SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTexture, sampler_PlanarReflectionTexture,
            uv + float2(cos(angle), sin(angle)) * r * scale, 0) * w;
        weightSum += w;
    }
    return sum / weightSum;
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
