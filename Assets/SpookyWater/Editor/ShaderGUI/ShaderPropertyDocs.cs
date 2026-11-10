using System.Collections.Generic;

namespace SpookyWater.Editor
{
    public static class ShaderPropertyDocs
    {
        public const string GeneralSectionId = "General";

        public static bool TryGetTooltip(string shaderName, string propertyName, out string tooltip)
        {
            if (!string.IsNullOrEmpty(shaderName) && Tooltips.TryGetValue(shaderName + "|" + propertyName, out tooltip)) return true;
            return Tooltips.TryGetValue(propertyName, out tooltip);
        }

        public static bool TryGetSection(string id, out string title, out string tooltip)
        {
            if (Sections.TryGetValue(id, out var entry))
            {
                title = entry.title;
                tooltip = entry.tooltip;
                return true;
            }

            title = Prettify(id);
            tooltip = null;
            return false;
        }

        public static bool TryGetIntro(string shaderName, out string intro)
        {
            return ShaderIntro.TryGetValue(shaderName ?? string.Empty, out intro);
        }

        public static string Prettify(string id)
        {
            if (string.IsNullOrEmpty(id)) return GeneralSectionId;
            var builder = new System.Text.StringBuilder(id.Length + 4);
            for (var i = 0; i < id.Length; i++)
            {
                var c = id[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(id[i - 1])) builder.Append(' ');
                builder.Append(c);
            }

            return builder.ToString();
        }

        public static readonly Dictionary<string, (string title, string tooltip)> Sections = new Dictionary<string, (string title, string tooltip)>
        {
            ["General"] = ("General", "Settings that are not part of a named group."),
            ["Other"] = ("Other", "Settings that are not part of a named group."),
            ["Surface"] = ("Surface", "How the mesh moves: how strongly ripples displace it and how that movement fades toward the far edge and the ends of the level."),
            ["Color"] = ("Color", "The water's own colors and how they blend with depth."),
            ["Clarity"] = ("Clarity", "How much of the scene behind the front glass shows through, and how fast that is lost with depth."),
            ["Mist"] = ("Mist", "A glowing haze, lit from above, that veils everything seen through the water. Off at Mist Amount 0."),
            ["Waterline"] = ("Waterline", "The bright foam line and soft glow along the top edge of the front glass."),
            ["Reflection"] = ("Reflection", "The mirror image of reflectable sprites and the sky, and how ripples wobble and blur it."),
            ["ContactWavelets"] = ("Contact Wavelets", "Tiny horizontal strokes lapping where objects, obstacles and banks meet the water, smearing and shifting the reflection there."),
            ["Shimmer"] = ("Surface Shimmer", "Thin broken glints on the surface. Off at Shimmer Strength 0."),
            ["RippleHighlight"] = ("Ripple Highlight", "Light and dark bands drawn on ripple rings so they read clearly."),
            ["Underside"] = ("Underside", "What the surface looks like from below when the camera is underwater: what is seen through it and where it turns into a mirror."),
            ["Glow"] = ("Glow", "Light the water gives off by itself: it stays bright in a dark scene and, with HDR values and URP Bloom, spills a soft halo. Off at Glow Intensity 0."),
            ["DepthFog"] = ("Depth Fog", "Fades the water into a fog color with distance (world Z). Match it to the fog layers in your scene. Alpha 0 turns it off."),
            ["Distortion"] = ("Distortion", "Bends everything seen through the front glass: a slow boiling wobble, plus a bulge under passing ripples near the surface. Both fade out toward the waterline (Surface Calm Depth) so things half in the water stay whole. Sorting layers can be kept sharp under Project Settings > SpookyWater > Distorted Sorting Layers (e.g. untick Player)."),
            ["Horizon"] = ("Horizon", "How the far water fades into haze and the horizon color."),
            ["Debris"] = ("Debris", "Look of floating debris: tint, edge cut-out, tilt shading and wet edge."),
            ["Shape"] = ("Shape", "What each particle draws and how soft its edges are."),
            ["Blending"] = ("Blending", "How the result is combined with what is already on screen, and depth behavior."),
            ["Bands"] = ("Bands", "Default band images for the two layers every shaft scrolls sideways, unless its Water Light Shaft component sets its own."),
            ["WaterSide"] = ("Water Side", "Limits the effect to above or below the waterline."),
            ["Detail"] = ("Detail", "Optional texture and fine-tuning inputs."),
            ["AutoNormals"] = ("Auto Normals", "Fakes a normal map from the sprite itself, so Light 2Ds with Normal Maps enabled shade it with relief and rim light - no normal map needed. Edges bulge outward like a soft pillow (Edge Bevel) and bright details stand out of dark ones (Detail Bumps). Costs 8 extra texture reads per pixel, only in the normals pass, which runs only while a normal-mapped light is in view."),
            ["Debug"] = ("Debug", "Visualizations for diagnosing the simulation. Leave off in a finished scene."),
        };

