Shader "SpookyWater/Sprite Fog"
{
    Properties
    {
        [Section(Detail)] _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}

        [Section(WaterSide)] [Enum(Everywhere, 0, Above Water Only, 1, Below Water Only, 2)] _WaterClipMode("Water Side", Float) = 0
        [ShowIf(_WaterClipMode)] _WaterClipY("Waterline Y", Float) = 0
        [ShowIf(_WaterClipMode)] _WaterClipSoftness("Waterline Edge Softness", Float) = 0.1
        [ShowIf(_WaterClipMode)] [Toggle] _WaterMurkMatch("Match Water Murk", Float) = 0
        [ShowIf(_WaterMurkMatch)] _WaterMurkFromZ("Murk From Z", Float) = -100000

        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    HLSLINCLUDE
        #include "../Includes/WaterSide.hlsl"

        half4 _WaterUnderwaterColor;
        float4 _WaterUnderwaterFog;

        void ApplyWaterMurk(inout half4 color, float3 worldPos, float3 cameraWS, half match, float fromZ)
        {
            if (match < 0.5 || _WaterUnderwaterFog.y <= 0.0) return;
            float depthToPixel = worldPos.z - cameraWS.z;
            float stretch = depthToPixel > 1e-3 ? distance(cameraWS, worldPos) / depthToPixel : 0.0;
            float startZ = max(max(_WaterUnderwaterFog.x, cameraWS.z), fromZ);
            float path = stretch * max(worldPos.z - startZ, 0.0);
            color.rgb = _WaterUnderwaterColor.rgb;
            color.a = 1.0 - exp(-path / _WaterUnderwaterFog.y);
        }
    ENDHLSL

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

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
                float3 worldPos    : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _WaterClipMode;
                float _WaterClipY;
                float _WaterClipSoftness;
                half _WaterMurkMatch;
                float _WaterMurkFromZ;
            CBUFFER_END

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldPos = TransformObjectToWorld(input.positionOS);
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 color = input.color;
                ApplyWaterMurk(color, input.worldPos, GetCameraPositionWS(), _WaterMurkMatch, _WaterMurkFromZ);
                color.a *= WaterSideMask(input.worldPos.y, _WaterClipMode, _WaterClipY, _WaterClipSoftness);
                return CommonLitFragment(input, color);
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
                float3 worldPos : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _WaterClipMode;
                float _WaterClipY;
                float _WaterClipSoftness;
                half _WaterMurkMatch;
                float _WaterMurkFromZ;
            CBUFFER_END

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.worldPos = TransformObjectToWorld(input.positionOS);
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 color = input.color;
                ApplyWaterMurk(color, input.worldPos, GetCameraPositionWS(), _WaterMurkMatch, _WaterMurkFromZ);
                color.a *= WaterSideMask(input.worldPos.y, _WaterClipMode, _WaterClipY, _WaterClipSoftness);
                return CommonUnlitFragment(input, color);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
