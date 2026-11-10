using System;
using UnityEngine;

namespace SpookyWater
{
    [Serializable]
    public class RippleGridSettings
    {
        [Header("Simulation Grid")]
        [Tooltip("Simulation texels per world unit. Higher gives finer ripple rings and more accurate bounces " +
                 "off small obstacles, at a GPU cost that grows with the square. Everything in this group is " +
                 "read when the water is generated, so click Generate Water or restart Play mode to apply it.")]
        [Min(1f)] public float texelsPerUnit = 32f;
        [Tooltip("Largest the simulation texture may be along either axis, in texels. A level wider than " +
                 "this divided by Texels Per Unit (32 world units at the defaults) is simulated through a " +
                 "window that follows the camera, so cost and ripple sharpness stay the same however big the " +
                 "level is. Ripples that leave the window fade out.")]
        [Min(8)] public int maxResolution = 1024;
        [Tooltip("Texels per world unit of the shoreline mask, baked once for the whole level when the water is " +
                 "generated. Shores land exactly on cell edges as long as the cell edges fall on this grid.")]
        [Min(1f)] public float maskTexelsPerUnit = 16f;
        [Tooltip("Simulation ticks per second, independent of the frame rate. A tick may run several substeps " +
                 "so fast ripples stay stable.")]
        [Min(1f)] public float tickRate = 60f;
        [Tooltip("World-unit width of the absorbing layer the simulation extends past its open edges (the " +
                 "front of the water, and the far edge when a horizon extension continues the water), so " +
                 "ripples fade out there instead of bouncing back. Wider absorbs more cleanly.")]
        [Min(0.001f)] public float openEdgeWidth = 1.5f;

        [Header("Splash Energy")]
        [Tooltip("World-unit radius of the bump a contact or click stamps into the water. It also sets the " +
                 "ring spacing: rings come out roughly twice this far apart.")]
        [Min(0.01f)] public float impactRadius = 0.2f;
        [Tooltip("Height of that bump for a strength-1 contact, which is how much energy a splash puts in. " +
                 "The bump spreads into a ring, so the visible ring ends up several times lower.")]
        [Min(0f)] public float impactHeight = 4f;

        [Header("Spreading")]
        [Tooltip("World units per second that ripples travel outward. Substeps are added automatically to " +
                 "stay stable at any speed.")]
        [Min(0f)] public float speed = 4f;
        [Tooltip("Squashes ripple rings front to back (world Z), because Z stands in for depth in the 2.5D " +
                 "view and perfectly round rings look wrong. 1 is round, 0.5 is half as deep as wide. Waves " +
                 "travel slower along Z by this factor and splashes are stamped as matching ellipses, so " +
                 "bounces off shores and obstacles stay consistent. It can be changed live, but the grid's " +
                 "Z resolution follows the value at generation, so regenerate after lowering it a lot or " +
                 "the squashed rings turn blocky.")]
        [Range(0.1f, 1f)] public float zSquash = 0.5f;
        [Tooltip("Times per second the water bobs up and down after a ripple passes (Hz). It adds a spring " +
                 "pulling every point back to flat, so a splash leaves a train of rings behind its front " +
                 "instead of one ring and a faint tail. It also slows how fast a splash's energy spreads. " +
                 "0 turns it off. Damping still sets how long everything lasts.")]
        [Range(0f, 8f)] public float springFrequency;

        [Header("Energy Loss")]
        [Tooltip("How fast ripples die out over time. A ring keeps e^-damping of its energy each second, so " +
                 "1.2 loses about 70% per second. Higher means calmer water sooner.")]
        [Min(0f)] public float damping = 1.2f;
        [Tooltip("Fraction of a ring's energy lost for every world unit it travels, on top of the time " +
                 "damping, so fast and slow ripples die out after the same distance. 0 means time damping " +
                 "only. Rings also thin out naturally as they grow; this is extra.")]
        [Range(0f, 0.99f)] public float distanceLoss;
        [Tooltip("How much of a ripple's energy bounces back off a shore (an unpainted Surface cell or the " +
                 "field margin). 1 is a perfect mirror, 0 means the shore soaks it all up.")]
        [Range(0f, 1f)] public float shoreReflectivity = 1f;
        [Tooltip("How much of a ripple's energy bounces back off a WaterRippleObstacle (rock, post, sprite). " +
                 "1 is a perfect mirror, 0 means the obstacle soaks it all up.")]
        [Range(0f, 1f)] public float obstacleReflectivity = 1f;

        [Header("Wakes")]
        [Tooltip("World-unit radius of the trough a moving disturbance (a mouse drag, a swimmer, a boat) " +
                 "pushes into the water, which is roughly the width of its wake.")]
        [Min(0.01f)] public float wakeRadius = 0.15f;
        [Tooltip("Depth of that trough at full strength. It scales with how far the mover travels each " +
                 "frame, so holding still makes no wake.")]
        [Min(0f)] public float wakeHeight = 2f;
        [Tooltip("Height of the bump pushed up just ahead of the mover, relative to the trough behind it. " +
                 "It makes the wake lean in the direction of travel. 0 leaves the trough only.")]
        [Range(0f, 2f)] public float wakeBowRatio = 0.6f;
    }

