#ifndef WATER_LIT_2D_INCLUDED
#define WATER_LIT_2D_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/CombinedShapeLightShared.hlsl"

Varyings WaterFinishVertex(Attributes input, float3 positionOS)
{
    Varyings o = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.positionCS = TransformObjectToHClip(positionOS);
    o.positionWS = TransformObjectToWorld(positionOS);
    o.screenPos = ComputeScreenPos(o.positionCS);

    o.lightingUV = half2(o.screenPos.xy / o.screenPos.w);
    o.uv = input.uv;
    return o;
}

half4 WaterFinishFragment(half3 albedo, half alpha, half4 mask, half3 normalTS, Varyings input)
{
    SurfaceData2D surfaceData;
    InputData2D inputData;

    InitializeSurfaceData(albedo, alpha, mask, normalTS, surfaceData);

    float2 lightingUV = input.screenPos.xy / input.screenPos.w;
    InitializeInputData(input.uv, lightingUV, inputData);

    return CombinedShapeLightShared(surfaceData, inputData);
}

half3 WaterGlowEmission(half3 shaded, half4 glowColor, half intensity, half followColor)
{
    return glowColor.rgb * intensity * lerp(1.0, shaded, followColor);
}

half4 WaterFinishGlowFragment(half3 albedo, half alpha, half3 emission, half selfLit, Varyings input)
{
    half4 c = WaterFinishFragment(albedo, alpha, half4(1, 1, 1, 1), half3(0, 0, 1), input);
    c.rgb = lerp(c.rgb, albedo, saturate(selfLit)) + emission;
    return c;
}

#endif
