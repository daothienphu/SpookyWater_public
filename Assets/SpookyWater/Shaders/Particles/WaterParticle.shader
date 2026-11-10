Shader "SpookyWater/Water Particle"
{
    Properties
    {
        [Section(Shape)] [Enum(Droplet, 0, Bubble, 1, Ring, 2, Crown, 3, Puff, 5)] _Shape("Shape", Float) = 0
        _Softness("Edge Softness", Range(0.01, 1)) = 0.35
        _RimWidth("Rim Width", Range(0.02, 0.6)) = 0.18
        _FillOpacity("Fill Opacity", Range(0, 1)) = 0.15
        _Glint("Glint", Range(0, 2)) = 1
        _Breakup("Breakup", Range(0, 2)) = 1

        [Section(Color)] _Color("Tint", Color) = (1,1,1,1)
        _FoamColor("Foam Color", Color) = (1,1,1,1)

        [Section(Blending)] [Enum(Off, 0, On, 1)] _ZWrite("Write Depth", Float) = 0
        [ShowIf(_ZWrite)] _DepthCutoff("Depth Write Cutoff", Range(0, 1)) = 0.3
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4
    }

    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _FoamColor;
            half _Shape;
            half _Softness;
            half _RimWidth;
            half _FillOpacity;
            half _Glint;
            half _Breakup;
            half _ZWrite;
            half _DepthCutoff;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color       : COLOR;
            float4 uv         : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color       : COLOR;
            float4 uv         : TEXCOORD0;
        };

        float Hash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                        lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
        }

        float Fbm(float2 p)
        {
            return ValueNoise(p) * 0.6 + ValueNoise(p * 2.3 + 17.1) * 0.3 + ValueNoise(p * 5.1 + 3.7) * 0.1;
        }

        half Fill(float d)
        {
            float w = max(fwidth(d), 1e-4);
            return saturate(0.5 - d / w);
        }

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            o.color = input.color * _Color;
            o.uv = input.uv;
            return o;
        }

        half4 Droplet(float2 p, float r, half4 tint)
        {
            half soft = max(_Softness, 1e-3);
            half disc = 1.0 - smoothstep(1.0 - soft, 1.0, r);
            half shade = saturate(dot(p, float2(0.55, -0.75)) * 0.5 + 0.5);
            half3 color = tint.rgb * lerp(1.15, 0.7, shade);
            half crescent = smoothstep(0.55, 0.95, r) * saturate(dot(normalize(p + 1e-4), float2(-0.6, 0.8)));
            half glint = 1.0 - smoothstep(0.0, 0.28, length(p - float2(-0.3, 0.32)));
            color = lerp(color, _FoamColor.rgb, saturate(crescent * 0.8 + glint * _Glint));
            return half4(color, disc);
        }

        half4 Crown(float2 uv, float age, float seed, half4 tint)
        {
            float x = uv.x * 2.0 - 1.0;
            float y = uv.y;

            float r1 = frac(seed * 7.13), r2 = frac(seed * 13.71), r3 = frac(seed * 3.97);
            x -= (r1 - 0.5) * 0.4 * y;
            float wallHeight = x < 0.0 ? lerp(0.7, 1.05, r2) : lerp(0.7, 1.05, r3);
            float ax = abs(x);

            float outer = lerp(0.45, lerp(0.7, 0.97, saturate(age * 1.5)), sqrt(saturate(y)));
            float inner = lerp(0.05, outer - 0.3, saturate(y * 1.3));

            float lip = lerp(0.4, 0.92, smoothstep(0.0, 0.8, ax)) * wallHeight;
            lip *= 0.85 + 0.3 * Fbm(float2(x * 5.0 + seed * 31.0, age * 2.5 + seed * 7.0));
            float fingers = pow(ValueNoise(float2(x * 7.0 + seed * 57.0, seed * 3.0)), 2.5);
            lip += fingers * 0.35 * smoothstep(0.05, 0.45, age) * smoothstep(0.1, 0.5, ax);
            lip *= 1.0 - 0.45 * smoothstep(0.35, 1.0, age);

            float body = Fill(max(ax - outer, y - lip));
            if (body <= 0.0) return 0;

            float rel = y / max(lip, 1e-3);
            float holes = Fbm(uv * float2(7.0, 5.0) + seed * 13.0 + float2(0.0, -age * 0.9));
            float tear = saturate((age * 1.4 - 0.4) * _Breakup + rel * 0.3 * _Breakup + fingers * age * 0.6);
            body *= smoothstep(tear - 0.05, tear + 0.05, holes);

            float wall = smoothstep(inner - 0.1, inner + 0.15, ax);
            float foam = smoothstep(0.8, 1.0, rel);
            float streak = ValueNoise(float2(x * 18.0 + seed * 5.0, y * 2.0 - age * 3.0));
            float bands = sin(y * 16.0 - age * 14.0 + x * 3.0 + seed * 20.0) * 0.5 + 0.5;
            half3 color = tint.rgb * lerp(0.8, 1.15, y) * lerp(1.0, 1.2, (streak * 0.7 + bands * 0.3) * wall);
            color = lerp(color, tint.rgb * 0.55, smoothstep(outer - 0.09, outer, ax) * (1.0 - foam));
            color = lerp(color, _FoamColor.rgb, foam);
            half alpha = body * lerp(_FillOpacity * lerp(0.7, 1.3, bands), 0.95, saturate(wall + foam)) * smoothstep(0.0, 0.06, y);
            return half4(color, alpha);
        }

        half4 Frag(Varyings input) : SV_Target
        {
            float2 p = input.uv.xy * 2.0 - 1.0;
            float r = length(p);
            float age = input.uv.z;
            float seed = input.uv.w;
            half soft = max(_Softness, 1e-3);
            half disc = 1.0 - smoothstep(1.0 - soft, 1.0, r);

            half4 result;
            if (_Shape < 0.5) result = Droplet(p, r, input.color);
            else if (_Shape < 1.5)
            {
                half rimInner = 1.0 - _RimWidth;
                half rim = smoothstep(rimInner - soft * 0.5, rimInner, r) * disc;
                half glint = 1.0 - smoothstep(0.0, 0.28, length(p - float2(-0.38, 0.38)));
                half alpha = saturate(max(rim, disc * _FillOpacity) + glint * _Glint);
                result = half4(input.color.rgb + glint * _Glint, alpha);
            }
            else if (_Shape < 2.5)
            {
                half rimInner = 1.0 - _RimWidth;
                result = half4(input.color.rgb, smoothstep(rimInner - soft * 0.5, rimInner, r) * disc);
            }
            else if (_Shape < 3.5) result = Crown(input.uv.xy, age, seed, input.color);
            else
            {
                half n = Fbm(input.uv.xy * 3.0 + seed * 9.0 + age * 0.8);
                result = half4(input.color.rgb, exp(-r * r * 3.0) * saturate(n * 1.6 - 0.3));
            }

            result.a *= input.color.a;
            if (_ZWrite > 0.5) clip(result.a - _DepthCutoff);
            return result;
        }
    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]
        ZTest [_ZTest]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    CustomEditor "SpookyWater.Editor.SpookyWaterShaderGUI"
}
