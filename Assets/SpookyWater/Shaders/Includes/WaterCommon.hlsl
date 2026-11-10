#ifndef WATER_COMMON_INCLUDED
#define WATER_COMMON_INCLUDED

float2 _WaterFieldOrigin;
float2 _WaterFieldSize;

float2 WaterFieldUV(float3 positionWS)
{
    return saturate((positionWS.xz - _WaterFieldOrigin) / max(_WaterFieldSize, 1e-4));
}

TEXTURE2D(_RippleSimTexture); SAMPLER(sampler_RippleSimTexture);
float2 _RippleSimOrigin;
float2 _RippleSimSize;

#define WATER_MAX_MICRO_RIPPLES 16
float4 _WaterMicroRipples[WATER_MAX_MICRO_RIPPLES];
float4 _WaterMicroRippleParams;

float MicroRippleHeight(float2 worldXZ)
{
    float maxRadius = _WaterMicroRippleParams.z;
    if (maxRadius <= 0.0) return 0.0;

    float wavelength = max(_WaterMicroRippleParams.y, 1e-3);
    float k = 6.2831853 / wavelength;
    float lifetime = max(_WaterMicroRippleParams.w, 1e-3);
    float h = 0.0;
    for (int i = 0; i < WATER_MAX_MICRO_RIPPLES; i++)
    {
        float4 r = _WaterMicroRipples[i];
        float2 offset = worldXZ - r.xy;
        float d2 = dot(offset, offset);
        float age = _TimeParameters.x - r.z;
        [branch] if (r.w == 0.0 || d2 >= maxRadius * maxRadius || age < 0.0 || age > lifetime) continue;

        float d = sqrt(d2);
        float front = _WaterMicroRippleParams.x * age;
        float t = age / lifetime;

        float envelope = saturate((front - d) / wavelength) * (1.0 - smoothstep(maxRadius * 0.55, maxRadius, d))
            * (1.0 - t) * (1.0 - t);
        h += r.w * envelope * sin(k * (d - front));
    }
    return h;
}

#define WATER_MAX_BOW_WAVES 8
float4 _WaterBowWaves[WATER_MAX_BOW_WAVES];
float4 _WaterBowWaveShapes[WATER_MAX_BOW_WAVES];
int _WaterBowWaveCount;

float BowWaveHeight(float2 worldXZ)
{
    float h = 0.0;
    for (int i = 0; i < min(_WaterBowWaveCount, WATER_MAX_BOW_WAVES); i++)
    {
        float4 w = _WaterBowWaves[i];
        float4 s = _WaterBowWaveShapes[i];
        float dz = (worldXZ.y - w.y) / max(s.y, 1e-3);
        float u = (worldXZ.x - w.x) * s.x / max(w.w, 1e-3);
        [branch] if (abs(dz) >= 1.0 || u < -3.0 || u > 3.0) continue;

        float x = u > 0.0 ? u / max(s.z, 0.05) : u;
        float depthFade = 1.0 - dz * dz;
        float mound = exp(-2.2 * x * x);
        float trough = s.w * exp(-2.2 * (u + 1.6) * (u + 1.6));
        h += w.z * (mound - trough) * depthFade * depthFade;
    }
    return h;
}

float SampleRippleField(float2 worldXZ)
{
    float micro = MicroRippleHeight(worldXZ) + BowWaveHeight(worldXZ);
    float2 uv = (worldXZ - _RippleSimOrigin) / max(_RippleSimSize, 1e-4);
    if (any(uv < 0.0) || any(uv > 1.0)) return micro;
    return SAMPLE_TEXTURE2D_LOD(_RippleSimTexture, sampler_RippleSimTexture, uv, 0).r + micro;
}

TEXTURE2D(_WaterShoreDistanceTex); SAMPLER(sampler_WaterShoreDistanceTex);
float4 _WaterShoreDistanceRect;

