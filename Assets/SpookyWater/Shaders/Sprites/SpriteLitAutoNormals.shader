Shader "SpookyWater/Sprite Lit Auto Normals"
{
    Properties
    {
        [Section(AutoNormals)] _EdgeBevel("Edge Bevel", Range(0, 4)) = 1.2
        _EdgeWidth("Edge Width", Range(0.5, 32)) = 6
        _DetailBumps("Detail Bumps", Range(0, 16)) = 3
        _DetailWidth("Detail Width", Range(0.5, 8)) = 1.5
        _NormalMapStrength("Normal Map Strength", Range(0, 2)) = 1

        [Section(Detail)] _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0, 1)) = 0
        [ToggleUI] _ZWrite("Depth Write", Float) = 0

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half _AlphaClipThreshold;
                half _EdgeBevel;
                half _EdgeWidth;
                half _DetailBumps;
                half _DetailWidth;
                half _NormalMapStrength;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                clip(input.color.a * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - _AlphaClipThreshold);
                return CommonLitFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4   color           : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"
            #include "../Includes/AutoNormals2D.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half _AlphaClipThreshold;
                half _EdgeBevel;
                half _EdgeWidth;
                half _DetailBumps;
                half _DetailWidth;
                half _NormalMapStrength;
            CBUFFER_END

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonNormalsVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                half4 mainTex = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(mainTex.a - _AlphaClipThreshold);

                half3 autoNormal = AutoNormalTS(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.uv, _MainTex_TexelSize.xy, _EdgeWidth, _EdgeBevel, _DetailWidth, _DetailBumps);
                half3 mapNormal = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                half3 normalTS = BlendAutoNormal(autoNormal, mapNormal, _NormalMapStrength);

                return NormalsRenderingShared(mainTex, normalTS, input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue"="Transparent" "RenderType"="Transparent"}

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _MainTex_TexelSize;
                half _AlphaClipThreshold;
                half _EdgeBevel;
                half _EdgeWidth;
                half _DetailBumps;
                half _DetailWidth;
                half _NormalMapStrength;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                clip(input.color.a * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a - _AlphaClipThreshold);
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
