Shader "SpookyWater/Water Front"
{
    Properties
    {
        [HideInInspector] _RippleScale("Ripple Scale", Float) = 1
        [Section(Surface)] _EdgeFalloffWidth("Edge Falloff Width", Range(0.001, 0.25)) = 0.03
        _VerticalFalloffPower("Vertical Falloff Power", Range(0.1, 6)) = 1.6

        [Section(Color)] _ShallowColor("Shallow Color", Color) = (0.55, 0.85, 0.85, 1)
        _DeepColor("Deep Color", Color) = (0.043, 0.145, 0.235, 1)
        _DepthGradientPower("Depth Gradient Power", Range(0.1, 6)) = 1.6
        _WaterDepthRange("Depth Gradient Range", Float) = 6.5

        [Section(Clarity)] _Clarity("Clarity", Range(0, 1)) = 0.6
        _ClarityDepthLoss("Clarity Depth Loss", Range(0, 1)) = 0.6
        _TransmissionTint("Transmission Tint", Color) = (0.45, 1, 0.9, 1)
        _SurfaceGlowColor("Surface Glow Color", Color) = (0.1, 0.6, 0.55, 1)
        _SurfaceGlowDepth("Surface Glow Depth", Float) = 1.2

        [Section(Glow)] _GlowIntensity("Glow Intensity", Range(0, 8)) = 0
        [ShowIf(_GlowIntensity)] [HDR] _GlowColor("Glow Color", Color) = (0.3, 0.6, 1, 1)
        [ShowIf(_GlowIntensity)] _GlowSelfLit("Self Illumination", Range(0, 1)) = 0.5
        [ShowIf(_GlowIntensity)] _GlowFollowColor("Glow Follows Color", Range(0, 1)) = 0.5
        [ShowIf(_GlowIntensity)] _GlowDepth("Glow Depth", Float) = 2
        [ShowIf(_GlowIntensity)] _GlowDeepAmount("Deep Glow", Range(0, 1)) = 0.1

        [Section(Mist)] _MistAmount("Mist Amount", Range(0, 1)) = 0
        [ShowIf(_MistAmount)] [HDR] _MistColor("Mist Lit Color", Color) = (0.22, 0.55, 0.85, 1)
        [ShowIf(_MistAmount)] _MistDeepColor("Mist Deep Color", Color) = (0.03, 0.08, 0.16, 1)
        [ShowIf(_MistAmount)] _MistLightDepth("Mist Light Depth", Float) = 3
        [ShowIf(_MistAmount)] _MistDepthThickening("Mist Depth Thickening", Range(0, 1)) = 0.4
        [ShowIf(_MistAmount)] _MistBreakup("Mist Breakup", Range(0, 1)) = 0.35
        [ShowIf(_MistAmount)] _MistScale("Mist Scale", Float) = 0.3
        [ShowIf(_MistAmount)] _MistSpeed("Mist Drift Speed", Float) = 0.15

        [Section(Waterline)] _FoamColor("Line Color", Color) = (0.85, 0.98, 1, 1)
        _WaterlinePosition("Waterline Position", Range(0, 1)) = 0.985
        _WaterlineFoamWidth("Waterline Foam Width", Range(0.001, 0.15)) = 0.02
        _WaterlineGlow("Waterline Glow Reach", Range(0, 0.5)) = 0.08
        _WaterlineGlowStrength("Waterline Glow Strength", Range(0, 1)) = 0.35
        _WaterlineThicknessVariation("Thickness Variation", Range(0, 1)) = 0.65
        _WaterlineBrightnessVariation("Brightness Variation", Range(0, 1)) = 0.5
        _WaterlineVariationScale("Variation Scale", Float) = 0.7
        _WaterlineVariationSpeed("Variation Speed", Float) = 0.3
        _WaterlineRippleResponse("Ripple Response", Range(0, 3)) = 1
        _WaterlineCrestBrightness("Crest Brightness", Range(0, 3)) = 0.8

        [Section(Distortion)] _DistortionStrength("Distortion Strength", Range(0, 0.05)) = 0.006
        _DistortionScale("Distortion Scale", Float) = 6
        _DistortionSpeed("Distortion Speed", Float) = 0.4
        _DistortionDir("Sway Direction", Vector) = (0.05, 0.02, 0, 0)
        _DistortionRipple("Ripple Refraction", Range(0, 0.3)) = 0.05
        _DistortionRippleDepth("Ripple Refraction Depth", Float) = 1.5
        _DistortionSurfaceCalm("Surface Calm Depth", Float) = 1.2

        [HideInInspector] _PocketStencilComp("Pocket Stencil Comparison", Float) = 6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite On

        Stencil
        {
            Ref 128
            ReadMask 128
            Comp [_PocketStencilComp]
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 surfaceInfo : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float2 surfaceInfo : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_CameraSortingLayerTexture); SAMPLER(sampler_CameraSortingLayerTexture);
            TEXTURE2D(_WaterDistortionMask); SAMPLER(sampler_WaterDistortionMask);

            float _WaterGlobalSurfaceY;
            float _WaterDistortionMaskOn;
            float _WaterlineMask;

            #define WATER_MAX_POCKET_LINES 8
            float4 _WaterPocketLines[WATER_MAX_POCKET_LINES];
            float _WaterPocketLineCount;

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                half _DepthGradientPower;
                half _WaterDepthRange;
                half _MistAmount;
                half4 _MistColor;
                half4 _MistDeepColor;
                half _MistLightDepth;
                half _MistDepthThickening;
                half _MistBreakup;
                float _MistScale;
                float _MistSpeed;
                half _Clarity;
                half _ClarityDepthLoss;
                half4 _TransmissionTint;
                half4 _SurfaceGlowColor;
                half _SurfaceGlowDepth;
                half _GlowIntensity;
                half4 _GlowColor;
                half _GlowSelfLit;
                half _GlowFollowColor;
                half _GlowDepth;
                half _GlowDeepAmount;
                half _WaterlineGlow;
                half _WaterlineGlowStrength;
                half _WaterlineThicknessVariation;
                half _WaterlineBrightnessVariation;
                float _WaterlineVariationScale;
                float _WaterlineVariationSpeed;
                half _WaterlineRippleResponse;
                half _WaterlineCrestBrightness;
                half _RippleScale;
                half _EdgeFalloffWidth;
                half _VerticalFalloffPower;
                half _WaterlinePosition;
                half _WaterlineFoamWidth;
                half4 _DistortionDir;
                half _DistortionScale;
                half _DistortionStrength;
                half _DistortionSpeed;
                half _DistortionRipple;
                half _DistortionRippleDepth;
                half _DistortionSurfaceCalm;
            CBUFFER_END

            #include "../Includes/WaterCommon.hlsl"
            #include "../Includes/WaterLit2D.hlsl"

            float2 BackgroundDistortion(float3 positionWS, float depthBelow, float t)
            {
                float2 offset = 0;
                if (_DistortionStrength > 0.0)
                {
                    float2 q = positionWS.xy * (_DistortionScale * 0.2);
                    float tt = t * _DistortionSpeed;
                    float2 broad = float2(
                        WaterNoiseD(q + float2(tt * 0.55, tt * 0.2)).x,
                        WaterNoiseD(q + float2(5.2, 1.3) - float2(tt * 0.3, tt * 0.45)).x);
                    float2 fine = float2(
                        WaterNoiseD(q * 2.13 + float2(9.7, 3.1) - float2(tt * 0.9, -tt * 0.4)).x,
                        WaterNoiseD(q * 2.13 + float2(2.9, 7.7) + float2(tt * 0.7, tt * 0.6)).x);
                    offset = (broad + 0.45 * fine) * _DistortionStrength;
                    offset += _DistortionDir.xy * sin(tt * 0.35) * _DistortionStrength;
                }
                if (_DistortionRipple > 0.0)
                {
                    float2 lineXZ = float2(positionWS.x, _WaterFieldOrigin.y);
                    float h = SampleRipple(lineXZ);
                    float2 g = RippleGradient(lineXZ, 0.06);
                    float reach = exp(-depthBelow / max(_DistortionRippleDepth, 1e-3));
                    offset += float2(g.x * 0.5, h * 2.0) * (_DistortionRipple * _RippleScale * reach);
                }
                if (_DistortionSurfaceCalm > 0.0)
                    offset *= smoothstep(0.0, _DistortionSurfaceCalm, depthBelow);
                return offset;
            }

            float PocketLightDepth(float2 positionXY)
            {
                float best = 1e4;
                for (int i = 0; i < min((int)_WaterPocketLineCount, WATER_MAX_POCKET_LINES); i++)
                {
                    float4 p = _WaterPocketLines[i];
                    float side = max(max(p.x - positionXY.x, positionXY.x - p.y), 0.0);
                    float below = p.z - positionXY.y;
                    best = min(best, max(below, 0.0) + max(-below, 0.0) * 3.0 + side * 2.0);
                }
                return best;
            }

            half DistortionMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_WaterDistortionMask, sampler_WaterDistortionMask, uv).r;
            }

            Varyings Vert(Attributes input)
            {
                float3 restWS = TransformObjectToWorld(input.positionOS);
                float2 fieldUV = WaterFieldUV(restWS);

                float2 topWorldXZ = float2(restWS.x, _WaterFieldOrigin.y);
                float h = SampleWaterHeight(topWorldXZ, _RippleScale);
                float verticalFalloff = pow(saturate(input.uv.y), max(_VerticalFalloffPower, 0.001));
                h *= WaterEdgeFalloff1D(fieldUV.x, _EdgeFalloffWidth) * verticalFalloff;

                float3 displacedOS = input.positionOS + float3(0, h, 0);
                Varyings o = WaterFinishVertex(input, displacedOS);
                o.surfaceInfo = input.surfaceInfo.z > 0.5 ? input.surfaceInfo.xy : float2(_WaterGlobalSurfaceY, _WaterlineMask);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float t = _TimeParameters.x;
                float surfaceY = input.surfaceInfo.x;
                half waterlineMask = input.surfaceInfo.y;
                half depthBelow = min(max(surfaceY - input.positionWS.y, 0.0), PocketLightDepth(input.positionWS.xy));

                bool masked = _WaterDistortionMaskOn > 0.5;
                float2 offset = BackgroundDistortion(input.positionWS, depthBelow, t);
                if (masked)
                {
                    half keep = max(DistortionMask(screenUV), DistortionMask(screenUV + offset));
                    offset *= 1.0 - saturate(keep * 1.5);
                }
                half3 background = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture, screenUV + offset).rgb;

                half depthT = WaterWorldDepthT(input.positionWS.y, surfaceY, _WaterDepthRange);
                half3 waterColor = WaterDepthGradient(_ShallowColor.rgb, _DeepColor.rgb, depthT, _DepthGradientPower);

                half visibility = _Clarity * (1.0 - depthT * _ClarityDepthLoss);
                half3 albedo = lerp(waterColor, background * _TransmissionTint.rgb, visibility);

                albedo += _SurfaceGlowColor.rgb * exp(-depthBelow / max(_SurfaceGlowDepth, 1e-3));

                if (_MistAmount > 0.0)
                {
                    float2 mistUV = input.positionWS.xy * _MistScale;
                    float2 mistDrift = float2(1.0, 0.3) * (t * _MistSpeed * _MistScale);
                    float cloud = WaterNoiseD(mistUV + mistDrift).x
                        + 0.5 * WaterNoiseD(mistUV * 2.07 - mistDrift * 0.8 + 9.1).x;
                    half breakup = max(0.0, 1.0 + cloud * _MistBreakup * 1.2);
                    half lit = exp(-depthBelow / max(_MistLightDepth, 1e-3));
                    half3 mistColor = lerp(_MistDeepColor.rgb, _MistColor.rgb, lit);
                    half density = _MistAmount * lerp(1.0, lerp(0.35, 1.0, depthT), _MistDepthThickening) * breakup;
                    albedo = lerp(albedo, mistColor, saturate(density));
                }

                if (waterlineMask > 0.001)
                {
                    float wx = input.positionWS.x;
                    float vx = wx * _WaterlineVariationScale;
                    float vt = t * _WaterlineVariationSpeed;
                    float swell = WaterNoiseD(float2(vx + vt * 0.6, vt * 0.35)).x
                        + 0.5 * WaterNoiseD(float2(vx * 2.3 - vt * 0.9, 7.1 + vt * 0.5)).x;
                    float shimmer = WaterNoiseD(float2(vx * 1.7 - vt * 0.8, 3.7 + vt * 0.6)).x
                        + 0.5 * WaterNoiseD(float2(vx * 4.1 + vt * 1.3, 11.3)).x;
                    half swell01 = saturate(swell * 0.75 + 0.5);
                    half shimmer01 = saturate(shimmer * 0.75 + 0.5);

                    float2 lineXZ = float2(wx, _WaterFieldOrigin.y);
                    const float tap = 0.125;
                    float r0 = SampleRipple(lineXZ - float2(tap * 2.0, 0));
                    float r1 = SampleRipple(lineXZ - float2(tap, 0));
                    float r2 = SampleRipple(lineXZ);
                    float r3 = SampleRipple(lineXZ + float2(tap, 0));
                    float r4 = SampleRipple(lineXZ + float2(tap * 2.0, 0));
                    float rippleH = (r0 + 2.0 * r1 + 3.0 * r2 + 2.0 * r3 + r4) / 9.0 * _RippleScale;
                    float rippleSlope = ((r3 + r4) - (r0 + r1)) * 0.5 * _RippleScale / (tap * 3.0);
                    half crest = smoothstep(0.0, 1.0, (max(rippleH, 0.0) * 6.0 + abs(rippleSlope) * 0.5) * _WaterlineRippleResponse);
                    half trough = smoothstep(0.0, 1.0, max(-rippleH, 0.0) * 6.0 * _WaterlineRippleResponse);

                    half widthMul = lerp(1.0 - _WaterlineThicknessVariation, 1.0 + _WaterlineThicknessVariation * 1.5, swell01);
                    widthMul *= (1.0 + crest * 1.5) * (1.0 - trough * 0.6);
                    half width = max(_WaterlineFoamWidth * widthMul, 1e-4);
                    half brightness = lerp(1.0 - _WaterlineBrightnessVariation, 1.0 + _WaterlineBrightnessVariation * 0.6, shimmer01)
                        * (1.0 + crest * _WaterlineCrestBrightness);

                    half bandTop = _WaterlinePosition + _WaterlineFoamWidth * 0.5;
                    half dTop = bandTop - input.uv.y;
                    half aa = max(fwidth(input.uv.y), 1e-4);
                    half foam = smoothstep(-aa, aa, dTop) * smoothstep(width, width * 0.25, dTop) * waterlineMask;

                    half below = max(dTop, 0.0);
                    half glowReach = max(_WaterlineGlow * lerp(0.75, 1.25, swell01), 1e-3);
                    half glowBrightness = lerp(1.0, brightness, 0.5);
                    albedo += _FoamColor.rgb * _WaterlineGlowStrength * glowBrightness * exp(-below / glowReach) * waterlineMask;
                    albedo = lerp(albedo, _FoamColor.rgb * brightness, foam);
                }

                half glowFalloff = lerp(exp(-depthBelow / max(_GlowDepth, 1e-3)), 1.0, _GlowDeepAmount);
                half3 emission = WaterGlowEmission(albedo, _GlowColor, _GlowIntensity, _GlowFollowColor) * glowFalloff;
                return WaterFinishGlowFragment(albedo, 1.0, emission, _GlowSelfLit * step(1e-4, _GlowIntensity), input);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