float SampleShoreDistance(float2 worldXZ)
{
    if (any(_WaterShoreDistanceRect.zw <= 0.0)) return 1e4;
    float2 uv = (worldXZ - _WaterShoreDistanceRect.xy) / _WaterShoreDistanceRect.zw;
    return SAMPLE_TEXTURE2D_LOD(_WaterShoreDistanceTex, sampler_WaterShoreDistanceTex, uv, 0).r;
}

float2 WaterHash2(float2 p)
{
    p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return -1.0 + 2.0 * frac(sin(p) * 43758.5453);
}

float3 WaterNoiseD(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);

    float2 ga = WaterHash2(i);
    float2 gb = WaterHash2(i + float2(1, 0));
    float2 gc = WaterHash2(i + float2(0, 1));
    float2 gd = WaterHash2(i + float2(1, 1));
    float va = dot(ga, f);
    float vb = dot(gb, f - float2(1, 0));
    float vc = dot(gc, f - float2(0, 1));
    float vd = dot(gd, f - float2(1, 1));

    float value = va + u.x * (vb - va) + u.y * (vc - va) + u.x * u.y * (va - vb - vc + vd);
    float2 derivative = ga + u.x * (gb - ga) + u.y * (gc - ga) + u.x * u.y * (ga - gb - gc + gd)
        + du * (u.yx * (va - vb - vc + vd) + float2(vb, vc) - va);
    return float3(value, derivative);
}

float4 _WaterWind;
float4 _WaterWindShape;
float4 _WaterWindPhase;

float3 WaterWindOctave(float2 worldXZ, float2 dir, float wavelength, float stretch, float phase, float seed)
{
    float2 across = float2(-dir.y, dir.x);
    float2 scale = float2(1.0 / wavelength, 1.0 / (wavelength * stretch));
    float2 q = float2(dot(worldXZ, dir) - phase, dot(worldXZ, across)) * scale + seed;
    float3 n = WaterNoiseD(q);
    return float3(n.x, n.y * scale.x * dir + n.z * scale.y * across);
}

float WaterWindDetail(float wavelength, float footprint)
{
    return smoothstep(2.0, 6.0, wavelength / max(footprint, 1e-5));
}

float2 WaterRotate(float2 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2(v.x * c - v.y * s, v.x * s + v.y * c);
}

float3 WaterWindWaves(float2 worldXZ)
{
    float height = _WaterWind.x;
    [branch] if (height <= 0.0) return 0.0;

    float wavelength = max(_WaterWind.y, 1e-3);
    float stretch = max(_WaterWindShape.z, 1.0);
    float2 dir = _WaterWindShape.xy;

    float3 cameraWS = GetCameraPositionWS();
    float depth = max(worldXZ.y - cameraWS.z, 0.1);
    float pixelAngle = 2.0 / max(_ScreenParams.y * abs(UNITY_MATRIX_P._m11), 1.0);
    float camHeight = max(abs(cameraWS.y - _WaterWind.w), 0.25);
    float footprint = depth * pixelAngle * (1.0 + depth / camHeight);

    float3 w = WaterWindOctave(worldXZ, dir, wavelength, stretch, _WaterWindPhase.x, 0.0)
        * WaterWindDetail(wavelength, footprint);
    float l1 = wavelength * 0.47;
    w += 0.5 * WaterWindOctave(worldXZ, WaterRotate(dir, 0.45), l1, stretch * 0.8, _WaterWindPhase.y, 17.3)
        * WaterWindDetail(l1, footprint);
    float l2 = wavelength * 0.23;
    w += 0.25 * WaterWindOctave(worldXZ, WaterRotate(dir, -0.6), l2, stretch * 0.65, _WaterWindPhase.z, 41.9)
        * WaterWindDetail(l2, footprint);

    float gustiness = _WaterWind.z;
    float gust = 1.0;
    float2 gustGrad = 0.0;
    [branch] if (gustiness > 0.0)
    {
        float gustSize = max(_WaterWindShape.w, 0.1);
        float3 g = WaterNoiseD((worldXZ - dir * _WaterWindPhase.w) / gustSize + 7.7);
        float t = saturate(g.x / 0.7 + 0.5);
        gust = 1.0 - gustiness * (1.0 - t * t * (3.0 - 2.0 * t));
        gustGrad = gustiness * 6.0 * t * (1.0 - t) / 0.7 * g.yz / gustSize;
    }

    return height * float3(gust * w.x, gust * w.yz + w.x * gustGrad);
}