        public static readonly Dictionary<string, string> ShaderIntro = new Dictionary<string, string>
        {
            ["SpookyWater/Water Surface"] = "The reflective top of the water, painted on the Surface tilemap. Only ripples move it. Reflections, depth fog and the underside view are all set here.",
            ["SpookyWater/Water Front"] = "The cross-section glass of the water, painted on the Front tilemap, plus the animated waterline. It tints, distorts and lights everything drawn behind it. Keep Ripple Scale equal to the Water Surface material so the two edges meet.",
            ["SpookyWater/Water Horizon"] = "The far water that fades into the horizon, generated by the Water Tilemap Builder. Reflection, fog, glow and underwater values are copied from the Water Surface material every time you press Generate Water, so edit those there.",
            ["SpookyWater/Surface Debris"] = "Material for floating debris such as lily pads and leaves. Most values are driven by the Water Surface Debris component.",
            ["SpookyWater/Water Particle"] = "Procedural splash, droplet, bubble and ring particles. Each particle material picks one Shape.",
            ["SpookyWater/Light Shaft"] = "Material for underwater Water Light Shaft components (big parallel god-ray bands). Per-shaft look is set on the component; only blending and the default band image are set here.",
            ["SpookyWater/Sprite Fog"] = "A fog sprite material that never writes depth. Use the Water Fog Sprite component to limit a sheet to above or below the water.",
            ["SpookyWater/Sprite Lit Depth Write"] = "A 2D-lit sprite material that writes depth so the water clips correctly against the sprite. Like Sprite Lit Auto Normals, it builds normals from the sprite itself, so normal-mapped lights (e.g. a Water Glow Light) shade it with relief and rim light.",
            ["SpookyWater/Sprite Lit Auto Normals"] = "A drop-in replacement for Sprite-Lit-Default that invents normals from the sprite's own shape and shading, so lights with Normal Maps enabled (e.g. a Water Glow Light) wrap around every sprite without hand-made normal maps. Needs a Light 2D with Normal Maps set to Fast or Accurate; global lights stay flat.",
        };

