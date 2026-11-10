Shader "Hidden/SpookyWater/Pocket Mask"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Cull Off
            ZWrite Off
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 128
                WriteMask 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float4 Vert(float3 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS);
            }

            half4 Frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
