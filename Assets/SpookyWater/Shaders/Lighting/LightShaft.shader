Shader "SpookyWater/Light Shaft"
{
    Properties
    {
        [Section(Blending)] [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4

        [Section(Bands)] [NoScaleOffset] _BandTex("Band Texture A", 2D) = "black" {}
        [NoScaleOffset] _BandTex2("Band Texture B", 2D) = "black" {}

        [HideInInspector] [HDR] _Color("Color", Color) = (0.35, 0.75, 1, 1)
        [HideInInspector] _Intensity("Intensity", Float) = 1
        [HideInInspector] _Opacity("Opacity", Float) = 1
        [HideInInspector] _Fill("Fill", Float) = 0.1
        [HideInInspector] _BandShape1("Band Shape A", Vector) = (1, 0.03, 0.03, 0.2)
        [HideInInspector] _BandLight1("Band Light A", Vector) = (1, 0.5, 0, 0)
        [HideInInspector] _BandShape2("Band Shape B", Vector) = (1.2, -0.03, 0.03, 0.2)
        [HideInInspector] _BandLight2("Band Light B", Vector) = (1, 0.5, 0, 0)
        [HideInInspector] _SideFade("Side Fade", Float) = 0.15
        [HideInInspector] _Seed("Seed", Float) = 0
        [HideInInspector] _WaterClipY("Waterline Y", Float) = 0
        [HideInInspector] _WaterClipSoftness("Waterline Edge Softness", Float) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest [_ZTest]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../Includes/WaterCommon.hlsl"
            #include "../Includes/WaterSide.hlsl"

            TEXTURE2D(_BandTex);
            SAMPLER(sampler_BandTex);
            float4 _BandTex_TexelSize;
            TEXTURE2D(_BandTex2);
            SAMPLER(sampler_BandTex2);
            float4 _BandTex2_TexelSize;
            float4 _ShaftShadowAxes;
            float4 _ShaftOccluders[16];
            float4 _ShaftOccluderOutlines[16];
            float4 _ShaftOccluderShadows[16];
            float _ShaftOccluderCount;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _Opacity;
                half _Fill;
                float4 _BandShape1;
                float4 _BandLight1;
                float4 _BandShape2;
                float4 _BandLight2;
                float _SideFade;
                float _Seed;
                float _WaterClipY;
                float _WaterClipSoftness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            half SampleBlurred(TEXTURE2D_PARAM(tex, samp), float texWidth, float2 uv, float sigma)
            {
                float spacing = sigma * 0.6667;
                float lod = log2(max(spacing * texWidth, 1.0));
                float2 offset = float2(spacing, 0.0);
                half v = SAMPLE_TEXTURE2D_LOD(tex, samp, uv, lod).r * 0.2707;
                v += (SAMPLE_TEXTURE2D_LOD(tex, samp, uv + offset, lod).r
                    + SAMPLE_TEXTURE2D_LOD(tex, samp, uv - offset, lod).r) * 0.2167;
                v += (SAMPLE_TEXTURE2D_LOD(tex, samp, uv + offset * 2.0, lod).r
                    + SAMPLE_TEXTURE2D_LOD(tex, samp, uv - offset * 2.0, lod).r) * 0.1113;
                v += (SAMPLE_TEXTURE2D_LOD(tex, samp, uv + offset * 3.0, lod).r
                    + SAMPLE_TEXTURE2D_LOD(tex, samp, uv - offset * 3.0, lod).r) * 0.0366;
                return v;
            }

            half OccluderShadow(float3 positionWS)
            {
                float3 ray = IsPerspectiveProjection() ? positionWS - _WorldSpaceCameraPos : GetViewForwardDir();
                float rayZ = abs(ray.z) > 1e-4 ? ray.z : 1e-4;
                half lit = 1.0;
                int count = min((int)_ShaftOccluderCount, 16);
                [loop] for (int i = 0; i < count; i++)
                {
                    float4 interval = _ShaftOccluders[i];
                    float4 outline = _ShaftOccluderOutlines[i];
                    float4 shadow = _ShaftOccluderShadows[i];
                    float2 onPlane = positionWS.xy + ray.xy * ((interval.z - positionWS.z) / rayZ);
                    float down = dot(onPlane, _ShaftShadowAxes.xy);
                    float across = dot(onPlane, _ShaftShadowAxes.zw);
                    float offset = across - outline.x;
                    float entry = shadow.w > 0.0
                        ? outline.y - sqrt(max(shadow.w * shadow.w - offset * offset, 0.0))
                        : outline.y + max(outline.z * offset, outline.w * offset);
                    float below = down - entry;
                    float soft = max(shadow.y + shadow.z * max(below, 0.0), 1e-3) * 0.5;
                    half inside = smoothstep(interval.x - soft, interval.x + soft, across)
                        * (1.0 - smoothstep(interval.y - soft, interval.y + soft, across));
                    half reach = smoothstep(0.0, soft * 2.0, below);
                    if (interval.w > 0.0) reach *= saturate(1.0 - below * interval.w);
                    lit = min(lit, 1.0 - shadow.x * inside * reach);
                }
                return lit;
            }

            void SampleLayer(TEXTURE2D_PARAM(tex, samp), float texWidth, float4 shape, float4 light,
                float across, float along, float phase, out half bands, out half glow)
            {
                float scroll = frac(_Time.y * shape.y + phase);
                float2 uv = float2(across * shape.x + scroll, 1.0 - along);
                bands = SampleBlurred(TEXTURE2D_ARGS(tex, samp), texWidth, uv, shape.z) * light.x;
                glow = SampleBlurred(TEXTURE2D_ARGS(tex, samp), texWidth, uv, shape.w) * light.y;
            }

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float across = input.uv.x;
                float along = input.uv.y;

                half bandsA, glowA, bandsB, glowB;
                SampleLayer(TEXTURE2D_ARGS(_BandTex, sampler_BandTex), _BandTex_TexelSize.z, _BandShape1, _BandLight1,
                    across, along, _Seed * 0.173, bandsA, glowA);
                SampleLayer(TEXTURE2D_ARGS(_BandTex2, sampler_BandTex2), _BandTex2_TexelSize.z, _BandShape2, _BandLight2,
                    across, along, _Seed * 0.611 + 0.37, bandsB, glowB);
                half bands = max(bandsA, bandsB);
                half glow = max(glowA, glowB);

                float side = min(across, 1.0 - across) / max(_SideFade, 1e-4);
                half sideMask = _SideFade > 0.0 ? smoothstep(0.0, 1.0, saturate(side)) : 1.0;

                half light = (lerp(_Fill, 1.0, saturate(bands)) + glow) * sideMask * input.uv.z
                    * OccluderShadow(input.positionWS);
                float edgeY = _WaterClipY + WaterSurfaceHeightAt(float3(input.positionWS.x, _WaterClipY, _WaterFieldOrigin.y));
                light *= WaterSideMask(input.positionWS.y, 2.0, edgeY, _WaterClipSoftness);
                return half4(_Color.rgb * (_Intensity * _Opacity * light), 0.0);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