float2 WaterWindReflectionDistortion(float2 worldXZ, float rippleScale, float normalStrength, float distortion)
{
    float2 slope = WaterWindWaves(worldXZ).yz * rippleScale * normalStrength;
    float2 d = normalize(float3(-slope.x, 1.0, -slope.y)).xz * distortion;
    d.y = min(d.y, 0.0);
    return d;
}

float SampleRipple(float2 worldXZ)
{
    return SampleRippleField(worldXZ) + WaterWindWaves(worldXZ).x;
}

float2 RippleGradient(float2 worldXZ, float step)
{
    float dx = SampleRippleField(worldXZ + float2(step, 0)) - SampleRippleField(worldXZ - float2(step, 0));
    float dz = SampleRippleField(worldXZ + float2(0, step)) - SampleRippleField(worldXZ - float2(0, step));
    return float2(dx, dz) / (2.0 * step) + WaterWindWaves(worldXZ).yz;
}

float4 WaterHash4(float2 cell)
{
    return float4(WaterHash2(cell), WaterHash2(cell + 17.31)) * 0.5 + 0.5;
}

float4 WaterWaveletSet(float2 worldXZ, float rowSpacing, float stretch, float thickness, float softness, float breakup,
    float contact, float lap, float lapTime, float drift, float twinkleTime, float glintChance, float density, float aaRows,
    float seed, out float strokeLength, out float rowShift)
{
    float cellLength = rowSpacing * stretch;
    float bob = lap * sin(lapTime + WaterNoiseD(float2(worldXZ.x / cellLength * 0.35, seed)).x * 6.2832);
    float rowF = (worldXZ.y + drift) / rowSpacing + bob;
    float row = floor(rowF);
    float colF = worldXZ.x / cellLength + WaterHash4(float2(row, seed)).x;
    float col = floor(colF);
    float4 r = WaterHash4(float2(col, row) + seed * 3.7);
    float4 q = WaterHash4(float2(row, col) + seed * 5.3 + 11.0);

    rowShift = WaterNoiseD(float2(worldXZ.x / cellLength * 0.5 + lapTime * 0.05, row * 1.37 + seed)).x
        * 2.0 * cellLength * contact * contact;

    float lengthFraction = lerp(0.5, 1.0, r.y) * lerp(1.0, 0.6, contact);
    strokeLength = cellLength * lengthFraction;

    float u = (frac(colF) - 0.5) / (0.5 * lengthFraction);
    float wobble = WaterNoiseD(float2(u * 1.3 + r.w * 17.0, row * 0.71 + seed)).x * breakup * thickness * 0.6;
    float center = (q.x - 0.5) * 2.0 * max(0.5 - thickness * (1.0 + 0.6 * breakup), 0.0) + wobble;
    float v = frac(rowF) - 0.5 - center;

    float edge = WaterNoiseD(float2(u * 2.3 + r.w * 31.0, v / max(thickness, 1e-3) * 1.5 + q.y * 9.0)).x;
    float profile = lerp(saturate(1.0 - u * u), sqrt(saturate(1.0 - u * u)), softness);
    float halfThick = thickness * profile * max(1.0 + breakup * 0.9 * edge, 0.0);
    halfThick = min(halfThick, max(0.5 - abs(center), 0.0));
    float aa = max(aaRows, 1e-4);
    float feather = max(halfThick * softness, aa);

    float lens = saturate((halfThick - abs(v)) / feather + 0.5 * (1.0 - softness));
    lens = lens * lens * (3.0 - 2.0 * lens) * saturate(halfThick / aa * 2.0);

    float grain = WaterNoiseD(worldXZ / rowSpacing * float2(0.45, 1.6) + seed * 5.0 + r.zw * 4.0).x;
    lens *= saturate(1.0 + breakup * (grain * 1.4 - 0.25));

    float life = lerp(0.4, 1.0, 0.5 + 0.5 * sin(twinkleTime + q.y * 6.2832));
    float presence = smoothstep(r.x - 0.3, r.x + 0.3, contact * life * 1.4 * density) * saturate(contact * 2.5);
    return float4(lens * presence, r.z * 2.0 - 1.0, q.z < 0.5 ? -1.0 : 1.0, q.w < glintChance ? 1.0 : 0.0);
}

