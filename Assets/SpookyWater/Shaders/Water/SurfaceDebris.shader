Shader "SpookyWater/Surface Debris"
{
    Properties
    {
        [Section(Debris)] _MainTex("Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _AlphaClipThreshold("Alpha Clip", Range(0, 1)) = 0.5
        _TiltShading("Tilt Shading", Range(0, 4)) = 1
        _WetEdgeWidth("Wet Edge Width", Range(0, 0.25)) = 0.04
        _WetEdgeDarken("Wet Edge Darken", Range(0, 1)) = 0.35
        _FogAmount("Fog Amount", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Cull Off
        ZWrite On

        Offset -1, -1

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
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half2  lightingUV : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half4  color      : COLOR;
                half   tilt       : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _AlphaClipThreshold;
                half _TiltShading;
                half _WetEdgeWidth;
                half _WetEdgeDarken;
                half _FogAmount;
            CBUFFER_END

            #include "../Includes/WaterCommon.hlsl"
            #include "../Includes/WaterLit2D.hlsl"

            Varyings Vert(Attributes input)
            {
                float3 restWS = TransformObjectToWorld(input.positionOS);
                float h = WaterSurfaceHeightAt(restWS);
                float3 displacedOS = TransformWorldToObject(restWS + float3(0, h, 0));

                Varyings o = WaterFinishVertex(input, displacedOS);
                o.color = input.color * _Color;

                float2 slope = RippleGradient(restWS.xz, 0.03) * _WaterSurfaceDisplacement.x;
                float2 toCameraXZ = normalize(_WorldSpaceCameraPos.xz - restWS.xz + 1e-4);
                o.tilt = -dot(slope, toCameraXZ) * _TiltShading;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(tex.a - _AlphaClipThreshold);

                half3 albedo = tex.rgb;

                [branch] if (_WetEdgeWidth > 0.0 && _WetEdgeDarken > 0.0)
                {
                    float2 r = float2(_WetEdgeWidth, _WetEdgeWidth);
                    half edge = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(r.x, 0)).a;
                    edge = min(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(r.x, 0)).a);
                    edge = min(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, r.y)).a);
                    edge = min(edge, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, r.y)).a);
                    albedo *= 1.0 - _WetEdgeDarken * (1.0 - smoothstep(0.0, 1.0, edge));
                }

                half tilt = input.tilt;
                albedo *= 1.0 + max(tilt, 0.0) / (max(tilt, 0.0) + 1.0) * 0.5 - max(-tilt, 0.0) / (max(-tilt, 0.0) + 1.0) * 0.35;

                albedo = ApplyWaterDepthFog(albedo, input.positionWS.z, _WaterSurfaceFogColor,
                    _WaterSurfaceFogParams.x, _WaterSurfaceFogParams.y, _WaterSurfaceFogParams.z, _FogAmount);

                return WaterFinishFragment(albedo, 1.0, half4(1, 1, 1, 1), half3(0, 0, 1), input);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
