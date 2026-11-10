Shader "SpookyWater/Water Horizon"
{
    Properties
    {
        [Section(Surface)] _RippleScale("Ripple Height", Float) = 1
        _RippleHeightOffset("Height Offset", Float) = 0
        _EdgeFalloffWidth("Edge Falloff", Range(0.001, 0.25)) = 0.03
        _DisplacementFalloffPower("Displacement Falloff", Range(0.1, 6)) = 2

        [Section(Horizon)] _NearColor("Near Color", Color) = (0.043, 0.145, 0.235, 1)
        _HorizonColor("Horizon Color", Color) = (0.55, 0.68, 0.78, 1)
        _ColorFadePower("Color Fade Curve", Range(0.1, 6)) = 1.2
        _HazeDistance("Haze Distance", Float) = 45
        _UnderwaterFogDistance("Underwater Fog Distance", Float) = 15
        _UnderwaterHazeColor("Underwater Haze Color", Color) = (0.87, 0.99, 0.98, 0)
        _UnderwaterFadeDistance("Underwater Fade Distance", Float) = 12
        _UnderwaterFadeAmount("Underwater Fade Amount", Range(0, 1)) = 0.2
        _EdgeFadeStart("Edge Fade Start", Range(0, 1)) = 0.85

        [Section(Detail)] _MainTex("Detail Texture", 2D) = "white" {}

        [HideInInspector] _ReflectionOffset("Reflection UV Offset", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ReflectionBlurSize("Reflection Blur Size", Float) = 0.004
        [HideInInspector] _ReflectionDistortion("Reflection Distortion", Float) = 0.03
        [HideInInspector] _NormalStrength("Normal Strength", Float) = 2.5
        [HideInInspector] _ReflectionTint("Reflection Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _ReflectionSkyColor("Reflection Sky Color", Color) = (0.55, 0.75, 0.95, 1)
        [HideInInspector] _ReflectionMinStrength("Reflection Strength, Straight-Down View", Float) = 0.45
        [HideInInspector] _ReflectionMaxStrength("Reflection Strength, Grazing View", Float) = 0.9
        [HideInInspector] _ReflectionFresnelPower("Reflection Fresnel Power", Float) = 2.5
        [HideInInspector] _UnderwaterTint("See-Through Tint", Color) = (0.75, 0.95, 0.95, 1)
        [HideInInspector] _UnderwaterSkyColor("See-Through Sky Color", Color) = (0.55, 0.8, 0.9, 1)
        [HideInInspector] _UnderwaterColor("Outside-Window Color (also what the far edge fades to)", Color) = (0.03, 0.3, 0.32, 1)
        [HideInInspector] _UnderwaterWindowAngle("Snell's Window Half-Angle (degrees)", Float) = 90
        [HideInInspector] _UnderwaterWindowSoftness("Snell's Window Edge Softness", Float) = 0.08
        [HideInInspector] _UnderwaterBlurSize("See-Through Blur Size", Float) = 0.002
        [HideInInspector] _UnderwaterRippleRefraction("Ripple Refraction", Float) = 0.1
        [HideInInspector] _UnderwaterRippleHighlightGain("Highlight Gain", Float) = 10
        [HideInInspector] _UnderwaterRippleHighlightOpacity("Highlight Opacity", Float) = 1
        [HideInInspector] _RippleHighlightColor("Highlight Color", Color) = (0.9, 1, 1, 1)
        [HideInInspector] _DepthFogColor("Depth Fog Color", Color) = (0.3, 0.5, 0.2, 0)
        [HideInInspector] _DepthFogStartZ("Depth Fog Start Z", Float) = 0
        [HideInInspector] _DepthFogEndZ("Depth Fog End Z", Float) = 10
        [HideInInspector] _DepthFogPower("Depth Fog Curve", Float) = 1
        [HideInInspector] _DepthFogReflection("Fog On Reflections", Float) = 0.25
        [HideInInspector] _GlowIntensity("Glow Intensity", Float) = 0
        [HideInInspector] [HDR] _GlowColor("Glow Color", Color) = (0.3, 0.6, 1, 1)
        [HideInInspector] _GlowSelfLit("Self Illumination", Float) = 0.5
        [HideInInspector] _GlowFollowColor("Glow Follows Color", Float) = 0.5
        [HideInInspector] _GlowUnderside("Underside Glow", Float) = 1
        [HideInInspector] _ContactLineAmount("Contact Wavelets", Float) = 1
        [HideInInspector] _ContactLineReach("Reach", Float) = 1.5
        [HideInInspector] _ContactReachPixels("Minimum Reach (pixels)", Float) = 60
        [HideInInspector] _ContactLineSpread("Side Spread", Float) = 0.5
        [HideInInspector] _ContactLineScale("Rows Per Unit", Float) = 5
        [HideInInspector] _ContactWaveletLength("Stroke Length", Float) = 3.5
        [HideInInspector] _ContactWaveletThickness("Stroke Thickness", Float) = 0.34
        [HideInInspector] _ContactWaveletSoftness("Stroke Softness", Float) = 0.7
        [HideInInspector] _ContactWaveletBreakup("Stroke Breakup", Float) = 0.6
        [HideInInspector] _ContactLapAmount("Lapping Distance", Float) = 0.4
        [HideInInspector] _ContactLineSpeed("Lapping Speed", Float) = 0.35
        [HideInInspector] _ContactDriftSpeed("Drift Speed", Float) = 0.25
        [HideInInspector] _ContactPerspective("Perspective Scale", Float) = 0.5
        [HideInInspector] _ContactTwinkleSpeed("Twinkle Speed", Float) = 0.25
        [HideInInspector] _ContactSmearLength("Reflection Smear", Float) = 1.2
        [HideInInspector] _ContactLineSkew("Reflection Shift", Float) = 0.5
        [HideInInspector] _ContactRowShift("Row Slide", Float) = 0.6
        [HideInInspector] _ContactFarBoost("Far Boost", Float) = 2.5
        [HideInInspector] _ContactFarDistance("Far Boost Distance", Float) = 40
        [HideInInspector] _ContactBreak("Reflection Break", Float) = 1.5
        [HideInInspector] _ContactGlintColor("Glint Color", Color) = (0.92, 0.98, 1, 0.6)
        [HideInInspector] _ContactGlintChance("Glint Chance", Float) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite On

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float4 flatScreenPos : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _DepthFogColor;
                float _DepthFogStartZ;
                float _DepthFogEndZ;
                half _DepthFogPower;
                half _DepthFogReflection;
                half4 _UnderwaterTint;
                half4 _UnderwaterSkyColor;
                half4 _UnderwaterColor;
                half _UnderwaterWindowAngle;
                half _UnderwaterWindowSoftness;
                half4 _NearColor;
                half4 _HorizonColor;
                half _ColorFadePower;
                half _RippleScale;
                half _RippleHeightOffset;
                half _EdgeFalloffWidth;
                half _DisplacementFalloffPower;
                half _HazeDistance;
                half _EdgeFadeStart;
                half _UnderwaterFogDistance;
                half4 _UnderwaterHazeColor;
                half _UnderwaterFadeDistance;
                half _UnderwaterFadeAmount;
                half _UnderwaterBlurSize;
                half _UnderwaterRippleRefraction;
                half _UnderwaterRippleHighlightGain;
                half _UnderwaterRippleHighlightOpacity;
                half4 _RippleHighlightColor;
                half4 _ReflectionOffset;
                half _ReflectionBlurSize;
                half _ReflectionDistortion;
                half _NormalStrength;
                half4 _ReflectionTint;
                half4 _ReflectionSkyColor;
                half _ReflectionMinStrength;
                half _ReflectionMaxStrength;
                half _ReflectionFresnelPower;
                half _ContactLineAmount;
                half _ContactLineReach;
                half _ContactReachPixels;
                half _ContactLineSpread;
                float _ContactLineScale;
                half _ContactWaveletLength;
                half _ContactWaveletThickness;
                half _ContactWaveletSoftness;
                half _ContactWaveletBreakup;
                half _ContactLapAmount;
                half _ContactLineSpeed;
                half _ContactDriftSpeed;
                half _ContactPerspective;
                half _ContactTwinkleSpeed;
                half _ContactSmearLength;
                half _ContactLineSkew;
                half _ContactRowShift;
                half _ContactFarBoost;
                float _ContactFarDistance;
                half _ContactBreak;
                half4 _ContactGlintColor;
                half _ContactGlintChance;
                half _GlowIntensity;
                half4 _GlowColor;
                half _GlowSelfLit;
                half _GlowFollowColor;
                half _GlowUnderside;
            CBUFFER_END

            #include "../Includes/WaterCommon.hlsl"
            #include "../Includes/WaterLit2D.hlsl"
            #include "../Includes/WaterReflection.hlsl"

            Varyings Vert(Attributes input)
            {
                float3 restWS = TransformObjectToWorld(input.positionOS);
                float2 fieldUV = WaterFieldUV(restWS);
                float settle = pow(saturate(1.0 - input.uv.y), _DisplacementFalloffPower);
                float h = SampleWaterHeight(restWS.xz, _RippleScale, _RippleHeightOffset);
                h *= WaterEdgeFalloff1D(fieldUV.x, _EdgeFalloffWidth) * settle;

                float3 displacedOS = input.positionOS + float3(0, h, 0);
                Varyings o = WaterFinishVertex(input, displacedOS);
                o.flatScreenPos = ComputeScreenPos(TransformWorldToHClip(restWS));
                return o;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool fromBelow = _WaterViewedFromBelow > 0.5;
                if (IS_FRONT_VFACE(frontFace, true, false) == fromBelow) discard;

                float3 cameraWS = GetCameraPositionWS();
                float viewDistance = distance(cameraWS, input.positionWS);
                float depthToPixel = max(input.positionWS.z - cameraWS.z, 1e-3);
                float farPath = viewDistance * saturate(1.0 - (_WaterHorizonSeam.x - cameraWS.z) / depthToPixel);
                half3 viewDirWS = normalize(cameraWS - input.positionWS);
                half viewDot = saturate(viewDirWS.y);

                if (fromBelow)
                {
                    float2 windGradient = WaterWindWaves(input.positionWS.xz).yz * _RippleScale;
                    float2 windRefraction = windGradient * _UnderwaterRippleRefraction;
                    windRefraction.y = max(windRefraction.y, 0.0);
                    float2 throughScreenUV = input.screenPos.xy / input.screenPos.w + windRefraction;
                    half4 seen = SampleReflectionBlurred(throughScreenUV, _UnderwaterBlurSize);
                    half3 through = (seen.rgb + _UnderwaterSkyColor.rgb * (1.0 - saturate(seen.a))) * _UnderwaterTint.rgb;
                    half cosWindow = cos(radians(_UnderwaterWindowAngle));
                    half window = _UnderwaterWindowAngle >= 89.9 ? 1.0
                        : smoothstep(cosWindow - _UnderwaterWindowSoftness, cosWindow + _UnderwaterWindowSoftness, viewDot);
                    half3 underside = lerp(_UnderwaterColor.rgb, through, window);
                    half facing = -dot(windGradient, normalize(cameraWS.xz - input.positionWS.xz + 1e-5)) * _UnderwaterRippleHighlightGain;
                    half lit = max(facing, 0.0);
                    half shade = max(-facing, 0.0);
                    underside = lerp(underside, _RippleHighlightColor.rgb, lit / (lit + 1.0) * _UnderwaterRippleHighlightOpacity);
                    underside *= 1.0 - shade / (shade + 1.0) * _UnderwaterRippleHighlightOpacity * 0.5;
                    half3 murkColor = lerp(_UnderwaterColor.rgb, _UnderwaterHazeColor.rgb, _UnderwaterHazeColor.a);
                    half murk = 1.0 - exp(-farPath / max(_UnderwaterFogDistance, 1e-3));
                    underside = lerp(underside, murkColor, murk);
                    underside = lerp(underside, murkColor, smoothstep(_EdgeFadeStart, 1.0, input.uv.y));

                    half alpha = 1.0 - _UnderwaterFadeAmount * smoothstep(0.0, max(_UnderwaterFadeDistance, 1e-3), farPath);
                    clip(alpha - 0.004);
                    half3 undersideGlow = WaterGlowEmission(underside, _GlowColor, _GlowIntensity * _GlowUnderside, _GlowFollowColor) * (1.0 - murk);
                    return WaterFinishGlowFragment(underside, alpha, undersideGlow, _GlowSelfLit * step(1e-4, _GlowIntensity), input);
                }

                half t = pow(saturate(input.uv.y), max(_ColorFadePower, 0.001));
                half3 albedo = lerp(_NearColor.rgb, _HorizonColor.rgb, t);
                albedo *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb;

                float2 screenUV = input.flatScreenPos.xy / input.flatScreenPos.w;
                float2 dPdx = ddx(input.positionWS.xz);
                float2 dPdy = ddy(input.positionWS.xz);
                float4 wavelet = 0;
                float strokeLength = 0;
                float rowSpacing = 1;
                float rowShift = 0;
                float farBoost = lerp(1.0, _ContactFarBoost,
                    saturate((input.positionWS.z - GetCameraPositionWS().z) / max(_ContactFarDistance, 1e-3)));
                [branch] if (_ContactLineAmount > 0.0)
                {
                    half contact = WaterContactMask(input.positionWS.xz, cameraWS.xz, _ContactLineReach,
                        _ContactLineSpread, 0.0, _ContactReachPixels * farBoost * max(abs(dPdx.y), abs(dPdy.y))) * _ContactLineAmount;
                    [branch] if (contact > 0.0)
                        wavelet = WaterContactWavelets(input.positionWS.xz, contact, max(_ContactLineScale, 1e-3),
                            _ContactWaveletLength, _ContactWaveletThickness, _ContactWaveletSoftness, _ContactWaveletBreakup, _ContactLapAmount,
                            _TimeParameters.x * _ContactLineSpeed * 6.2832, _TimeParameters.x * _ContactDriftSpeed, _ContactPerspective,
                            _TimeParameters.x * _ContactTwinkleSpeed * 6.2832,
                            _ContactGlintChance, farBoost, dPdx, dPdy, strokeLength, rowSpacing, rowShift);
                }
                float2 strokeUV = WaterStrokeScreenSize(strokeLength, rowSpacing, dPdx, dPdy);
                float rowSlideUV = WaterStrokeScreenSize(rowShift, 0.0, dPdx, dPdy).x * _ContactRowShift * farBoost;
                float2 windDistortion = WaterWindReflectionDistortion(input.positionWS.xz, _RippleScale, _NormalStrength, _ReflectionDistortion);
                float2 reflUV = screenUV + _ReflectionOffset.xy + wavelet.x * wavelet.yz * strokeUV * float2(_ContactLineSkew, _ContactBreak) * farBoost
                    + float2(rowSlideUV, 0.0) + windDistortion;
                half4 reflected = SampleReflectionSmeared(reflUV, float2(wavelet.x * strokeUV.x * farBoost + abs(rowSlideUV) * 0.5,
                    wavelet.x * abs(strokeUV.y) * farBoost * _ContactWaveletSoftness) * _ContactSmearLength * 0.5, _ReflectionBlurSize);
                half3 reflection = (reflected.rgb + _ReflectionSkyColor.rgb * (1.0 - saturate(reflected.a))) * _ReflectionTint.rgb;
                reflection = lerp(reflection, _ContactGlintColor.rgb, wavelet.x * wavelet.w * _ContactGlintColor.a);
                half reflectionStrength = lerp(_ReflectionMinStrength, _ReflectionMaxStrength, pow(1.0 - viewDot, _ReflectionFresnelPower));
                albedo = lerp(albedo, reflection, reflectionStrength);
                half fogScale = WaterReflectionFogScale(reflectionStrength, reflected.a, _DepthFogReflection);

                albedo = ApplyWaterDepthFog(albedo, input.positionWS.z, _DepthFogColor, _DepthFogStartZ, _DepthFogEndZ, _DepthFogPower, fogScale);
                half haze = (1.0 - exp(-farPath / max(_HazeDistance, 1e-3))) * fogScale;
                albedo = lerp(albedo, _HorizonColor.rgb, haze);
                half edgeFade = smoothstep(_EdgeFadeStart, 1.0, input.uv.y);
                albedo = lerp(albedo, _HorizonColor.rgb, edgeFade);

                half3 emission = WaterGlowEmission(albedo, _GlowColor, _GlowIntensity, _GlowFollowColor) * (1.0 - haze) * (1.0 - edgeFade);
                return WaterFinishGlowFragment(albedo, 1.0, emission, _GlowSelfLit * step(1e-4, _GlowIntensity), input);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