float4 WaterContactWavelets(float2 worldXZ, float contact, float rowsPerUnit, float stretch, float thickness,
    float softness, float breakup, float lap, float lapTime, float drift, float perspective, float twinkleTime,
    float glintChance, float density, float2 dPdx, float2 dPdy, out float strokeLength, out float rowSpacing, out float rowShift)
{
    const float minPixels = 4.0;
    float baseSpacing = 1.0 / rowsPerUnit;
    float footprintZ = max(abs(dPdx.y), abs(dPdy.y)) + 1e-6;
    float octave = min(max(log2(footprintZ * minPixels / baseSpacing) * (1.0 - saturate(perspective)), 0.0), 6.0);
    float o0 = floor(octave);
    float blend = octave - o0;
    float s0 = baseSpacing * exp2(o0);
    float s1 = s0 * 2.0;
    float l0, l1, r0, r1;
    float4 w0 = WaterWaveletSet(worldXZ, s0, stretch, thickness, softness, breakup, contact, lap, lapTime, drift, twinkleTime,
        glintChance, density, footprintZ / s0, o0 * 7.13, l0, r0);
    float4 w1 = WaterWaveletSet(worldXZ, s1, stretch, thickness, softness, breakup, contact, lap, lapTime, drift, twinkleTime,
        glintChance, density, footprintZ / s1, o0 * 7.13 + 7.13, l1, r1);
    strokeLength = lerp(l0, l1, blend);
    rowShift = lerp(r0, r1, blend);
    rowSpacing = lerp(s0, s1, blend);
    return lerp(w0, w1, blend);
}

float2 WaterStrokeScreenSize(float strokeLength, float rowSpacing, float2 dPdx, float2 dPdy)
{
    float2 strokeUV = float2(strokeLength / (max(abs(dPdx.x), abs(dPdy.x)) + 1e-6) * (_ScreenParams.z - 1.0),
        rowSpacing / (max(abs(dPdx.y), abs(dPdy.y)) + 1e-6) * (_ScreenParams.w - 1.0));
    return clamp(strokeUV, -0.1, 0.1);
}

#define WATER_MAX_CONTACTS 64
float4 _WaterContacts[WATER_MAX_CONTACTS];
int _WaterContactCount;

float WaterContactMask(float2 worldXZ, float2 cameraXZ, float reach, float spread, float shoreAmount, float minReach)
{
    reach = max(reach, 1e-3);
    spread *= max(minReach / reach, 1.0);
    reach = max(reach, minReach);
    float toCamera = max(worldXZ.y - cameraXZ.y, 1e-3);
    float mask = 0;
    for (int k = 0; k < min(_WaterContactCount, WATER_MAX_CONTACTS); k++)
    {
        float4 c = _WaterContacts[k];
        float dz = c.z - worldXZ.y;
        if (dz < -0.05 || dz > reach) continue;
        float x = worldXZ.x + (worldXZ.x - cameraXZ.x) * max(dz, 0.0) / toCamera;
        float t = saturate(dz / reach);
        float side = max(c.x - x, x - c.y);
        float sideFade = 1.0 - saturate(side / (spread * (0.25 + 0.75 * t) + 1e-3));
        mask = max(mask, smoothstep(1.0, 0.0, t) * sideFade);
    }
    [branch] if (shoreAmount > 0.0)
        mask = max(mask, smoothstep(reach, 0.0, SampleShoreDistance(worldXZ)) * shoreAmount);
    return mask;
}

float SampleWaterHeight(float2 worldXZ, float rippleScale, float rippleOffset)
{
    return SampleRipple(worldXZ) * rippleScale + rippleOffset;
}