        public static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>
        {
            ["_EdgeBevel"] = "How strongly the sprite's outline bulges toward the viewer, so a light beside it catches the near edge and leaves the far edge dark (rim light). 0 = flat silhouette.",
            ["_EdgeWidth"] = "How far in from the outline the bevel reaches, in texels of the sprite texture. Wider = rounder, softer shapes. With a Sprite Atlas keep the atlas Padding at least this wide or neighbouring sprites bleed into the edge.",
            ["_DetailBumps"] = "Relief taken from the sprite's own shading: brighter pixels are treated as raised, darker ones as recessed. 0 = only the outline bevel. Too high and painted texture turns noisy.",
            ["_DetailWidth"] = "Size of the detail relief, in texels. 1-2 picks up brush strokes; higher follows larger painted forms.",
            ["_NormalMapStrength"] = "How much a hand-made Normal Map adds to the generated normals. No effect when the Normal Map slot is empty.",
            ["SpookyWater/Sprite Lit Auto Normals|_ZWrite"] = "Write depth so the water clips correctly against the sprite (like Sprite Lit Depth Write). Use with an Alpha Clip Threshold around 0.5, or soft edges will hide the water behind them.",
            ["_AlphaClipThreshold"] = "Pixels fainter than this are thrown away (they neither draw nor hide the water under them).",
            ["_Breakup"] = "Crown: how strongly the sheet tears into holes and a ragged edge as it ages.",
            ["_Clarity"] = "How much of what's behind the water shows through. 0 = solid water color, 1 = fully clear.",
            ["_ClarityDepthLoss"] = "How much of that clarity is lost going down to the Depth Gradient Range. 1 = can't see through at all at the bottom.",
            ["_Color"] = "Multiplied over the texture.",
            ["_ColorFadePower"] = "Curve of the Near-to-Horizon blend. Higher = keeps the near color longer.",
            ["_ContactBreak"] = "How far each stroke shifts its bit of reflection up or down, in rows. Cuts thin gaps into the reflection.",
            ["_ContactDriftSpeed"] = "World units per second the wavelet rows travel outward from an object's waterline (toward the camera), fading as they leave the reach. 0 = they only lap in place.",
            ["_ContactFarBoost"] = "Distant wavelets are more pronounced, as on a real lake seen at a grazing angle: at Far Boost Distance and beyond, the band is this many times taller, denser and more distorting. 1 = same everywhere.",
            ["_ContactFarDistance"] = "World units from the camera (along Z) at which Far Boost reaches full strength, ramping up from 1 at the camera.",
            ["_ContactGlintChance"] = "Fraction of strokes that catch the sky as a pale glint.",
            ["_ContactGlintColor"] = "Color of strokes catching the sky; alpha = how strongly.",
            ["_ContactLapAmount"] = "How far the rows lap toward and away from the waterline, in rows.",
            ["_ContactLineAmount"] = "Tiny waves lapping at the base of anything cutting into the water - reflected sprites crossing the waterline (WaterReflectable), WaterRippleObstacles and, by Shore Amount, the banks. Seen from afar they're short, thin horizontal strokes that smear and shift the reflection. 0 = off.",
            ["_ContactLineReach"] = "How many world units in front of an object's waterline the wavelets reach (toward the camera), thinning out over that distance.",
            ["_ContactLineScale"] = "Rows of wavelets per world unit, nearest set. Farther away, rows too thin to see hand over to wider-spaced ones, so their spacing reads as distance.",
            ["_ContactLineSkew"] = "How far each stroke shifts its bit of reflection sideways, in stroke lengths.",
            ["_ContactLineSpeed"] = "Lapping cycles per second.",
            ["_ContactLineSpread"] = "How far past an object's sides they spread, in world units, at the end of the reach (narrower right at the waterline).",
            ["_ContactPerspective"] = "How much distant strokes shrink with perspective. 0 = every row keeps the same on-screen size (far rows swap to wider-spaced sets), 1 = true world size (distant rows get tiny and fade out).",
            ["_ContactReachPixels"] = "Far away, the reach never covers less than this many pixels of screen (and the side spread grows with it), so distant objects still get a readable band of wavelets.",
            ["_ContactRowShift"] = "How far whole rows near the waterline slide the reflection sideways (in stroke lengths), each its own way - breaks the reflection's base into staggered slices between the strokes too.",
            ["_ContactShoreAmount"] = "Wavelets along the banks too. 0 = only objects standing in the water.",
            ["_ContactSmearLength"] = "How far each stroke smears the reflection along itself, in stroke lengths. Stretches whatever's reflected there sideways past its edges.",
            ["_ContactTwinkleSpeed"] = "How fast single strokes fade in and out, in cycles per second.",
            ["_ContactWaveletBreakup"] = "Paint-like irregularity: wobbly, uneven stroke edges and patchy coverage, so strokes read as loose brush dabs rather than clean dashes. 0 = clean lens shapes.",
            ["_ContactWaveletLength"] = "Longest stroke, in multiples of the row spacing.",
            ["_ContactWaveletSoftness"] = "How soft and rounded strokes are: feathers their edges, rounds their ends into blobs, and spreads their reflection smear vertically too. 0 = thin crisp lens-shaped strokes.",
            ["_ContactWaveletThickness"] = "Half-thickness of a stroke at its middle, as a fraction of the row spacing.",
            ["_DebugClickMarker"] = "Draws a colored circle where the latest ripple was created (blue = pushed up, red = pushed down).",
            ["_DebugClickMarkerFadeTime"] = "Seconds the circle takes to fade out.",
            ["_DebugClickMarkerRadius"] = "Radius of that circle, in world units.",
            ["_DebugDistortionMask"] = "Shows which pixels behind the glass are kept sharp by the Distorted Sorting Layers setting: pink = sharp, teal = bent. Dark blue = the mask isn't running (every layer ticked, or the mask shader is missing).",
            ["_PocketStencilComp"] = "Internal. How the front water reacts to WaterPocket shapes: 6 (Not Equal, the default) hides it inside every pocket so the pocket's air shows through; the pocket's own copy of the material uses 3 (Equal) so its water draws only inside the shape. Set automatically, leave it alone.",
            ["_DebugHeightVis"] = "Replaces shading with the raw up/down displacement (green = up, red = down) to check waves move both ways.",
            ["_DebugRippleVis"] = "Replaces all shading with a heat map of the raw ripple simulation (red = up, blue = down).",
            ["_DebugRippleVisScale"] = "How much the heat map exaggerates small ripple heights.",
            ["_DeepColor"] = "Water color at the far edge of the surface. Shallow blends into Deep going back into the scene.",
            ["_DepthCutoff"] = "Alpha below which a depth-writing particle's pixels are discarded.",
            ["_DepthFogColor"] = "Color the water fades into with distance (world Z) - match your scene's fog sprites. Alpha = how thick the fog gets at End Z; alpha 0 turns depth fog off.",
            ["_DepthFogEndZ"] = "World Z where the fog reaches full strength (the color's alpha).",
            ["_DepthFogPower"] = "Shape of the fade between Start and End Z. 1 = even; higher = stays clear longer, then thickens quickly.",
            ["_DepthFogReflection"] = "How much the depth fog (and the horizon's haze) also covers reflected sprites. They're already painted with their own distance haze, so 1 washes far reflections out to flat fog color; lower keeps far water reflective. The water's own color and the reflected sky always get the full fog.",
            ["_DepthFogStartZ"] = "World Z where the water starts fading into the fog color.",
            ["_DepthGradientPower"] = "Curve of the Shallow-to-Deep blend. 1 = even; higher = stays shallow-colored longer, then turns deep near the back.",
            ["_DisplacementFalloffPower"] = "How quickly the up/down movement settles to flat going toward the horizon. Higher = flattens sooner.",
            ["_DistanceFalloffPower"] = "How the up/down movement fades toward the far edge of the water. 1 = linear fade; higher = movement dies out sooner behind the front edge; lower = stays strong further back.",
            ["_DistortionDir"] = "Direction (xy) of a slow back-and-forth sway added on top of the wobble. It sways in place, it doesn't scroll away. Scaled by Distortion Strength.",
            ["_DistortionRipple"] = "How much ripples passing along the waterline bend what is behind the glass under them (they bulge it up and sideways). Independent of Distortion Strength; 0 = off.",
            ["_DistortionRippleDepth"] = "How far below the surface the ripple bending reaches before fading out, in world units.",
            ["_DistortionSurfaceCalm"] = "Depth below the waterline, in world units, over which all distortion fades in. Right at the surface nothing is bent, so a character or floating object half in the water doesn't look broken at the waterline. 0 = bend all the way up to the surface.",
            ["_DistortionScale"] = "Size of the wobble of things seen through the water. Higher = tighter wobble; 6 gives blobs about one unit across.",
            ["_DistortionSpeed"] = "How fast the wobble boils and sways.",
            ["_DistortionStrength"] = "How far things seen through the water get pushed around by the wobble (fraction of the screen). 0.003-0.01 reads as water; higher looks like heat haze. 0 = off.",
            ["_DstBlend"] = "Leave at One for both Additive and Screen.",
            ["_EdgeFadeStart"] = "Where the mesh starts fully fading into Horizon Color, as a fraction toward its far edge, hiding the edge itself.",
            ["_EdgeFalloffWidth"] = "Width of the fade at the water's left and right ends (fraction of the level width), so waves don't tear away from the banks.",
            ["_FillOpacity"] = "Bubble fill opacity inside the rim; for Crown, how see-through the middle of the sheet is.",
            ["_FogAmount"] = "Fraction of the water's depth fog it gets (it's on the water, so normally all of it).",
            ["_Glint"] = "Brightness of the specular glint (droplets and bubbles).",
            ["_GlowColor"] = "Color of the water's own light (HDR). Push it above 1 with the HDR intensity slider, or raise Glow Intensity, to make URP Bloom spill a soft halo around the water - add a Volume with Bloom (threshold about 1) for that.",
            ["_GlowDeepAmount"] = "How much of the glow is left deep down, far below the surface. 0 = only a band under the surface glows; 1 = the whole body glows evenly.",
            ["_GlowDepth"] = "How far below the surface the glow fades, in world units (to about a third at this depth).",
            ["_GlowFollowColor"] = "0 = a flat glow of Glow Color; 1 = the glow is the water's own shaded color tinted by Glow Color, so reflections, ripples, foam and depth fog stay visible in it (and fogged far water glows less).",
            ["_GlowIntensity"] = "Light the water gives off by itself, added after 2D lighting so it stays bright in a dark scene. Above about 1 it feeds URP Bloom. 0 = off. On the Water Horizon it is copied from the Water Surface material.",
            ["_GlowRippleBoost"] = "Extra glow on the sides of ripples facing the camera, so ripple rings shine. 0 = even glow.",
            ["_GlowSelfLit"] = "How much the water ignores 2D lights: 0 = lit normally by Light 2D, 1 = full color even with no light. Only while Glow Intensity is above 0.",
            ["_GlowUnderside"] = "Glow multiplier when the camera is underwater and sees the surface from below.",
            ["_HazeDistance"] = "World units past the seam until the water is about two-thirds Horizon Color (aerial haze).",
            ["_HorizonColor"] = "Color the water and haze fade to at the horizon - match your sky/fog.",
            ["_MainTex"] = "Optional texture multiplied over the final color, mapped by the mesh's own UVs. Leave white for no effect.",
            ["_MaskTex"] = "Optional 2D-lighting mask, taken from the sprite's Secondary Textures.",
            ["_MistAmount"] = "How misty the water is: a glowing haze over everything seen through the water, lit from above, that WaterLightShaft beams blend into. 0 = clear.",
            ["_MistBreakup"] = "Slowly drifting cloudiness in the mist. 0 = an even veil.",
            ["_MistColor"] = "Mist color just under the surface, where it's lit from above. HDR, so it can glow.",
            ["_MistDeepColor"] = "Mist color deep down, away from the light.",
            ["_MistDepthThickening"] = "0 = equally thick everywhere; 1 = thin just under the surface, thickest deep down.",
            ["_MistLightDepth"] = "How far below the surface (world units) the lit mist fades into the deep color.",
            ["_MistScale"] = "Mist clouds per world unit. Smaller = bigger, softer clouds.",
            ["_MistSpeed"] = "How fast the mist drifts, world units per second.",
            ["_NearColor"] = "Base color at the seam with the painted surface - match the surface material's Deep Color.",
            ["_NormalMap"] = "Optional normal map for 2D lights, taken from the sprite's Secondary Textures.",
            ["_NormalStrength"] = "How steep the wave/ripple slopes look in the lighting and reflection wobble. Doesn't change how high the surface actually moves.",
            ["_ReflectionBlurSize"] = "Blur radius of the reflection (fraction of the screen). 0 = sharp mirror.",
            ["_ReflectionDistortion"] = "How much ripples and wind waves (the builder's Wind settings) wobble the reflection. The horizon water copies this value from the surface material.",
            ["_ReflectionFresnelPower"] = "How quickly the strength moves from Steep to Grazing as the view angle flattens. Higher = only the far, flattest parts get the full reflection.",
            ["_ReflectionMaxStrength"] = "Reflection strength when looking across the water at a low angle (far away). Real water is most mirror-like here.",
            ["_ReflectionMinStrength"] = "Reflection strength when looking steeply down at the water (close to the camera).",
            ["_ReflectionOffset"] = "Screen-space nudge for the reflection (xy). Fine-tuning only - should stay near zero.",
            ["_ReflectionSkyColor"] = "Color reflected wherever no reflectable sprite covers - the sky a real lake would mirror. Match your sky.",
            ["_ReflectionTint"] = "Multiplied into the reflected image. White = reflected colors as they are.",
            ["_RimStrength"] = "Soft brightening where you look across the water at a low angle, tinted by Shallow Color.",
            ["_RimWidth"] = "Bubble/ring rim width, as a fraction of the particle's radius.",
            ["_RippleHeightOffset"] = "Constant height added to the ripple displacement. Normally 0.",
            ["_RippleHighlightColor"] = "Color of the light bands drawn on ripple rings (the sides facing the camera); the far sides darken.",
            ["_RippleHighlightGain"] = "How sensitive the bands are to ripple slope. Higher = faint ripples show up more strongly.",
            ["_RippleHighlightOpacity"] = "Overall opacity of the ripple light/dark bands. 0 = ripples only show through the reflection wobble.",
            ["_RippleScale"] = "How strongly the interactive ripple simulation (clicks, bodies hitting the water) moves the surface up and down. 0 = ripples don't displace this mesh.",
            ["_ShallowColor"] = "Water color at the front edge of the surface.",
            ["_Shape"] = "What each particle quad draws.",
            ["_ShimmerColor"] = "Color of the glints.",
            ["_ShimmerScale"] = "Size of the glint pattern. Higher = finer glints.",
            ["_ShimmerStrength"] = "Thin broken horizontal glints on the surface. 0 = off.",
            ["_Softness"] = "Edge softness, as a fraction of the particle's radius.",
            ["_SrcBlend"] = "Additive (One / One) piles light up and blooms with HDR; Screen (One Minus Dst Color / One) never blows out to white.",
            ["_SurfaceGlowColor"] = "Glow added just under the surface - light scattered in the top of the water. Black = off.",
            ["_SurfaceGlowDepth"] = "How deep the surface glow reaches, in world units.",
            ["_TiltShading"] = "How much it brightens/darkens as ripples tilt it toward/away from the camera - makes rocking read. 0 = flat shading.",
            ["_TransmissionTint"] = "Color that things seen through the water take on.",
            ["_UnderwaterBlurSize"] = "Underwater view: blur of the see-through image (fraction of the screen).",
            ["_UnderwaterColor"] = "Underwater view: color outside Snell's window, where real water stops letting light through and mirrors the underwater instead.",
            ["_UnderwaterFadeAmount"] = "Camera underwater only: how far the underside fades out by Underwater Fade Distance. 0 = stays fully opaque (a hard edge wherever background art cuts it off), 1 = vanishes into the scenery behind it.",
            ["_UnderwaterFadeDistance"] = "Camera underwater only: world units past the seam over which the far underside fades out, so it dissolves into the scenery behind it instead of ending in a hard edge where background art standing in the water cuts it off. 0 = never fades.",
            ["_UnderwaterFogDistance"] = "Camera underwater only: world units past the seam until the view is about two-thirds murk (Outside-Window Color, blended toward Underwater Haze Color).",
            ["_UnderwaterHazeColor"] = "Camera underwater only: color the far underside fades to. Alpha = how much it replaces the Water Surface's Outside-Window Color (0 = dark murk, 1 = this color). Match it to the haze painted on far background art standing in the water, or the underside ends in a visible color edge where that art cuts it. Also used by fog sprites with Match Water Murk.",
            ["_UnderwaterRefraction"] = "Underwater view: how much ripples bend the view through the surface.",
            ["_UnderwaterRippleHighlightGain"] = "Underwater view: ripple band sensitivity (uses Ripple Highlight Color). Usually higher than the top side, since you see it through the tinted front glass.",
            ["_UnderwaterRippleHighlightOpacity"] = "Underwater view: opacity of the ripple bands.",
            ["_UnderwaterRippleRefraction"] = "Underwater view: how much ripple rings bend the view through the surface.",
            ["_UnderwaterSkyColor"] = "Underwater view: color seen through the surface wherever nothing above covers (the sky from below).",
            ["_UnderwaterTint"] = "Only used when the camera is underwater. Multiplied into the view of everything above the water, seen through the surface from below.",
            ["_UnderwaterWindowAngle"] = "Underwater view: half-angle in degrees of Snell's window, the cone you can see out of. 90 = see through everywhere; 48.6 = real water, which hides almost everything from a side-on 2.5D camera.",
            ["_UnderwaterWindowSoftness"] = "Underwater view: softness of the window's edge.",
            ["_VerticalFalloffPower"] = "How the waterline's up/down movement fades toward the bottom of each tile. Higher = only the top rows move, the rest stays rigid.",
            ["_WaterClipMode"] = "Which side of the waterline this fog shows on. Normally set per sprite by its WaterFogSprite component, which overrides this.",
            ["_WaterClipSoftness"] = "How softly the fog fades out at the waterline, in world units (fades on the kept side only).",
            ["_WaterClipY"] = "World Y of the waterline the fog is cut at. Normally set by WaterFogSprite.",
            ["_WaterDepthRange"] = "How many world units below the surface it takes to reach Deep Color.",
            ["_WaterMurkFromZ"] = "World Z the murk starts from - the next nearer murk sheet, so stacked sheets add up instead of doubling. Normally set by WaterFogSprite.",
            ["_WaterMurkMatch"] = "1 = ignore the sprite's color and take the water's underwater murk instead (color and density from distance). Normally set by WaterFogSprite's Match Water Murk.",
            ["_WaterlineBrightnessVariation"] = "How much the line's brightness and glow vary along its length. 0 = uniform glow.",
            ["_WaterlineCrestBrightness"] = "Extra brightness on ripple crests (above 1 = HDR, picked up by Bloom).",
            ["_WaterlineFoamWidth"] = "Total thickness of the foam line, in tile UV units.",
            ["_WaterlineGlow"] = "How far the soft glow bleeds down below the foam line, in tile UV units.",
            ["_WaterlineGlowStrength"] = "Brightness of that glow.",
            ["_WaterlinePosition"] = "Where the foam line sits on a waterline tile, in tile UV (1 = the very top edge).",
            ["_WaterlineRippleResponse"] = "How much ripples shape the line: crests and steep slopes thicken and brighten it, troughs thin it. 0 = off.",
            ["_WaterlineThicknessVariation"] = "How much the foam line swells and pinches along its length. 0 = even thickness everywhere.",
            ["_WaterlineVariationScale"] = "Size of those swells. Higher = more, shorter swells per world unit.",
            ["_WaterlineVariationSpeed"] = "How fast the swells drift and shimmer along the line.",
            ["_WetEdgeDarken"] = "How dark that wet edge gets.",
            ["_WetEdgeWidth"] = "Darkens the edge where it sits in the water (by distance from the texture's alpha edge, in UV). 0 = off.",
            ["_ZTest"] = "Depth test. Always for spray thrown up above the water: the displaced surface (a splash's own rebound mound, or the surface seen from below) writes depth and would hide it, while drawing it anyway lets the water's front face show it through the water and the camera see it from below.",
            ["_ZWrite"] = "Write depth so water drawn later behind the particle (e.g. the surface's underside) can't cover it. Pixels fainter than the cutoff are discarded.",
            ["SpookyWater/Light Shaft|_BandTex"] = "Default mask for Band Layer A (flat white vertical bands on black) on shafts whose component leaves that layer's texture empty. Only the layout is read; color comes from the component. Scrolled sideways and stretched over the shaft's height. Must tile left-right; import with Wrap U = Repeat, Wrap V = Clamp and mipmaps on (the blur reads blurred mips).",
            ["SpookyWater/Light Shaft|_BandTex2"] = "Default mask for Band Layer B, the second band layer that is combined with A as a union (light wherever either has a band). Same format as Band Texture A; defaults to LightShaftBandsFine, a sparser set of thinner bands so the union keeps dark gaps.",
            ["SpookyWater/Light Shaft|_ZTest"] = "LessEqual lets sprites closer to the camera (that write depth) cover the shaft. Always = drawn over everything in its sorting slot.",
            ["SpookyWater/Sprite Fog|_MainTex"] = "The sprite's texture. Set automatically by the Sprite Renderer.",
            ["SpookyWater/Sprite Lit Depth Write|_AlphaClipThreshold"] = "Pixels fainter than this are thrown away completely - they neither draw nor block the water behind them. Higher = harder, more cut-out edges; lower = softer edges, but faint fringes can hide the water behind.",
            ["SpookyWater/Sprite Lit Depth Write|_MainTex"] = "The sprite's texture. Set automatically by the Sprite Renderer.",
            ["SpookyWater/Sprite Lit Auto Normals|_MainTex"] = "The sprite's texture. Set automatically by the Sprite Renderer. Its alpha and brightness are read around each pixel to build the normals.",
            ["SpookyWater/Sprite Lit Auto Normals|_AlphaClipThreshold"] = "Pixels fainter than this are thrown away. 0 = keep everything (like Sprite-Lit-Default). Raise to about 0.5 together with Depth Write so the water clips against the sprite.",
            ["SpookyWater/Sprite Lit Auto Normals|_MaskTex"] = "Optional Light 2D mask texture (per-channel blend style masks), as in Sprite-Lit-Default.",
            ["SpookyWater/Sprite Lit Auto Normals|_NormalMap"] = "Optional hand-made normal map, added on top of the generated normals. Leave empty for sprites without one.",
            ["SpookyWater/Surface Debris|_MainTex"] = "The debris texture. WaterSurfaceDebris sets it from its sprite.",
            ["SpookyWater/Water Front|_DebugRippleVis"] = "Replaces all shading with a heat map of the raw ripple simulation at the waterline (red = up, blue = down).",
            ["SpookyWater/Water Front|_DeepColor"] = "Water color at the bottom of the depth range.",
            ["SpookyWater/Water Front|_DepthGradientPower"] = "Curve of the Shallow-to-Deep blend. 1 = even; higher = stays shallow-colored longer, then darkens near the bottom.",
            ["SpookyWater/Water Front|_FoamColor"] = "Color of the waterline foam line and its glow.",
            ["SpookyWater/Water Front|_RippleScale"] = "How strongly the interactive ripple simulation moves the waterline up and down. Keep equal to the surface (WaterSurface) material so the two edges meet.",
            ["SpookyWater/Water Front|_ShallowColor"] = "Water color just below the surface.",
            ["SpookyWater/Water Horizon|_EdgeFalloffWidth"] = "Width of the fade at the left and right ends (fraction of the level width).",
            ["SpookyWater/Water Horizon|_MainTex"] = "Optional texture multiplied over the color, mapped by the mesh's UVs. Leave white for no effect.",
            ["SpookyWater/Water Horizon|_RippleScale"] = "How strongly interactive ripples move this far water up and down (they rarely reach this far).",
            ["SpookyWater/Water Particle|_Color"] = "Multiplies every particle's color.",
            ["SpookyWater/Water Particle|_FoamColor"] = "Color of the bright edges: droplet rims, the crown's foam lip.",
        };
    }
}
