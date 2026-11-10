#ifndef WATER_SIDE_INCLUDED
#define WATER_SIDE_INCLUDED

half WaterSideMask(float worldY, half mode, float waterY, float softness)
{
    float s = max(softness, 1e-4);
    if (mode > 1.5) return 1.0 - smoothstep(waterY - s, waterY, worldY);
    if (mode > 0.5) return smoothstep(waterY, waterY + s, worldY);
    return 1.0;
}

#endif