float WaterEdgeFalloff1D(float x, float edgeWidth)
{
    float w = max(edgeWidth, 1e-4);
    return smoothstep(0.0, w, x) * smoothstep(0.0, w, 1.0 - x);
}

float4 _WaterHorizonSeam;
float4 _WaterHorizonHeightScale;
float _WaterHorizonEdgeFalloff;

float HorizonSeamVertexHeight(float worldX)
{
    float2 worldXZ = float2(worldX, _WaterHorizonSeam.x);
    float2 fieldUV = WaterFieldUV(float3(worldXZ.x, 0.0, worldXZ.y));
    float h = SampleWaterHeight(worldXZ, _WaterHorizonHeightScale.x, _WaterHorizonHeightScale.y);
    return h * WaterEdgeFalloff1D(fieldUV.x, _WaterHorizonEdgeFalloff);
}

float BlendIntoHorizonSeam(float h, float3 restWS)
{
    if (_WaterHorizonSeam.y <= 0.0) return h;

    float w = smoothstep(_WaterHorizonSeam.x - _WaterHorizonSeam.y, _WaterHorizonSeam.x, restWS.z);
    if (w <= 0.0) return h;

    float spacing = max(_WaterHorizonSeam.z, 1e-4);
    float column = floor((restWS.x - _WaterHorizonSeam.w) / spacing);
    float x0 = _WaterHorizonSeam.w + column * spacing;
    float t = saturate((restWS.x - x0) / spacing);
    float seamH = lerp(HorizonSeamVertexHeight(x0), HorizonSeamVertexHeight(x0 + spacing), t);
    return lerp(h, seamH, w);
}

float4 _WaterSurfaceDisplacement;
half4 _WaterSurfaceFogColor;
float4 _WaterSurfaceFogParams;

float WaterSurfaceHeightAt(float3 restWS)
{
    float2 fieldUV = WaterFieldUV(restWS);
    float settle = pow(saturate(1.0 - fieldUV.y), max(_WaterSurfaceDisplacement.z, 1e-3));
    float h = SampleWaterHeight(restWS.xz, _WaterSurfaceDisplacement.x, _WaterSurfaceDisplacement.y);
    h *= WaterEdgeFalloff1D(fieldUV.x, max(_WaterSurfaceDisplacement.w, 1e-3)) * settle;
    return BlendIntoHorizonSeam(h, restWS);
}

float3 ReconstructWaterNormalOS(float2 worldXZ, float rippleScale, float step, float strength)
{
    float2 d = RippleGradient(worldXZ, step) * (2.0 * step) * rippleScale * strength;
    return normalize(float3(-d.x, 1.0, -d.y));
}

half3 WaterDepthGradient(half3 shallowColor, half3 deepColor, half t, half power)
{
    t = pow(saturate(t), max(power, 0.001));
    return lerp(shallowColor, deepColor, t);
}

half3 ApplyWaterDepthFog(half3 albedo, float worldZ, half4 fogColor, float startZ, float endZ, half power, half amount)
{
    half t = saturate((worldZ - startZ) / max(endZ - startZ, 1e-3));
    return lerp(albedo, fogColor.rgb, pow(t, max(power, 0.001)) * fogColor.a * amount);
}

half WaterReflectionFogScale(half reflectionMix, half reflectionCoverage, half reflectionFog)
{
    return lerp(1.0, reflectionFog, saturate(reflectionMix * reflectionCoverage));
}

half WaterWorldDepthT(float worldY, float surfaceY, half depthRange)
{
    half t = saturate((surfaceY - worldY) / max(depthRange, 1e-4));
    return t + t * t - t * t * t;
}

half3 RippleDebugColor(float ripple, half visScale)
{
    half3 rest = half3(0.05, 0.05, 0.08);
    half3 up = half3(1.0, 0.2, 0.15);
    half3 down = half3(0.15, 0.5, 1.0);
    half t = saturate(abs(ripple) * visScale);
    return lerp(rest, ripple >= 0 ? up : down, t);
}

#endif
