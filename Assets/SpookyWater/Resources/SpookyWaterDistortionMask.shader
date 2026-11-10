Shader "Hidden/SpookyWater/Distortion Mask"
{
    Properties
    {
        [HideInInspector] _MainTex("Sprite Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            ZTest Always
            BlendOp Max
            Blend One One

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   alpha      : TEXCOORD1;
            };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.alpha = input.color.a * unity_SpriteColor.a;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.alpha;
                return half4(a, a, a, a);
            }
            ENDHLSL
        }
    }
}
