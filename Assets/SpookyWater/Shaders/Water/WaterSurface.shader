Shader "SpookyWater/Water Surface"
{
    Properties
    {
        [HideInInspector] _RippleScale("Ripple Scale", Float) = 1
        [Section(Surface)] _DistanceFalloffPower("Distance Falloff", Range(0.1, 4)) = 1
        _EdgeFalloffWidth("Edge Falloff", Range(0.001, 0.25)) = 0.03
        _NormalStrength("Normal Strength", Range(0, 8)) = 2.5

        [Section(Color)] _ShallowColor("Near Color", Color) = (0.55, 0.85, 0.85, 1)
        _DeepColor("Far Color", Color) = (0.043, 0.145, 0.235, 1)
        _DepthGradientPower("Depth Gradient Curve", Range(0.1, 6)) = 1.6

        [Section(Glow)] _GlowIntensity("Glow Intensity", Range(0, 8)) = 0
        [ShowIf(_GlowIntensity)] [HDR] _GlowColor("Glow Color", Color) = (0.3, 0.6, 1, 1)
        [ShowIf(_GlowIntensity)] _GlowSelfLit("Self Illumination", Range(0, 1)) = 0.5
        [ShowIf(_GlowIntensity)] _GlowFollowColor("Glow Follows Color", Range(0, 1)) = 0.5
        [ShowIf(_GlowIntensity)] _GlowRippleBoost("Ripple Glow Boost", Range(0, 4)) = 1
        [ShowIf(_GlowIntensity)] _GlowUnderside("Underside Glow", Range(0, 2)) = 1

        [Section(DepthFog)] _DepthFogColor("Fog Color", Color) = (0.3, 0.5, 0.2, 0)
        [ShowIf(_DepthFogColor)] _DepthFogStartZ("Fog Start", Float) = 0
        [ShowIf(_DepthFogColor)] _DepthFogEndZ("Fog End", Float) = 10
        [ShowIf(_DepthFogColor)] _DepthFogPower("Fog Curve", Range(0.1, 4)) = 1
        [ShowIf(_DepthFogColor)] _DepthFogReflection("Fog On Reflections", Range(0, 1)) = 0.25

        [Section(Reflection)] [Vector2] _ReflectionOffset("Reflection Offset", Vector) = (0, 0, 0, 0)
        _ReflectionDistortion("Distortion", Range(0, 0.2)) = 0.03
        _ReflectionBlurSize("Blur", Range(0, 0.02)) = 0.004
        _ReflectionTint("Tint", Color) = (1, 1, 1, 1)
        _ReflectionSkyColor("Sky Color", Color) = (0.55, 0.75, 0.95, 1)
        _ReflectionMinStrength("Steep View Strength", Range(0, 1)) = 0.45
        _ReflectionMaxStrength("Grazing View Strength", Range(0, 1)) = 0.9
        _ReflectionFresnelPower("Fresnel Power", Range(0.5, 8)) = 2.5

        [Section(ContactWavelets)] _ContactLineAmount("Wavelet Amount", Range(0, 1)) = 1
        _ContactLineReach("Reach", Float) = 1.5
        _ContactReachPixels("Minimum Reach", Float) = 60
        _ContactLineSpread("Side Spread", Float) = 0.5
        _ContactShoreAmount("Shore Amount", Range(0, 1)) = 0.6
        _ContactLineScale("Rows Per Unit", Float) = 5
        _ContactWaveletLength("Stroke Length", Range(2, 30)) = 3.5
        _ContactWaveletThickness("Stroke Thickness", Range(0.03, 0.45)) = 0.34
        _ContactWaveletSoftness("Stroke Softness", Range(0, 1)) = 0.7
        _ContactWaveletBreakup("Stroke Breakup", Range(0, 1)) = 0.6
        _ContactLapAmount("Lapping Distance", Range(0, 1.5)) = 0.4
        _ContactLineSpeed("Lapping Speed", Float) = 0.35
        _ContactDriftSpeed("Drift Speed", Float) = 0.25
        _ContactPerspective("Perspective Scale", Range(0, 1)) = 0.5
        _ContactTwinkleSpeed("Twinkle Speed", Float) = 0.25
        _ContactSmearLength("Reflection Smear", Range(0, 1.5)) = 1.2
        _ContactLineSkew("Reflection Shift", Range(0, 1)) = 0.5
        _ContactRowShift("Row Slide", Range(0, 2)) = 0.6
        _ContactFarBoost("Far Boost", Range(1, 5)) = 2.5
        _ContactFarDistance("Far Boost Distance", Float) = 40
        _ContactBreak("Reflection Break", Range(0, 3)) = 1.5
        _ContactGlintColor("Glint Color", Color) = (0.92, 0.98, 1, 0.6)
        _ContactGlintChance("Glint Chance", Range(0, 1)) = 0.4

        [Section(RippleHighlight)] _RippleHighlightColor("Highlight Color", Color) = (0.9, 1, 1, 1)
        _RippleHighlightGain("Highlight Gain", Range(0, 20)) = 4
        _RippleHighlightOpacity("Highlight Opacity", Range(0, 1)) = 0.6

        [Section(Horizon)] _HorizonColor("Horizon Color", Color) = (0.55, 0.68, 0.78, 1)
        _ColorFadePower("Color Fade Curve", Range(0.1, 6)) = 1.2
        _HazeDistance("Haze Distance", Float) = 45
        _EdgeFadeStart("Edge Fade Start", Range(0, 1)) = 0.85
        _DisplacementFalloffPower("Wave Falloff", Range(0.1, 6)) = 2
        _UnderwaterFogDistance("Underwater Fog Distance", Float) = 15
        _UnderwaterHazeColor("Underwater Haze Color", Color) = (0.87, 0.99, 0.98, 0)
        _UnderwaterFadeDistance("Underwater Fade Distance", Float) = 12
        _UnderwaterFadeAmount("Underwater Fade Amount", Range(0, 1)) = 0.2

        [Section(Underside)] _UnderwaterTint("See-Through Tint", Color) = (0.75, 0.95, 0.95, 1)
        _UnderwaterSkyColor("See-Through Sky Color", Color) = (0.55, 0.8, 0.9, 1)
        _UnderwaterColor("Outside Window Color", Color) = (0.03, 0.3, 0.32, 1)
        _UnderwaterRefraction("Refraction", Range(0, 0.2)) = 0.02
        _UnderwaterRippleRefraction("Ripple Refraction", Range(0, 0.5)) = 0.1
        _UnderwaterRippleHighlightGain("Highlight Gain", Range(0, 20)) = 10
        _UnderwaterRippleHighlightOpacity("Highlight Opacity", Range(0, 1)) = 1
        _UnderwaterBlurSize("See-Through Blur", Range(0, 0.02)) = 0.002
        _UnderwaterWindowAngle("Window Angle", Range(0, 90)) = 90
        _UnderwaterWindowSoftness("Window Softness", Range(0.001, 0.5)) = 0.08

    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite On

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
                float2 fieldUV    : TEXCOORD4;
                float4 flatScreenPos : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };


            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half _DepthGradientPower;
                half4 _DepthFogColor;
                float _DepthFogStartZ;
                float _DepthFogEndZ;
                half _DepthFogPower;
                half _DepthFogReflection;
                half _RippleScale;
                half _NormalStrength;
                half _DistanceFalloffPower;
                half _EdgeFalloffWidth;
                half4 _ReflectionOffset;
                half _ReflectionDistortion;
                half _ReflectionBlurSize;
                half4 _ReflectionTint;
                half4 _ReflectionSkyColor;
                half _ReflectionMinStrength;
                half _ReflectionMaxStrength;
                half _ReflectionFresnelPower;
                half4 _RippleHighlightColor;
                half _RippleHighlightGain;
                half _RippleHighlightOpacity;
                half _ContactLineAmount;
                float _ContactLineReach;
                half _ContactReachPixels;
                float _ContactLineSpread;
                half _ContactShoreAmount;
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
                half4 _UnderwaterTint;
                half4 _UnderwaterSkyColor;
                half4 _UnderwaterColor;
                half _UnderwaterRefraction;
                half _UnderwaterRippleRefraction;
                half _UnderwaterRippleHighlightGain;
                half _UnderwaterRippleHighlightOpacity;
                half _UnderwaterBlurSize;
                half _UnderwaterWindowAngle;
                half _UnderwaterWindowSoftness;
                half _GlowIntensity;
                half4 _GlowColor;
                half _GlowSelfLit;
                half _GlowFollowColor;
                half _GlowRippleBoost;
                half _GlowUnderside;
            CBUFFER_END

            #include "../Includes/WaterCommon.hlsl"
            #include "../Includes/WaterLit2D.hlsl"
            #include "../Includes/WaterReflection.hlsl"

            half4 UndersideFrag(Varyings input, float3 normalOS, float3 normalWS)
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 rippleGradient = RippleGradient(input.positionWS.xz, 0.02) * _RippleScale;
                float2 refraction = normalOS.xz * _UnderwaterRefraction + rippleGradient * _UnderwaterRippleRefraction;

                refraction.y = max(refraction.y, 0.0);
                float2 throughUV = screenUV + refraction;
                half4 seen = SampleReflectionDiscBlur(throughUV, _UnderwaterBlurSize);
                half3 through = (seen.rgb + _UnderwaterSkyColor.rgb * (1.0 - saturate(seen.a))) * _UnderwaterTint.rgb;
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                half cosView = saturate(dot(-normalWS, viewDirWS));
                half cosWindow = cos(radians(_UnderwaterWindowAngle));
                half window = _UnderwaterWindowAngle >= 89.9 ? 1.0
                    : smoothstep(cosWindow - _UnderwaterWindowSoftness, cosWindow + _UnderwaterWindowSoftness, cosView);
                half3 albedo = lerp(_UnderwaterColor.rgb, through, window);

                float2 toCameraXZ = normalize(GetCameraPositionWS().xz - input.positionWS.xz + 1e-5);
                half facing = -dot(rippleGradient, toCameraXZ) * _UnderwaterRippleHighlightGain;
                half lit = max(facing, 0.0);
                half shade = max(-facing, 0.0);
                half highlightMask = _UnderwaterRippleHighlightOpacity;
                albedo = lerp(albedo, _RippleHighlightColor.rgb, lit / (lit + 1.0) * highlightMask);
                albedo *= 1.0 - shade / (shade + 1.0) * highlightMask * 0.5;

                half3 emission = WaterGlowEmission(albedo, _GlowColor, _GlowIntensity * _GlowUnderside, _GlowFollowColor)
                    * (1.0 + _GlowRippleBoost * lit / (lit + 1.0));
                return WaterFinishGlowFragment(albedo, 1.0, emission, _GlowSelfLit * step(1e-4, _GlowIntensity), input);
            }

            Varyings Vert(Attributes input)
            {
                float3 restWS = TransformObjectToWorld(input.positionOS);
                float2 fieldUV = WaterFieldUV(restWS);
                float settle = pow(saturate(1.0 - fieldUV.y), _DistanceFalloffPower);
                float h = SampleWaterHeight(restWS.xz, _RippleScale);
                h *= WaterEdgeFalloff1D(fieldUV.x, _EdgeFalloffWidth) * settle;
                h = BlendIntoHorizonSeam(h, restWS);

                float3 displacedOS = input.positionOS + float3(0, h, 0);
                Varyings o = WaterFinishVertex(input, displacedOS);
                o.fieldUV = fieldUV;
                o.flatScreenPos = ComputeScreenPos(TransformWorldToHClip(restWS));
                return o;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool fromBelow = _WaterViewedFromBelow > 0.5;
                bool isFrontFace = IS_FRONT_VFACE(frontFace, true, false);
                if (isFrontFace == fromBelow) discard;

                float2 screenUV = input.flatScreenPos.xy / input.flatScreenPos.w;
                float2 dPdx = ddx(input.positionWS.xz);
                float2 dPdy = ddy(input.positionWS.xz);
                float4 wavelet = 0;
                float strokeLength = 0;
                float rowSpacing = 1;
                float rowShift = 0;
                float farBoost = lerp(1.0, _ContactFarBoost,
                    saturate((input.positionWS.z - GetCameraPositionWS().z) / max(_ContactFarDistance, 1e-3)));
                [branch] if (_ContactLineAmount > 0.0 && !fromBelow)
                {
                    half contact = WaterContactMask(input.positionWS.xz, GetCameraPositionWS().xz,
                        _ContactLineReach, _ContactLineSpread, _ContactShoreAmount,
                        _ContactReachPixels * farBoost * max(abs(dPdx.y), abs(dPdy.y))) * _ContactLineAmount;
                    [branch] if (contact > 0.0)
                        wavelet = WaterContactWavelets(input.positionWS.xz, contact, max(_ContactLineScale, 1e-3),
                            _ContactWaveletLength, _ContactWaveletThickness, _ContactWaveletSoftness, _ContactWaveletBreakup, _ContactLapAmount,
                            _TimeParameters.x * _ContactLineSpeed * 6.2832, _TimeParameters.x * _ContactDriftSpeed, _ContactPerspective,
                            _TimeParameters.x * _ContactTwinkleSpeed * 6.2832,
                            _ContactGlintChance, farBoost, dPdx, dPdy, strokeLength, rowSpacing, rowShift);
                }

                float3 normalOS = ReconstructWaterNormalOS(input.positionWS.xz, _RippleScale, 0.03, _NormalStrength);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));

                if (fromBelow)
                {
                    return UndersideFrag(input, normalOS, normalWS);
                }

                half3 albedo = WaterDepthGradient(_ShallowColor.rgb, _DeepColor.rgb, input.fieldUV.y, _DepthGradientPower);
                half3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                half viewDot = saturate(dot(normalWS, viewDirWS));

                float2 strokeUV = WaterStrokeScreenSize(strokeLength, rowSpacing, dPdx, dPdy);
                float2 distortion = normalOS.xz * _ReflectionDistortion;
                distortion.y = min(distortion.y, 0.0);
                distortion += WaterWindReflectionDistortion(input.positionWS.xz, _RippleScale, _NormalStrength, _ReflectionDistortion);
                distortion += wavelet.x * wavelet.yz * strokeUV * float2(_ContactLineSkew, _ContactBreak) * farBoost;
                float rowSlideUV = WaterStrokeScreenSize(rowShift, 0.0, dPdx, dPdy).x * _ContactRowShift * farBoost;
                distortion.x += rowSlideUV;
                float2 reflUV = screenUV + _ReflectionOffset.xy + distortion;
                half4 reflected = SampleReflectionSmeared(reflUV, float2(wavelet.x * strokeUV.x * farBoost + abs(rowSlideUV) * 0.5,
                    wavelet.x * abs(strokeUV.y) * farBoost * _ContactWaveletSoftness) * _ContactSmearLength * 0.5, _ReflectionBlurSize);
                half3 reflection = (reflected.rgb + _ReflectionSkyColor.rgb * (1.0 - saturate(reflected.a))) * _ReflectionTint.rgb;

                reflection = lerp(reflection, _ContactGlintColor.rgb, wavelet.x * wavelet.w * _ContactGlintColor.a);
                half reflectionFresnel = pow(1.0 - viewDot, _ReflectionFresnelPower);
                half reflectionStrength = lerp(_ReflectionMinStrength, _ReflectionMaxStrength, reflectionFresnel);
                half reflectionMix = reflectionStrength;
                albedo = lerp(albedo, reflection, reflectionMix);
                half fogScale = WaterReflectionFogScale(reflectionMix, reflected.a, _DepthFogReflection);

                float2 toCameraXZ = normalize(GetCameraPositionWS().xz - input.positionWS.xz + 1e-5);
                half facing = -dot(RippleGradient(input.positionWS.xz, 0.02) * _RippleScale, toCameraXZ) * _RippleHighlightGain;
                half lit = max(facing, 0.0);
                half shade = max(-facing, 0.0);
                half highlightMask = _RippleHighlightOpacity;
                albedo = lerp(albedo, _RippleHighlightColor.rgb, lit / (lit + 1.0) * highlightMask);
                albedo *= 1.0 - shade / (shade + 1.0) * highlightMask * 0.5;

                albedo = ApplyWaterDepthFog(albedo, input.positionWS.z, _DepthFogColor, _DepthFogStartZ, _DepthFogEndZ, _DepthFogPower, fogScale);

                half3 emission = WaterGlowEmission(albedo, _GlowColor, _GlowIntensity, _GlowFollowColor)
                    * (1.0 + _GlowRippleBoost * lit / (lit + 1.0));
                return WaterFinishGlowFragment(albedo, 1.0, emission, _GlowSelfLit * step(1e-4, _GlowIntensity), input);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