    [Serializable]
    public class AmbientRippleSettings
    {
        [Tooltip("Drop small random ripples on open water so still water does not look like glass. They share " +
                 "the simulation with every other ripple, so speed and energy loss come from the Ripple Grid " +
                 "settings. These can be changed live in Play mode.")]
        public bool enabled = true;
        [Tooltip("Seconds until the next ambient ripple event, as a (min, max) range re-rolled each time.")]
        [MinMaxRange(0.05f, 10f)] public Vector2 interval = new(0.4f, 1.8f);
        [Tooltip("Ripples per event, as a (min, max) range. Above 1, a few land together nearby like a " +
                 "scatter of drops.")]
        [MinMaxRange(1f, 6f)] public Vector2Int count = new(1, 2);
        [Tooltip("World-unit spread of an event's extra ripples around its first one.")]
        [Min(0f)] public float clusterSpread = 0.35f;
        [Tooltip("Splash radius in world units, as a (min, max) range. It also sets the ring spacing (roughly " +
                 "twice this). Keep it above about 2.5 simulation texels (0.08 at 32 texels per unit) or the " +
                 "rings turn blocky.")]
        [MinMaxRange(0.02f, 0.5f)] public Vector2 radius = new(0.08f, 0.14f);
        [Tooltip("Splash height as a (min, max) range, in the same units as Impact Height.")]
        [MinMaxRange(0f, 4f)] public Vector2 strength = new(0.4f, 1.2f);
        [Tooltip("Chance that a ripple starts as a dip rather than a bump.")]
        [Range(0f, 1f)] public float downChance = 0.5f;
    }

    [Serializable]
    public class FrontRippleSettings
    {
        [Tooltip("Scatter tiny, quickly fading ripples just behind the water's front edge, only in the camera's " +
                 "view. It gives restless water close to the viewer while the far water stays calm. They are " +
                 "drawn by the water shaders rather than simulated, so they never spread across the lake. " +
                 "These can be changed live in Play mode.")]
        public bool enabled = true;
        [Tooltip("Seconds until the next one, as a (min, max) range re-rolled each time.")]
        [MinMaxRange(0.02f, 5f)] public Vector2 interval = new(0.08f, 0.3f);
        [Tooltip("How far back from the water's front edge, in world units of depth, they can appear.")]
        [Min(0f)] public float maxDistance = 1.2f;
        [Tooltip("Farthest a ripple's rings reach from its centre, in world units. The ripple is gone by then.")]
        [Range(0.05f, 2f)] public float maxRadius = 0.5f;
        [Tooltip("Seconds a ripple lasts, fading out the whole time.")]
        [Range(0.1f, 3f)] public float lifetime = 0.7f;
        [Tooltip("How fast its rings spread, in world units per second.")]
        [Min(0.01f)] public float speed = 0.8f;
        [Tooltip("Distance between its rings, in world units.")]
        [Range(0.02f, 0.5f)] public float wavelength = 0.07f;
        [Tooltip("Height as a (min, max) range, in the same units as the ripple simulation, re-rolled per " +
                 "ripple. Each one starts as a bump or a dip at random.")]
        [MinMaxRange(0f, 2f)] public Vector2 strength = new(0.15f, 0.35f);
    }
    [Serializable]
    public class WindSettings
    {
        [Tooltip("Keep the whole surface gently moving as if wind were blowing over it: long, low wave bands " +
                 "that drift with the wind and break up the reflection into wavering streaks. They are drawn " +
                 "by the water shaders on top of the ripple simulation, so splashes and wakes still work as " +
                 "before. Off leaves the water a flat mirror at rest. Every value here can be changed live.")]
        public bool enabled;
        [Tooltip("How hard the wind blows. Scales the height of the wind waves from none (0) to Wave Height (1).")]
        [Range(0f, 1f)] public float strength = 0.5f;
        [Tooltip("Direction the wind blows across the water, in degrees around the vertical: 0 blows toward " +
                 "+X (right), 90 away from the camera (+Z), 180 toward -X (left), 270 toward the camera. " +
                 "Waves travel this way and their crests lie across it.")]
        [Range(0f, 360f)] public float direction = 270f;
        [Tooltip("Height of the wind waves at full Strength, in the same units as the ripple simulation " +
                 "(multiplied by each material's Ripple Height). Even small values break up the reflection " +
                 "a lot, because the reflection follows the slope rather than the height.")]
        [Min(0f)] public float waveHeight = 0.08f;
        [Tooltip("World-unit distance between the main wave crests. Two smaller sets of waves ride on top at " +
                 "about half and a quarter of this. Waves too small to see from far away fade out smoothly " +
                 "instead of shimmering, so longer waves stay visible further into the distance.")]
        [Min(0.05f)] public float wavelength = 1.2f;
        [Tooltip("How much longer a crest is than the gap between crests. 1 gives blotchy, round bumps; " +
                 "higher gives long bands lying across the wind, which smear the reflection into streaks.")]
        [Range(1f, 8f)] public float crestLength = 3f;
        [Tooltip("World units per second the main waves travel with the wind. The smaller waves move slower, " +
                 "as on real water, so the pattern keeps changing instead of sliding past like a texture.")]
        [Min(0f)] public float speed = 0.6f;
        [Tooltip("How patchy the wind is. 0 ruffles the water evenly; 1 leaves calm, mirror-like patches " +
                 "between gusts that sweep across the water with the wind.")]
        [Range(0f, 1f)] public float gustiness = 0.5f;
        [Tooltip("World-unit size of the gust patches and the calm patches between them.")]
        [Min(0.1f)] public float gustSize = 8f;
        [Tooltip("World units per second the gust patches travel across the water.")]
        [Min(0f)] public float gustSpeed = 1.5f;
    }
}
