Shader "Hidden/SpookyWater/Ripple Simulation"
{
    Properties
    {
        _MainTex("Source", 2D) = "black" {}
        [NoScaleOffset] _RippleStaticMaskTex("Static Water Mask", 2D) = "white" {}
        [NoScaleOffset] _RippleMaskTex("Water Mask", 2D) = "white" {}
    }

    CGINCLUDE

    #include "UnityCG.cginc"

    #define WATER_MAX_RIPPLE_OBSTACLES 32
    #define WATER_MAX_RIPPLE_IMPULSES 16

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D _RippleStaticMaskTex;
    float2 _RippleStaticMaskOrigin;
    float2 _RippleStaticMaskSize;
    sampler2D _RippleMaskTex;

    float2 _RippleSimOrigin;
    float2 _RippleSimSize;

    float4 _RippleWindowSponge;
    float2 _RippleWindowSpongeWidth;
    float2 _RippleShiftUV;

    float2 _RippleCourant2;
    float _RippleZSquash;
    float _RippleStepDamping;
    float _RippleSpringStep;
    float _RippleStepRelax;
    float _RippleSpongeStep;
    float _RippleWallBand;
    float _RippleWallAbsorbStep;
    float _RippleShoreAbsorb;
    float _RippleObstacleAbsorb;

    float4 _RippleObstacleA[WATER_MAX_RIPPLE_OBSTACLES];
    float4 _RippleObstacleB[WATER_MAX_RIPPLE_OBSTACLES];
    int _RippleObstacleCount;

    float4 _RippleImpulses[WATER_MAX_RIPPLE_IMPULSES];
    float4 _RippleImpulseEnds[WATER_MAX_RIPPLE_IMPULSES];
    int _RippleImpulseCount;

    float2 SimUVToWorld(float2 uv)
    {
        return _RippleSimOrigin + uv * _RippleSimSize;
    }

    float2 StaticMask(float2 worldXZ)
    {
        return tex2D(_RippleStaticMaskTex, (worldXZ - _RippleStaticMaskOrigin) / _RippleStaticMaskSize).rg;
    }

    float WindowSponge(float2 uv)
    {
        float2 width = max(_RippleWindowSpongeWidth, 1e-4);
        float4 t = saturate(1.0 - float4(uv.x, 1.0 - uv.x, uv.y, 1.0 - uv.y) / width.xxyy) * _RippleWindowSponge;
        float m = max(max(t.x, t.y), max(t.z, t.w));
        return m * m;
    }

    float ObstacleDistance(float2 p, float4 a, float4 b)
    {
        float2 d = p - a.xy;
        if (b.z > 0.5) return length(d) - a.z;

        float2 axisX = b.xy;
        float2 axisZ = float2(-axisX.y, axisX.x);
        float2 q = abs(float2(dot(d, axisX), dot(d, axisZ))) - a.zw;
        return length(max(q, 0)) + min(max(q.x, q.y), 0);
    }

    float4 FragBuildMask(v2f_img i) : SV_Target
    {
        float2 p = SimUVToWorld(i.uv);
        if (StaticMask(p).r < 0.5) return 0;

        float obstacleNear = 0;
        for (int k = 0; k < _RippleObstacleCount; k++)
        {
            float d = ObstacleDistance(p, _RippleObstacleA[k], _RippleObstacleB[k]);
            if (d <= 0) return 0;
            obstacleNear = max(obstacleNear, 1.0 - saturate(d / _RippleWallBand));
        }

        float shoreNear = 0;
        [unroll]
        for (int dir = 0; dir < 8; dir++)
        {
            float angle = dir * (UNITY_PI / 4.0);
            float2 stepWorld = float2(cos(angle), sin(angle)) * _RippleWallBand;
            [unroll]
            for (int depth = 1; depth <= 3; depth++)
            {
                float t = depth / 3.0;
                if (StaticMask(p + stepWorld * t).r < 0.5) shoreNear = max(shoreNear, 1.0 - t + 1.0 / 3.0);
            }
        }

        float absorb = max(shoreNear * shoreNear * _RippleShoreAbsorb, obstacleNear * obstacleNear * _RippleObstacleAbsorb);
        return float4(1, saturate(absorb), 0, 0);
    }

    float NeighbourHeight(float2 uv, float centre)
    {
        float h = tex2D(_MainTex, uv).r;
        return tex2D(_RippleMaskTex, uv).r > 0.5 ? h : centre;
    }

    float4 FragStep(v2f_img i) : SV_Target
    {
        float2 mask = tex2D(_RippleMaskTex, i.uv).rg;
        if (mask.r < 0.5) return 0;

        float2 s = tex2D(_MainTex, i.uv).rg;
        float2 texel = _MainTex_TexelSize.xy;

        float l = NeighbourHeight(i.uv - float2(texel.x, 0), s.r);
        float r = NeighbourHeight(i.uv + float2(texel.x, 0), s.r);
        float d = NeighbourHeight(i.uv - float2(0, texel.y), s.r);
        float u = NeighbourHeight(i.uv + float2(0, texel.y), s.r);

        float accel = _RippleCourant2.x * (l + r - 2.0 * s.r) + _RippleCourant2.y * (d + u - 2.0 * s.r)
            - _RippleSpringStep * s.r;
        float sponge = max(StaticMask(SimUVToWorld(i.uv)).g, WindowSponge(i.uv));
        float absorb = exp(-sponge * _RippleSpongeStep - mask.g * _RippleWallAbsorbStep);
        float velocity = (s.r - s.g) * _RippleStepDamping * absorb;
        float next = (s.r + velocity + accel) * _RippleStepRelax * absorb;
        return float4(next, s.r, 0, 0);
    }

    float DistanceToSegment(float2 p, float2 a, float2 b)
    {
        float2 ab = b - a;
        float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
        return distance(p, a + ab * t);
    }

    float4 FragImpulse(v2f_img i) : SV_Target
    {
        float2 s = tex2D(_MainTex, i.uv).rg;
        float2 unsquash = float2(1.0, 1.0 / max(_RippleZSquash, 1e-3));
        float2 p = SimUVToWorld(i.uv) * unsquash;

        for (int k = 0; k < _RippleImpulseCount; k++)
        {
            float4 imp = _RippleImpulses[k];
            float t = DistanceToSegment(p, imp.xy * unsquash, _RippleImpulseEnds[k].xy * unsquash) / max(imp.z, 1e-4);
            if (t < 1.0)
            {
                float bump = imp.w * (0.5 + 0.5 * cos(t * UNITY_PI));
                s += bump;
            }
        }
        return float4(s, 0, 0);
    }

    float4 FragShift(v2f_img i) : SV_Target
    {
        float2 uv = i.uv + _RippleShiftUV;
        if (any(uv < 0.0) || any(uv > 1.0)) return 0;
        return tex2D(_MainTex, uv);
    }

    float4 FragFillWalls(v2f_img i) : SV_Target
    {
        float2 s = tex2D(_MainTex, i.uv).rg;
        if (tex2D(_RippleMaskTex, i.uv).r > 0.5) return float4(s, 0, 0);

        float2 texel = _MainTex_TexelSize.xy;
        float sum = 0;
        float weight = 0;
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                float2 uv = i.uv + float2(x, y) * texel;
                float w = (x == 0 || y == 0) ? 1.0 : 0.5;
                w *= step(0.5, tex2D(_RippleMaskTex, uv).r);
                sum += tex2D(_MainTex, uv).r * w;
                weight += w;
            }
        }

        float h = weight > 0 ? sum / weight : 0;
        return float4(h, h, 0, 0);
    }

    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "BuildMask"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment FragBuildMask
            ENDCG
        }

        Pass
        {
            Name "Step"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragStep
            ENDCG
        }

        Pass
        {
            Name "Impulse"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment FragImpulse
            ENDCG
        }

        Pass
        {
            Name "Shift"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragShift
            ENDCG
        }

        Pass
        {
            Name "FillWalls"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert_img
            #pragma fragment FragFillWalls
            ENDCG
        }
    }
}
