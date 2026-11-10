# SpookyWater

Interactive water for 2D and 2.5D games in Unity (URP 2D Renderer). Paintable with Tilemaps. This package includes:

- a reflective water **surface**, displaced by a real GPU ripple simulation that can bounce off shores and obstacles,
- a see-through **front cross-section** with distortion, depth tint, glow, and waterline,
- automatic **ripples and splashes** whenever any `Rigidbody2D` crosses the waterline,
- splash **particles** (crowns, strands, droplets, mist, landing rings), swim spray, dash trails, bubbles,
- **floating** platforms and objects with per-slice buoyancy, and flat **debris** that bob and drift with ripples,
- planar **reflections**, and underwater view through the surface,
- **fog sheets** that can stay above or below the water, and soft underwater **light shafts**,
- a static scripting API (`SpookyWater.Water`) to make ripples, splashes and bubbles from gameplay code.

Everything is lit by 2D lights (built on Sprite Lit).

Namespace: `SpookyWater`. Package folder: `Assets/SpookyWater/` (or `Packages/com.daothienphu.spookywater/` when installed through the Package Manager).

## Previews

Preview GIFs (open on Gyazo):

- [Floating objects and platforms](https://gyazo.com/dd0b6d4e475886175bee571837a24358)
- [Dashing and lighting](https://gyazo.com/6398bb9b131e561de1563919877d55b0)
- [Leaping and splashes](https://gyazo.com/e6e7e4ca95e5f6a0dfaa65c87629b31a)
- [Water pocket](https://gyazo.com/a35ef5298606d65eb8347d27d3c4bf15)

## Contents

- [Previews](#previews)
- [Requirements](#requirements)
- [Installation](#installation)
- [Project Setup](#project-setup)
- [Quick start (5 minutes)](#quick-start-5-minutes)
- [Core concepts](#core-concepts)
- [Painting a level, step by step](#painting-a-level-step-by-step)
- [Which component do I need?](#which-component-do-i-need)
- [Use cases](#use-cases)
- [Component reference](#component-reference)
- [Scripting API](#scripting-api)
- [Layers and sorting layers](#layers-and-sorting-layers)
- [Settings asset](#settings-asset)
- [Material settings](#material-settings)
- [Menus reference](#menus-reference)
- [Samples](#samples)
- [Performance and tuning](#performance-and-tuning)
- [Limits and caps](#limits-and-caps)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)
- [Credits](#credits)

## Requirements

| Requirement | Notes |
|---|---|
| Unity **6000.3** or newer | Developed on 6000.3.9f1. |
| **Universal Render Pipeline 17.3+** with a **2D Renderer** | The water shaders are built on Sprite Lit; only the 2D Renderer drives 2D lights. |
| A **perspective** camera the water can follow | The camera tagged `MainCamera` by default, or any camera you hand over with a `WaterCameraSource` component or from code (see [Cameras from your own camera system](#cameras-from-your-own-camera-system)). |
| A **Global Light 2D** in the scene | Without any 2D light, lit water renders black. |
| 2D Tilemap Editor (`com.unity.2d.tilemap`) | Painting the water and the tile palette. |
| 2D Animation (`com.unity.2d.animation`) and 2D PSD Importer (`com.unity.2d.psdimporter`) | Needed by the demo character (Sara), a rigged `.psb`. Listed as package dependencies, so they install automatically. If you add them by hand, install **2D Animation before (or together with) the PSD Importer**, or reimport `Sara.psb` afterwards; otherwise the sprites import without their bones and the character does not animate. |
| *Optional:* Input System (`com.unity.inputsystem`) | Mouse ripples and the sample controller work with either the old Input Manager or the Input System. |
| *Optional:* 2D Sprite Shape (`com.unity.2d.spriteshape`) | Lets `WaterReflectable` mirror Sprite Shapes. |

The optional packages are detected automatically through version defines (`SPOOKYWATER_INPUTSYSTEM`, `SPOOKYWATER_SPRITESHAPE`); nothing has to be configured.

## Installation

Pick one.

### A. Package Manager, from the Git URL (recommended)

1. **Window > Package Manager > + > Add package from git URL...**
2. Enter:

   ```
   https://github.com/daothienphu/SpookyWater_public.git?path=/Assets/SpookyWater
   ```

   To pin a version, append a tag or commit: `...?path=/Assets/SpookyWater#v1.0.0`.

Or add it to `Packages/manifest.json` directly:

```json
{
  "dependencies": {
    "com.daothienphu.spookywater": "https://github.com/daothienphu/SpookyWater_public.git?path=/Assets/SpookyWater"
  }
}
```

A Git package is **read-only** (it lives in `Library/PackageCache`). That has two consequences, covered where they matter below:

- Duplicate the package materials into your own `Assets/` folder before editing them (you will want per-scene copies anyway).
- The settings asset ships with the package. To change layer names, create your own (**Assets > Create > SpookyWater > Settings**) inside any `Resources` folder of your project and delete nothing.

### B. `.unitypackage`

Download a `.unitypackage` from the GitHub Releases page and import it (**Assets > Import Package > Custom Package...**). Everything lands in `Assets/` and is fully editable.

- `SpookyWater.unitypackage`: the package alone, in `Assets/SpookyWater/`.
- `SpookyWater_WithDemo.unitypackage`: the package plus the demo scene in `Assets/SpookyWaterDemo/`. The demo needs **2D Animation** and **2D PSD Importer** (both part of the default 2D feature set of a new 2D URP project) for its character. On import it adds the sorting layers it uses; if your project already has some of them under other IDs, opening the demo scene remaps it onto yours (or run **Tools > SpookyWater > Demo > Fix Demo Sorting Layers**).

A `.unitypackage` does not install the URP or Tilemap packages for you; make sure they are in the project.

### C. Copy the folder

Copy `Assets/SpookyWater/` (with all `.meta` files) into your project's `Assets/`. Equivalent to B.

After installing, continue with [Project Setup](#project-setup).

## Project Setup

The water needs a few project settings that a fresh project does not have. One window checks and fixes them all.

1. Open **Tools > SpookyWater > Project Setup**. The window also opens once by itself on first import if something is missing (turn that off with the checkbox in the window).
2. Click **Fix All Project Items**.
3. Open the scene you want water in, and click **Fix All Scene Items**.

Each requirement shows a status, what it does, why it is needed and a **Fix** button where it can be automated.

| Requirement | Scope | What the fix does / why it is needed |
|---|---|---|
| Universal Render Pipeline with a 2D Renderer | Project | **No automatic fix.** Create a URP asset with a 2D Renderer (**Assets > Create > Rendering > URP Asset (with 2D Renderer)**) and assign it in **Project Settings > Graphics** and **Quality**. The water shaders are Sprite Lit based. |
| SpookyWater settings asset | Project | Creates `<package folder>/Resources/SpookyWaterSettings.asset` (already shipped). Holds layer names and sorting orders. Without it built-in defaults are used. |
| Reflection physics layer `SpookyWater_Reflections` | Project | Writes the name into the first free user layer. `WaterReflectable` puts hidden copies of your sprites on it; only the reflection camera renders it. |
| Water sorting layers in the right order | Project | Adds `SpookyWater_Surface` and `SpookyWater_Front`, ordered **after** `Default` (and `Player` if present). |
| Camera Sorting Layer Texture on the 2D Renderer | Project | Enables it on every 2D Renderer and binds it to `SpookyWater_Surface`. The front glass reads this texture to refract everything drawn before the water. |
| Water camera | Scene | The reflection camera, ripple window, mouse ripples and horizon follow `Water.Camera`. Fix tags the scene's only camera `MainCamera` (or adds a `WaterCameraSource` when the MainCamera fallback is off). A scene with no camera is only Info, since your camera system may spawn one at runtime; cameras that the water cannot pick up are a Warning. |
| Global Light 2D | Scene | Creates a "Global Light 2D". A Global Volume (bloom) is optional. |
| Water level with all materials assigned | Scene | Exactly one `WaterTilemapBuilder`; assigns the five default materials to empty slots. |
| Splash effects (optional) | Scene | One `WaterSplashEffects` for splashes, spray and bubbles. |

The same checks are reachable from **Project Settings > SpookyWater** and from a warning box with an **Open Project Setup** button on the builder inspector.

> If water rendering ever looks broken while the scripts and materials look correct, open Project Setup first.

## Quick start (5 minutes)

1. Install the package and run [Project Setup](#project-setup) (both Fix All buttons).
2. Make sure your camera is **Perspective** and tagged `MainCamera` (or see [Cameras from your own camera system](#cameras-from-your-own-camera-system)). A good start: a few units above the water and about 10 units in front of it (for water at the origin, position `(4, 3, -10)`), tilted down about 10 degrees (rotation `(10, 0, 0)`), field of view 40.
3. **GameObject > SpookyWater > Water**. This creates:
   - a **Water** object with a `Grid` (cell size 1, at world Z = 0) and a `WaterTilemapBuilder` with the default materials,
   - a child **Front Tilemap** with an 8 x 3 painted pond cross-section,
   - a child **Surface Tilemap** (placed 3 units higher in the Scene view so the two don't overlap) with an 8 x 4 painted surface,
   - a `WaterSplashEffects` if the scene had none.
4. Press **Generate Water** on the builder to preview in Edit mode, or just press **Play** (the builder regenerates on Start).
5. Drop something in: **GameObject > 2D Object > Sprites > Square**, add a `Rigidbody2D` and a `BoxCollider2D`, place it above the pond and press Play. It ripples and splashes when it hits the water.
6. Optional extras in one click each:
   - **GameObject > SpookyWater > Mouse Ripples**: click the water to ripple it.
   - **GameObject > SpookyWater > Floating Platform**: a raft you can land on.
   - **GameObject > SpookyWater > Floating Debris**: a lily pad.
   - Select your scenery and **GameObject > SpookyWater > Add To Selection > Reflection**: it appears in the water.

## Core concepts

### The two tilemaps

The water is described by two Tilemaps that share one Grid.

```
 Front Tilemap  (what you see head-on: X right, Y up)         Surface Tilemap (a top-down map: X right, rows = depth)

   y                                                          row 3  ~ ~ ~ ~ ~ ~ ~ ~     <- far edge (largest Z)
   0   B B B B B B B B   <- top row: becomes the waterline    row 2  ~ ~ ~ ~ ~ ~ ~ ~
  -1   B B B B B B B B                                        row 1  ~ ~ ~ ~ ~ ~ ~ ~
  -2   B B B B B B B B                                        row 0  ~ ~ ~ ~ ~ ~ ~ ~     <- near edge, world Z = 0
       x=0 ...       7                                               x=0 ...       7

   B = Water Body tile                                        ~ = Water Surface tile
```

- **Front Tilemap** (required): the vertical cross-section of the water facing the camera. Paint the **Water Body** tile on every cell that is water.
- **Surface Tilemap** (optional): a *map* of the flat top of the water, seen from above. It is painted in the Scene view like any tilemap, but the builder reads its rows as **depth**: the lowest painted row is the near edge (world Z = 0, where it meets the front glass), each row above it is one unit further from the camera. Its columns must line up with the front tilemap's columns (same X). Where it sits in the Scene view does not matter; only the cell indices are read.
- Leave the Surface Tilemap empty for an **underwater-only** level (no top surface, no reflection).

When the water is generated the painted tiles are hidden and replaced by the generated meshes. **Clear** removes the meshes and shows the tiles again.

### The three generated kinds

| Kind | Plane | Painted? | What it is |
|---|---|---|---|
| **Surface** | XZ (lying flat) | Yes, Water Surface tile | The reflective top. Flat as a mirror at rest, displaced by ripples only. Shows planar reflections. Has a 3D trigger collider (used for mouse ripples). |
| **Line** | XY (facing camera) | **No, derived** | Every front cell with no water directly above it: the animated waterline strip. Ripple-displaced, with a foam band. Carries a thin 2D trigger band that turns bodies crossing it into ripples and splashes. |
| **Body** | XY | Yes, Water Body tile | Fully submerged front "glass". Distortion and depth tint applied to sprites behind it. No displacement, no collider. |

Because Line is derived, **a hole in the Body paint becomes an underwater air pocket** with its own waterline.

Beyond the far edge of the painted surface the builder adds a cheap fan-shaped **horizon** mesh so a perspective camera sees water to the distance (optional; leave the Horizon material empty to disable).

### One level per scene

A scene has one `WaterTilemapBuilder`. All ponds are painted on the same two tilemaps; unpainted columns are shores. Everything the builder generates shares:

- **one ripple simulation** (`WaterField`) addressed by world position, so ripples cross tile boundaries seamlessly and bounce off shores,
- **one reflection camera** (`WaterReflectionRenderer`) mirroring the main camera across one water plane,
- **one set of shader globals**, which lets debris, fog, light shafts and particles know where the water is.

### Generated objects

After **Generate Water** the builder has a child `GeneratedWater` with chunk meshes (one per kind per `Chunk Size` square, so big levels cull per chunk), `WaterSurfaceColliders`, `WaterContactBands`, the `WaterField` and the `WaterReflectionRenderer`. Do not edit them by hand: they are rebuilt on every Generate and on Start. Their settings live on the builder.

### Coordinate conventions

- X is left/right, Y is up, **Z is depth away from the camera**.
- The front tilemap and its Grid sit at **world Z = 0**. That is also the near edge of the surface.
- Draw order comes from **sorting layers**, not Z. Z only drives perspective and parallax. Keep both in the same back-to-front order.

## Painting a level, step by step

1. **Create the water**: **GameObject > SpookyWater > Water** (or add a `Grid` + `WaterTilemapBuilder` yourself and two child Tilemaps, then assign them in the builder's *Tilemaps* group and press **Assign Defaults** under *Materials*).
2. **Create a palette**: **Assets > Create > SpookyWater > Tile Palette** (or the builder's **Create Tile Palette** button). Choose where to save it. It holds the two tiles: **Water Body** and **Water Surface** (assets in `Tiles/`).
3. Open **Window > 2D > Tile Palette** and select the new palette.
4. **Paint the cross-section**: select the **Front Tilemap** as the active tilemap (the builder has a **Select Front Tilemap** button) and paint **Water Body** on every water cell. The top cell of each column becomes the waterline. Columns with no paint are shores/land.
5. **Paint the surface**: select the **Surface Tilemap** (**Select Surface Tilemap**) and paint **Water Surface**. For each column of front water, paint rows starting at the same bottom row index going up; the number of rows is how deep (in Z) the pond reaches. Leave a cell out to make a hole (for a rock or an island). Make some columns shallower than others for an irregular far shore.
6. **Generate**: press **Generate Water**. Repeat painting and generating freely. Press **Clear** to see the tiles again.
7. **Add scenery**: put your ground/bank sprites around the pond on sorting layers *before* `SpookyWater_Surface` (e.g. `Default`), so the front glass refracts what is behind it. See [Layers and sorting layers](#layers-and-sorting-layers).
8. **Make scenery clip against the water** (optional but usually wanted for banks and props that stand in the water): give their sprites the `Materials/Sprites/SpriteLitDepthWrite.mat` material, or run **Tools > SpookyWater > Convert Material To Depth Clipping** on their material.
9. **Play**.

Rules of thumb:

- Surface columns must match front columns (same X). A surface cell above a column with no front water is ignored for height (it borrows the nearest column).
- Every pond should share one surface height; the builder warns if column heights differ by more than half a cell.
- Keep the Grid at Z = 0 and its rotation at zero.

## Which component do I need?

| I want... | Attach / create | On which object | Menu shortcut |
|---|---|---|---|
| Water in my scene | `WaterTilemapBuilder` + `Grid` + 2 Tilemaps | a new "Water" object | **GameObject > SpookyWater > Water** |
| Splashes, spray, bubbles | `WaterSplashEffects` (one per scene) | child of the water | created with **GameObject > SpookyWater > Water** |
| Things to ripple/splash when they fall in | nothing extra: a `Rigidbody2D` + `Collider2D` is enough | the falling object | - |
| A character that swims, with wake/spray/bubbles | `WaterSwimmer` (needs `Rigidbody2D`) | the character | **Add To Selection > Swimmer** |
| A raft you can stand on | `WaterFloater`, Mode = Platform (needs `Rigidbody2D` + `Collider2D`) | the raft | **Floating Platform** / **Add To Selection > Floater (Platform)** |
| A crate/log/barrel that sinks when stood on | `WaterFloater`, Mode = Object | the object | **Floating Object** / **Add To Selection > Floater (Object)** |
| Lily pads, leaves, twigs lying on the surface | `WaterSurfaceDebris` | a new object | **Floating Debris** |
| Ripples to bounce off a rock or post | `WaterRippleObstacle` | the rock/post | **Ripple Obstacle** / **Add To Selection > Ripple Obstacle** |
| Something to appear in the reflection | `WaterReflectable` | the object, or a parent to cover children | **Add To Selection > Reflection** |
| Click the water to ripple it | `WaterMouseRipples` | the camera, or any object | **Mouse Ripples** |
| The water to follow a camera that is not tagged MainCamera, or is spawned at runtime | `WaterCameraSource`, or `Water.CameraResolver` from code | the camera / its prefab | **Add To Selection > Camera Source** |
| Fog only above / only below the water | `WaterFogSprite` + `Fog.mat` on a SpriteRenderer | the fog sprite | **Fog Sheet** |
| God rays / underwater light | `WaterLightShaft` | a new object | **Light Shaft** |
| An object that shadows the god rays | `WaterLightShaftOccluder` | the log/rock | **Add To Selection > Light Shaft Occluder** |
| A glow around the character that lights its surroundings | `WaterGlowLight` (adds a Point `Light2D`) | a child of the character | **Add To Selection > Glow Light** |
| Sprites shaded with relief by that light, without normal maps | `SpriteLitAutoNormals.mat` material | the sprites / tilemaps | **Add To Selection > Auto Normals Material** |
| A sprite that clips correctly into the water | `SpriteLitDepthWrite.mat` material | the sprite | **Tools > SpookyWater > Convert Material To Depth Clipping** |
| Ripples/splashes from code | `SpookyWater.Water` static API | your script | - |

## Use cases

### Objects falling in, jumping out

**Attach:** nothing. **Requires:** `Rigidbody2D` + any `Collider2D` on a layer included in the builder's **Contact Layers**; a `WaterSplashEffects` in the scene for particles.

The builder creates thin trigger bands (`WaterContactBands`) along the top of every Line run. A body overlapping a band is judged by **how much of it is under water**, not by a single trigger enter, so floating bodies that never leave the band don't spam ripples:

- Falling in: once the body is at most *Enter Arm Fraction* (20%) submerged it is "armed"; when it passes *Enter Splash Fraction* (25%) while moving down, it ripples and splashes. The splash size follows the impact speed (*Reference Impact Speed* = strength 1).
- Jumping out: armed when at least *Exit Arm Fraction* (85%) submerged, fires when it rises past *Exit Splash Fraction* (75%). Exit splashes are scaled by *Exit Strength Scale*.
- A body that fell in trails bubbles down its path for a moment (*Plunge Bubbles*).

Every splash in or out also pushes a ripple whose strength grows with the body's vertical speed (*Splash Ripple Per Speed* 0.18 per u/s, capped at *Max Splash Ripple* 3, *Splash Ripple Radius* 2x the Impact Radius), so a fast dive or burst out moves the water far more than a slow dip.

Tune: builder > *Interaction* > **Contact Layers**, **Contact Ripple** (strength range, splash ripple and the four fractions); `WaterSplashEffects` > *Impacts*.

To exclude something (e.g. projectiles that should pass silently), put it on a physics layer that is not in **Contact Layers**.

### A swimming player character

**Attach:** `WaterSwimmer` (requires `Rigidbody2D`). **Requires:** a `WaterSplashEffects` for spray/bubbles.

1. Select the character, **GameObject > SpookyWater > Add To Selection > Swimmer**.
2. Set *Water > Float Height* to the height above the character's feet that floats level with the surface (roughly chest height), and *Swim Speed* / *Fast Speed* to your controller's swim and dash speeds.
3. From your controller, read the swimmer's state and drive its effects:

```csharp
using SpookyWater;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(WaterSwimmer))]
public class MySwimController : MonoBehaviour
{
    [SerializeField] float swimSpeed = 3f, dashSpeed = 9f, buoyancy = 4f;
    Rigidbody2D body;
    WaterSwimmer swimmer;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        swimmer = GetComponent<WaterSwimmer>();
        swimmer.SetSpeedRange(swimSpeed, dashSpeed);
        swimmer.OnEnterWater.AddListener(() => body.gravityScale = 0f);
        swimmer.OnExitWater.AddListener(() => body.gravityScale = 1f);
    }

    void FixedUpdate()
    {
        if (!swimmer.InWater) return;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        var velocity = input * swimSpeed;
        velocity.y += swimmer.Depth * buoyancy * (input.y > 0f ? 1f : 0.5f);
        body.linearVelocity = velocity;

        if (input.x != 0f) swimmer.FacingLeft = input.x < 0f;
    }

    public void Dash(float direction)
    {
        swimmer.Dashing = true;
        swimmer.DashBurst(direction);
        body.linearVelocity = new Vector2(direction * dashSpeed, body.linearVelocity.y);
    }

    public void EndDash() => swimmer.Dashing = false;
}
```

What `WaterSwimmer` does for you:

- **State:** `InWater`, `SurfaceY`, `Depth` (how far the float point is below the surface; 0 out of water), `Submerged` (deeper than *Surface Depth*). Events `OnEnterWater` / `OnExitWater`.
- **Wake:** drags a trough through the ripple simulation while swimming near the surface (stronger while `Dashing`).
- **Bow wave:** after swimming steadily one way along the surface, a mound of water builds up behind the character, grows and catches up until it carries them; when they stop, turn, dive or leave the water it rolls on ahead and dies down (faster against a shore). It is drawn by the water shaders, not simulated, so it stays one smooth mound. It must reach the water's front edge (*Bow Wave Depth*) to show on the waterline.
- **Surface spray:** small spray bursts while swimming along the surface, scaling with speed.
- **Dash trail:** above *Swim Speed* a sparse, low trail of droplets left behind (full at *Fast Speed*).
- **Bubbles:** a bubble trail while underwater plus occasional breath bubbles.

Set `EffectsEnabled = false` to silence everything (cutscenes), call `ResetTrail()` after teleporting so no wake is drawn across the jump.

The sample `WaterPlayerController` (**Add Component > SpookyWater > Samples > Water Player Controller**) is a complete land + water platformer controller built on this. It requires `Rigidbody2D`, `CapsuleCollider2D` and `WaterSwimmer`, and copies its own Swim Speed / Dash Speed into the swimmer unless **Drive Swimmer Speeds** is off.

### Floating platforms and objects

**Attach:** `WaterFloater` (requires `Rigidbody2D`; uses a `Collider2D` for its shape). Use a **Dynamic** `Rigidbody2D`.

- **GameObject > SpookyWater > Floating Platform** or **Floating Object** creates a sprite with a `BoxCollider2D`, `Rigidbody2D` and `WaterFloater`.
- Or select an existing object and **Add To Selection > Floater (Platform)** / **Floater (Object)**.

| Mode | Behaviour | Typical use |
|---|---|---|
| **Platform** | Stiff raft. Dips a little when landed on, leans toward whoever stands near an end (up to *Max Tilt*), never lets its top go under (*Min Freeboard*), and is pulled back to where you placed it along X (*Anchor Strength*). | Rafts, lily-pad platforms, docks that bob. |
| **Object** | Sinks by *Ridden Sink Depth* under anything standing on it (at *Sink Speed*), then bobs back up *Resurface Delay* after the rider leaves. Turns back upright (*Upright Strength*). | Crates, logs, barrels, sinking stepping stones. |

Buoyancy is computed per vertical slice (*Slices*), so weight on one end sinks that end. *Float Depth* is the fraction of the height under water when unloaded; *Load Stiffness* is how hard it resists extra load. Floaters also ripple the water as they bob and drag a wake while drifting (*Ripples* group). Falling in or bobbing out splashes through the contact bands like any body.

Place floaters with their bottom near the waterline; they settle on Play.

### Floating debris (lily pads, leaves, twigs)

**Attach:** `WaterSurfaceDebris` (adds `MeshFilter` + `MeshRenderer`). **Requires:** a sprite and a material using `SpookyWater/Surface Debris` (`Materials/Water/SurfaceDebris.mat`).

1. **GameObject > SpookyWater > Floating Debris** (creates a lily pad at Z = 1.5).
2. Replace *Look > Sprite* with any leaf, twig or petal sprite (the sprite is drawn lying flat on the water).
3. Move it anywhere over **Surface** tiles. Its height snaps to the water, in the editor too.

It bobs and tilts with every ripple ring that passes under it and is shoved outward by splashes. It bounces off shores, obstacles and other debris (*Collision Radius*, *Bounciness*). For a lily pad tethered by its stem, set *Anchor > Anchor Strength* > 0 and an *Anchor Slack*.

Typical values: light leaves/twigs *Push Response* 1.5-2; heavy pads 0.5.

Debris is not physics: it does not collide with Rigidbodies. Use a `WaterFloater` for things characters stand on.

### Rocks, posts and pillars that block ripples

**Attach:** `WaterRippleObstacle`.

Add it to the rock/post (**Add To Selection > Ripple Obstacle**). Ripples reflect off its footprint where it cuts the water plane. *Shape*:

- **Auto** (default) derives the footprint from a 3D `SphereCollider`/`BoxCollider`, or from a 2D sprite / `Collider2D` (a strip *Sprite Thickness* deep in Z).
- **Circle** (*Size.x* = diameter) or **Box** (*Size* = X, Z) in the object's local space, scaled by its transform.

*Padding* grows or shrinks the footprint. The obstacle also produces contact wavelets around its base. Up to 32 obstacles are simulated. For a hole in the surface (an island), also leave those Surface cells unpainted.

### Reflections

**Attach:** `WaterReflectable` on any object with a `SpriteRenderer`, `TilemapRenderer` or `SpriteShapeRenderer`, or on a parent (e.g. the Grid of your scenery tilemaps) with *Include Children* on.

- **Add Component > SpookyWater > Water Reflectable**, or **GameObject > SpookyWater > Add To Selection > Reflection** (works on a multi-selection).
- No layer changes: the component makes hidden proxy copies on `SpookyWater_Reflections`, so your objects keep their own layers.
- *Visible From Below* also shows the object through the surface when the camera is underwater.
- Sprite, colour, flip and visibility are copied every frame. If you change sorting, material (hit flash) or draw mode at runtime, enable the matching *Occasional Sync* option or call `SyncAllNow()`.
- Objects whose bases cross the water also get **contact wavelets**: short lapping strokes that smear the reflection at the waterline.

Alternatively, the builder's *Reflection > Reflection Mask* reflects whole physics layers in addition to `WaterReflectable` objects.

**Never** add it to fog sprites (see below).

### Fog above and below the water

**Attach:** `WaterFogSprite` on a `SpriteRenderer` using `Materials/Sprites/Fog.mat` (shader `SpookyWater/Sprite Fog`).

1. **GameObject > SpookyWater > Fog Sheet**.
2. Put it on a fog sorting layer (e.g. `Background_1_Fog`, `Mid_Fog`) and tint it with the sprite colour.
3. Choose *Side*:
   - **Everywhere**: plain fog.
   - **Above Water Only**: mist that must not show through the front glass.
   - **Below Water Only**: murk that must not rise into the air. Enable *Match Water Murk* to use the water's own underwater colour at the right density; stack one in front of each background band and they add up to the same fade as the far water.
4. *Edge Softness* fades the fog toward the cut. *Override Waterline* uses a fixed Y instead of the scene's water.

Don'ts:

- Don't use `SpriteLitDepthWrite.mat` for fog: its depth writes hide everything drawn after the sheet.
- Don't add `WaterReflectable` to fog: the mirrored sheet would start abruptly at the waterline and draw a hard line across the surface. The water fades by itself through the **Water Surface** material's *Depth Fog* section: match its colour and Z range to your fog layers.

### Keeping sprites sharp behind the water

The front glass bends whatever is behind it (Front material **Distortion**: *Distortion Strength* for the slow wobble, *Ripple Refraction* for the bulge under passing ripples). To keep a character crisp while it swims, open **Project Settings > SpookyWater** and untick its sorting layer (e.g. `Player`) in **Distorted Sorting Layers**. Sprites on unticked layers are still tinted, fogged and lit by the water, just not wobbled; the water around them keeps bending. Independently, *Surface Calm Depth* fades all bending out toward the waterline, so anything half in the water (a swimmer, a raft) doesn't look cut in two at the surface. With the Front material's Debug > *Show Distortion Mask* on, pink = kept sharp, teal = bent; plain dark blue means the mask is off because no layer is unticked.

- Only layers before `SpookyWater_Surface` are listed (only they are seen through the glass). New layers start ticked.
- It works by sorting layer, for every SpriteRenderer, TilemapRenderer and SpriteShape on it. A distorted sprite drawn in front of an unticked one is kept sharp where they overlap.
- Cost: one extra half-resolution draw of the unticked layers per camera, only while at least one layer is unticked.
- To check it, tick **Debug > Show Distortion Mask** on the Front material (pink = kept sharp).

### Light shafts

Big parallel bands of underwater god rays, like sunlight slanting down through a cloud gap. **Attach:** `WaterLightShaft` (adds `MeshFilter` + `MeshRenderer`).

1. **GameObject > SpookyWater > Light Shaft**.
2. Drag the side and bottom handles in the Scene view to size the shaft. With *Snap Top To Waterline* (on by default) its top edge sits *Top Offset* above the water surface, so only X/Z placement matters. The light is clipped exactly at the front glass's moving top edge (ripples included), so it starts right at the top of the waterline.
3. Shafts are **underwater only**: anything above the waterline is cut. Use `Materials/Lighting/LightShaft_Underwater.mat`, the front glass's Z (0), sorting layer `SpookyWater_Front`, order 500.

The shaft is filled from **two band layers** (A and B), each a mask of flat white vertical bands on black (`Textures/Lighting/LightShaftBands.png` for A and the sparser `LightShaftBandsFine.png` for B by default) that scrolls sideways. Each layer has its own *Texture*, *Strength*, *Band Scale* (world units per repeat) and *Scroll Speed*; *Edge Blur Width* and *Glow* / *Glow Width* (world units) are shared by both. The layers combine as a union - light wherever at least one has a band, crossings no brighter - so scrolling them in opposite directions at slightly different scales makes the shaft shimmer in place instead of sliding. *Angle* leans the whole shaft (a parallelogram whose sides run along the bands, so no band is cut off by an edge); *Fill* lights the gaps, *Opacity* fades the whole shaft, and *Brightness Along Height*, *Side Fade* and *Surface Fade* soften its edges. To use your own image: draw flat white vertical bands on black (a mask only; color comes from the shaft) that tile left-right, and import it with Wrap U = Repeat, Wrap V = Clamp and mipmaps on.

To make a shaft melt into the water instead of reading as a shape on top of it (the defaults, an Ori-style glowing cone): keep the cone's outline soft (*Side Fade* 0.5, the depth curve, *Surface Fade*) but the streaks inside it visible enough to read as moving light: *Band Scale* around 10-12, *Edge Blur Width* 0.7-0.9, *Fill* around 0.25, a soft, wide *Glow* (about 0.4 over 3 units) rather than a strong narrow one, and the two layers scrolling against each other at clearly different speeds (0.4 / -0.55). Too wide, too blurred or too much Fill and the streaks merge into one still band; a long *Height* with a *Brightness Along Height* curve that drops fast and then trails to 0 with a flat end (the default), and a pale, slightly desaturated *Color* at low *Intensity* x *Opacity* (about 0.2). Saturated colors and short, linear fades read as a separate layer.

Give neighbouring shafts different *Seed* values.

**Shadows:** add `WaterLightShaftOccluder` to an object (**Add To Selection > Light Shaft Occluder**) and every shaft it overlaps is darkened below it along the shaft's angle, like a floating log or a rock leaving a dark streak in the light. Its shape is its Collider2D (or sprite bounds) by default, or a Box/Circle you size on the component; *Strength*, *Softness*, *Softness Growth* (the shadow blurs with distance) and *Shadow Length* (0 = to the bottom) shape the streak. Up to 16 occluders per shaft; moving occluders are tracked every frame.

### A glowing character and normal-lit scenery

An Ori-style glow: a soft pool of light around the character that lights the scenery near it, stronger and wider under the water, and makes the scenery read as rounded shapes rather than flat cut-outs. Two parts, usable separately:

1. **The light:** select the character and **GameObject > SpookyWater > Add To Selection > Glow Light**. This adds a child with `WaterGlowLight`, which drives a Point `Light2D`: one look in the air, another under the water (blended over *Blend Time* when crossing the surface, using `Water.IsUnderwater`), a slow breathing pulse and a faint visible halo. Normal Maps on the light are switched to Accurate.
2. **The shading:** select your scenery (a parent works: children are included) and **Add To Selection > Auto Normals Material**. Sprite, tilemap and SpriteShape renderers on Sprite-Lit-Default get `Materials/Sprites/SpriteLitAutoNormals.mat` (`SpookyWater/Sprite Lit Auto Normals`). It lights exactly like Sprite-Lit-Default, but in the 2D Renderer's normals pass it builds a normal from the sprite itself: the outline bulges toward the viewer (*Edge Bevel*, *Edge Width* in texels) and bright painted details stand out of dark ones (*Detail Bumps*, *Detail Width*). Lights with Normal Maps enabled then catch the edge facing them and leave the far side dark - the rim-lit look - without authoring a normal map per sprite. A hand-made normal map in the *Normal Map* slot is added on top.

**Keep the glow off distant layers:** on the Glow Light, untick the far background layers (sky, far hills and their fog layers) under *Lit Layers*. The character's light then stops at the mid ground, as if the background were far behind; those layers keep only the Global Light 2D. Any other Light 2D does the same through its own *Target Sorting Layers*.

For the look in the reference: keep the scene's Global Light 2D dim and blue underwater, so the glow carries the scene (2D lights add on top of the global light: against a full-bright global light keep the glow around 0.7-1, or it blows out to white); lower the glow's *Light Height* (0.8-1.5) for strong rim light, raise it for flatter light. *Edge Width* should grow with the art's resolution (about 1% of the sprite's size is a good start).

- **Cost:** 8 extra texture reads per pixel, only in the normals pass, which URP runs only while a light with Normal Maps is in view. No extra textures or memory. Global lights never use normals, so their look is unchanged.
- **Props in or at the water** (banks, stumps, anything the surface must clip against) should stay on `SpriteLitDepthWrite.mat`: it has the same auto normals, plus the depth write the 3D surface needs. On Sprite Lit Auto Normals (no depth write by default) the surface can't clip such a sprite and it pokes through as a box. Use Sprite Lit Auto Normals for everything else, or turn on its *Depth Write* with *Alpha Clip Threshold* 0.5.
- Materials other than Sprite-Lit-Default are left alone by the menu.
- With a Sprite Atlas, keep its *Padding* at least *Edge Width*, or neighbouring sprites bleed into the bevel.
- The normals are a guess from the sprite's shape and brightness: they read well on painted, rounded forms and less well on flat graphic shapes or sprites with dark highlights (lower *Detail Bumps* there).

### Cameras from your own camera system

Everything that depends on the view (the reflection camera, the ripple simulation window on big levels, contact-line culling, the horizon's width, ambient bubbles, mouse ripples) asks one place for the camera: `Water.Camera`. It is resolved in this order, on every call:

1. `Water.SetCamera(camera)`: an explicit override from code (`null` clears it).
2. The most recently enabled `WaterCameraSource` whose camera is active.
3. `Water.CameraResolver`: a `Func<Camera>` you supply.
4. The camera tagged `MainCamera`, unless *mainCameraFallback* is off in the [settings asset](#settings-asset).

Nothing in the package holds on to a camera: when the answer changes, `Water.CameraChanged` fires, the reflection camera re-copies the new camera's settings and resizes its texture, and the horizon is reshaped. A camera that appears after the level was generated is picked up the same frame.

Pick whichever matches how your project gets its camera:

- **The camera is a prefab your camera system spawns:** add **Water Camera Source** to the prefab (**Add To Selection > Camera Source** on the camera). It registers when enabled and unregisters when disabled or destroyed. No code, no tag.
- **The camera is acquired through a service, locator or DI container:** set a resolver once in your bootstrap code. It is called lazily, so it can return `null` until the camera exists:

  ```csharp
  using SpookyWater;

  Water.CameraResolver = () => CameraService.Current ? CameraService.Current.UnityCamera : null;
  ```

- **Your system tells you when the camera changes:** push it in from that event with `Water.SetCamera(newCamera)`.
- **Another camera (e.g. a UI camera) is tagged MainCamera:** turn off *mainCameraFallback* in the settings asset so the water waits for your camera instead of following the wrong one.

Single components can still be given a camera of their own: `WaterMouseRipples.Camera`, and `WaterField.FocusCamera` (the ripple window and culling of one field, e.g. a Water Pocket's).

### Mouse / touch ripples

**Attach:** `WaterMouseRipples` on the camera or any object (**GameObject > SpookyWater > Mouse Ripples**). It aims through its own *Camera* if set, otherwise through `Water.Camera`, looked up per click.

Left click pushes the water up, right click pushes it down, dragging with the left button held leaves a wake. It raycasts against the Surface colliders, so it needs a Surface tilemap. Works with either input system. Turn on *Debug Log* if clicks seem ignored.

### Ripples, splashes and bubbles from gameplay code

```csharp
using SpookyWater;
using UnityEngine;

public class Cannonball : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D hit)
    {
        var p = transform.position;
        if (!Water.TryGetSurfaceY(p, out var surfaceY) || p.y > surfaceY) return;

        Water.Ripple(new Vector3(p.x, surfaceY, 0f), strength: 0.6f, pushUp: false);
        Water.Splash(new Vector3(p.x, surfaceY, p.z), 1f, hit.relativeVelocity);
        Water.Bubbles(p, count: 8, spread: 0.4f);
    }
}
```

```csharp
public class BoatWake : MonoBehaviour
{
    Vector3 last;

    void Start() => last = transform.position;

    void Update()
    {
        var now = transform.position;
        Water.Wake(last, now, strength: 0.8f);
        if (Water.IsUnderwater(now))
            Water.BubbleTrail(last, now, count: 3, spread: 0.2f);
        last = now;
    }
}
```

```csharp
public class RainOnPond : MonoBehaviour
{
    [SerializeField] float dropsPerSecond = 20f;
    [SerializeField] Vector2 xRange = new(0f, 8f), zRange = new(0f, 4f);

    void Update()
    {
        if (!Water.HasLevel) return;
        var drops = Mathf.FloorToInt(dropsPerSecond * Time.deltaTime + Random.value);
        for (var i = 0; i < drops; i++)
        {
            var pos = new Vector3(Random.Range(xRange.x, xRange.y), 0f, Random.Range(zRange.x, zRange.y));
            Water.Ripple(pos, Random.Range(0.05f, 0.15f), radiusScale: 0.5f);
        }
    }
}
```

All calls are safe no-ops when there is no water level (or, for particles, no `WaterSplashEffects`). See [Scripting API](#scripting-api).

### Multiple ponds

Paint every pond on the same two tilemaps, separated by unpainted columns. Columns without front water are shores, and ripples reflect off shores, so ponds do not exchange ripples. Limits:

- **One level per scene.** A second active builder logs an error once and does not generate. If the active builder is disabled or destroyed (e.g. an additively loaded scene unloads), the next enabled builder takes over and generates.
- **One reflection plane** at the average surface height. Ponds should sit at the same height (a warning appears if columns differ by more than half a cell).
- **One near edge** at Z = 0. Ponds can differ in Z depth by painting fewer Surface rows.
- One simulation for all ponds. A very large level simulates a window that follows the camera.

### Underwater air pockets and caves

For a shaped pocket with water of its own, use a [Water Pocket](#water-pocket) (**GameObject > SpookyWater > Water Pocket**): a closed bezier outline cuts the air out of the front water whatever is painted, and the water under its Water Level gets its own waterline, a short surface strip reaching back into the pocket, its own ripple simulation, ambient ripples and wind. Ripples on the pocket never show on the lake and the other way round, and `Water.TryGetSurfaceY` / `IsUnderwater` treat the pocket's air as air and its Water Level as the surface above the water under it, so swimmers, floaters, splashes and bubbles all work there.

The tile way still works: leave a hole in the **Body** paint and the top of the cells under it becomes a plain waterline with its own contact band. It has no surface strip and shares the main lake's ripple simulation (ripples at the lake's front edge show on it).

### Underwater-only levels

Leave the Surface Tilemap empty (or unassigned). You get the front glass, the waterline and contact splashes, but no top surface, no mouse ripples and no reflection.

### Per-scene water looks

The shared materials in `Materials/` are used by every scene. For a scene with its own look, duplicate `WaterSurface.mat`, `WaterFront.mat` and `WaterHorizon.mat` next to your scene, edit the copies and assign them on the builder (*Materials* group). Required when the package is installed from Git (read-only).

Keep the **Ripple Height** of the Surface material equal to the **Ripple Scale** of the Front material so the edges meet. Horizon reflection, fog and underwater values are copied from the Surface material on every Generate.

## Component reference

Every component appears under **Add Component > SpookyWater/**. Every field has a tooltip; every component's Help (?) button opens its section below. Groups are drawn as collapsible foldouts ("Advanced" and "Debug" start closed).

### Water Tilemap Builder

`SpookyWater/Water Tilemap Builder` - reads the tilemaps, generates the meshes and colliders, owns the ripple simulation, the reflection and every level-wide setting. **One per scene.** Put it on the object with the `Grid`.

**Tilemaps**

| Field | Default | Meaning |
|---|---|---|
| Front Tilemap | (Tilemap on this object) | The XY cross-section. Paint Water Body on every water cell. |
| Surface Tilemap | none | The XZ top, painted as a map (rows = depth). Optional. |

**Materials** (press **Assign Defaults** to fill empty slots)

| Field | Shader | Notes |
|---|---|---|
| Surface Mesh Material | `SpookyWater/Water Surface` | Required if a Surface tilemap is used. |
| Line Mesh Material | `SpookyWater/Water Front` | The waterline strip. |
| Body Mesh Material | `SpookyWater/Water Front` | The submerged glass; can be the same material as Line. |
| Horizon Material | `SpookyWater/Water Horizon` | Optional; empty = no horizon mesh. |
| Ripple Simulation Material | `Hidden/SpookyWater/Ripple Simulation` | Required. |

**Horizon**: *Horizon Distance* (60; 12 from the create menu) how far past the painted surface it reaches; *Horizon X/Z Segments*; *Horizon Spread* (1 = fans out with the camera's view angle so its sides stay off-screen); *Horizon Seam Blend* (world units over which the surface blends into the horizon).

**Ripples**

*Ripple Grid* (the simulation):

| Field | Default | Meaning |
|---|---|---|
| Texels Per Unit | 32 | Simulation resolution. Higher = finer rings, more GPU cost. |
| Max Resolution | 1024 | Largest simulation texture side. Levels bigger than `Max Resolution / Texels Per Unit` world units simulate a window following the camera. |
| Mask Texels Per Unit | 16 | Resolution of the shoreline mask (baked once). |
| Tick Rate | 60 | Simulation ticks per second, independent of frame rate. |
| Open Edge Width | 1.5 | Absorbing border where water continues (front edge, horizon, window edges). |
| Impact Radius | 0.2 | Radius of the bump a strength-1 contact stamps in. Sets ring spacing. |
| Impact Height | 4 | Height of that bump: how much energy a splash puts in. |
| Speed | 4 | World units per second rings travel. |
| Z Squash | 0.5 | Squashes rings front-to-back for the 2.5D look. |
| Spring Frequency | 0 | Extra bobbing after a ring passes (Hz). |
| Damping | 1.2 | Energy loss per second (a ring keeps e^-damping each second). |
| Distance Loss | 0 | Extra loss per world unit travelled. |
| Shore Reflectivity | 1 | How much bounces off shores (0 = absorbing beaches). |
| Obstacle Reflectivity | 1 | How much bounces off `WaterRippleObstacle`s. |
| Wake Radius / Wake Height / Wake Bow Ratio | 0.15 / 2 / 0.6 | Trough dragged by movers, and the bump pushed ahead of them. |

*Ambient Ripples* (random small ripples so still water doesn't look like glass; simulated and spread freely): *Enabled* (on), *Interval* (0.4-1.8 s), *Count* per event (1-2), *Cluster Spread* (0.35), *Radius* (0.08-0.14), *Strength* (0.4-1.2), *Down Chance* (0.5).

*Wind* (off by default; constant, gentle movement of the whole surface as if wind were blowing on it - long low wave bands that drift with the wind and break the reflection into wavering streaks; drawn analytically by the shaders on top of the simulation, so it costs no simulation time and splashes still work; all live-tunable): *Enabled* (off), *Strength* (0.5, scales height from 0 to Wave Height), *Direction* (270 = toward the camera, 90 = away, 0 = right), *Wave Height* (0.08, simulation units x material Ripple Height), *Wavelength* (1.2 world units; two smaller sets ride on top), *Crest Length* (3, how stretched the crests are across the wind), *Speed* (0.6), *Gustiness* (0.5, calm mirror patches between gusts), *Gust Size* (8), *Gust Speed* (1.5). Wind also moves the waterline, the horizon water's reflection and floating debris (visually; `WaterFloater` and debris drift do not feel it).

*Front Ripples* (tiny analytic ripples just behind the front edge, never spread across the lake; max 16 at once): *Enabled*, *Interval* (0.08-0.3 s), *Max Distance* from the front (1.2), *Max Radius* (0.5), *Lifetime* (0.7 s), *Speed* (0.8), *Wavelength* (0.07), *Strength* (0.15-0.35).

*Displacement Scale* (1; 0.25 from the create menu): multiplies the materials' ripple height for every generated tile.

**Interaction**

| Field | Default | Meaning |
|---|---|---|
| Contact Ripple Band Thickness | 0.1 | Height of the trigger band along each waterline run. |
| Contact Layers | Everything | Physics layers whose bodies ripple and splash. |
| Contact Ripple > Strength Range | 0.2-0.6 | Ripple strength from slow to fast contacts. |
| Contact Ripple > Enter Arm / Enter Splash Fraction | 0.2 / 0.25 | Submerged fractions that arm and fire an entry splash. |
| Contact Ripple > Exit Arm / Exit Splash Fraction | 0.85 / 0.75 | Submerged fractions that arm and fire an exit splash. |

**Reflection**: *Reflection Mask* (extra physics layers to reflect, in addition to `WaterReflectable` objects); *Underwater Clip Margin* (0.25, how far below the plane the see-through view starts when the camera is underwater); *Reflection Resolution Scale* (0.5 of the screen; lower = cheaper and softer).

**Mesh Detail**: *Surface Segments Per Cell* (12, waterline/surface edge detail), *Vertical Segments Per Cell* (4), *Surface Detailed Row Count* (3 nearest rows use finer segments), *Surface Near Segments* (20 x 20), *Surface Far Segments* (3 x 2).

**Advanced**: *Chunk Size* (16 cells; one mesh per chunk per kind, culled separately), *Field Margin* (2), *Regenerate On Start* (on - **leave it on**: ripple textures and shader globals are runtime-only).

**Debug**: *Verbose Field Logging* (logs every ripple and draws recent ones as gizmos).

**Buttons**: Generate Water, Clear, Select Front Tilemap, Select Surface Tilemap, Create Tile Palette, Assign Defaults, Open Project Setup. The inspector also reports a missing Front Tilemap, empty tilemaps, a second builder and an orthographic camera.

**Scripting**: `builder.Generate()` rebuilds the water (e.g. after painting tiles at runtime); `builder.Field` is the `WaterField`.

### Water Splash Effects

`SpookyWater/Water Splash Effects` - **one per scene**, created as a child of the water by **GameObject > SpookyWater > Water** (or by Project Setup's fix when it is missing). Builds its particle systems in code. Used by contact splashes, `WaterSwimmer`, floaters and the `Water.Splash/Spray/Bubbles/BubbleTrail` calls; also spawns ambient bubbles in on-screen water. Access from code: `WaterSplashEffects.Instance`.

| Group | Key fields (defaults) |
|---|---|
| Materials | Droplet, Mist, Crown, Bubble, Ring materials (`Materials/Particles/`, shader `SpookyWater/Water Particle`). Filled automatically. |
| Impacts | *Reference Impact Speed* (10 u/s = strength 1), *Min Impact Speed* (1.2; slower crossings only ripple), *Exit Strength Scale* (1), *Max Strength* (2.5). |
| Splash Crown | *Crown Size* (1.9 x 1.0 at strength 1; height linear in strength), *Crown Lifetime* (0.65-0.85 s), *Crown Count* (2-3 layered sheets), *Water Color*. |
| Splash Strands | *Strands Per Strength* (16), *Strand Size* (0.12-0.26), *Strand Speed Scale* (0.6), *Strand Stretch* (0.07), *Strand Landing Spray* (2-5). |
| Splash Droplets | *Droplets Per Strength* (60), *Droplet Speed* (2.5-6.5), *Droplet Angle* (8-50 deg), *Droplet Inherit Velocity* (0.35), *Droplet Depth Speed* (0.5), *Droplet Size* (0.035-0.16), *Droplet Gravity* (1.6 x physics), *Droplet Landing Ring Size* (0.25). |
| Droplet Ripples | *Droplet Ripple Chance* (0.5), *Droplet Ripple Strength* (0.06-0.25), *Droplet Ripple Radius* (0.3-0.7 x impact radius), *Max Droplet Ripples Per Frame* (3). |
| Splash Mist and Foam | *Mist Per Strength* (5), *Mist Size*, *Mist Lifetime*, *Mist Color*, *Foam Ring Flatten* (0.28), *Foam Color*. |
| Wake Spray | (dash trail) *Wake Droplets Per Unit* (9), *Wake Lift* (1-2.7), *Wake Drift* (-1-0.8), *Wake Length* (0.5). |
| Plunge Bubbles | *Plunge Bubbles Per Unit* (30), *Plunge Trail Duration* (1.2 s), *Plunge Trail Spread* (0.25). |
| Bubbles | *Bubble Rise Speed* (0.9-1.8), *Bubble Size* (0.03-0.09), *Bubble Wobble* (0.6), *Bubble Color*, *Pop Ring Size* (0.16). |
| Ambient Bubbles | *Ambient Clusters Per Second* (9; 0 = off), *Ambient Cluster Size* (2-7), *Ambient Depth Z* (0.1-4.5), *Bubble Far Opacity* (0.35). |

### Water Floater

`SpookyWater/Water Floater`, requires `Rigidbody2D` (Dynamic). The inspector shows only the group for the current mode.

| Group | Field | Default | Meaning |
|---|---|---|---|
| - | Mode | Platform | Platform or Object (see [Floating platforms and objects](#floating-platforms-and-objects)). |
| - | Shape | (own Collider2D) | Collider whose shape floats (Box/Capsule as a box, Circle as a circle). |
| Buoyancy | Float Depth | 0.35 | Fraction of its height under water when unloaded. |
| | Load Stiffness | 6 | Resistance to being pushed deeper (1 = plain buoyancy). |
| | Slices | 8 | Vertical slices buoyancy is computed over (smoother tilting). |
| | Damping Ratio | 0.8 | 0 = rings forever, 1 = critically damped. |
| | Water Drag | 2 | Sideways drag per second in water. |
| Platform | Min Freeboard | 0.05 | Top always stays at least this far above water. |
| | Max Tilt | 12 deg | Most it can lean. |
| | Anchor Strength | 8 | Pull back to its placed X (0 = drifts). |
| Object | Ridden Sink Depth | 1.5 | How much lower it floats while ridden. |
| | Sink Speed | 1.2 | Sink/rise speed (u/s). |
| | Resurface Delay | 0.4 s | Wait after the rider leaves. |
| | Rider Reach | 1.5 | Height above it within which dynamic bodies still count as riding once sunk. |
| | Upright Strength | 20 | Pull back to its placed angle (0 = rolls freely). |
| Ripples | Ripple Spacing / Threshold / Strength / Max Ripple Strength / Wake Strength | 0.75 / 0.03 / 8 / 1.5 / 0.5 | Ripples made as its waterline bobs, and the wake while drifting. |

### Water Surface Debris

`SpookyWater/Water Surface Debris`, adds `MeshFilter` + `MeshRenderer`.

| Group | Field | Default | Meaning |
|---|---|---|---|
| Look | Sprite | lily pad | Drawn lying flat on the water. |
| | Material | `SurfaceDebris.mat` | Must use `SpookyWater/Surface Debris`. Shared by all debris. |
| | Tint | white | Multiplied onto the sprite. |
| | Segments | 6 | Grid cells per side; more follows small ripples closer. |
| | Lift | 0.01 | Height above the water. |
| | Sorting Layer / Order | Surface / 10 | Must be above the surface tiles (order 0). |
| Drift | Push Response | 1 | How far ripples shove it (0 = only bobs). |
| | Water Drag / Max Speed | 1.5 / 1.5 | Glide-to-stop rate and speed cap. |
| | Spin / Spin Drag | 120 / 2 | Spin a kick adds, and how fast it stops. |
| | Collision Radius / Bounciness | 0 (auto) / 0.3 | Collision with shores, obstacles and other debris. |
| Anchor | Anchor Strength / Anchor Slack | 0 / 0.25 | Stem-like pull back to its placed position. |

From code: `Velocity`, `AddVelocity(Vector2 worldXZ)` (push it yourself, e.g. when a fish bumps it).

### Water Ripple Obstacle

`SpookyWater/Water Ripple Obstacle`.

| Field | Default | Meaning |
|---|---|---|
| Shape | Auto | Auto (from SphereCollider, BoxCollider, sprite or Collider2D), Circle or Box. |
| Size | (1, 1) | Circle: x = diameter. Box: (x, z). Local space, scaled by the transform. Ignored in Auto. |
| Sprite Thickness | 0.15 | Z depth of the footprint derived for 2D sprites/colliders. |
| Padding | 0 | Grows (or shrinks if negative) the footprint. |

Max 32 active obstacles.

### Water Pocket

`SpookyWater/Water Pocket`. An underwater air pocket shaped by a closed bezier in the front (XY) plane at world Z = 0. Inside the outline and above the Water Level the front water (WaterFront) is not drawn; inside and below it the pocket draws its own front water. It builds its pieces itself (in Edit mode too, not saved in the scene) and rebuilds after any change. Drag anchors, handles (Alt breaks a smooth corner) and the Water Level arrow in the Scene view; **Add Point** splits the curve after the selected anchor, **Copy Ripples From Builder** starts the ripple settings from the level's.

| Field | Default | Meaning |
|---|---|---|
| Points | rounded 6 x 3 box | Anchors with in/out handles, relative to the object's position (rotation and scale are ignored). |
| Water Level | -0.9 | Height of the pocket's water, relative to the object. Must cross the outline. |
| Curve Steps | 16 | Segments per bezier span. |
| Depth | 1.5 | How far the surface strip reaches back (world Z). |
| Surface Material | builder's | WaterSurface material for the strip. No planar reflection: it mirrors its Reflection Sky Color. |
| Displacement Scale | 1 | Multiplies both materials' Ripple Height (smaller waves than the lake). |
| Surface Segments | (16, 12) | Strip mesh: X per unit, Z over the depth. |
| Surface Expand | 1.5 | How far the strip widens on each side by its back edge, in world units (like the horizon fan). Raise it until the strip's sides stay hidden behind the front water around the pocket; 0 is a plain strip. |
| Clipped Sprites | empty | Sprite Renderers clipped by the outline (the pocket's back wall, rocks, plants). The pocket adds a Sprite Mask shaped exactly like the outline and sets their Mask Interaction, restoring it when the pocket is disabled or a sprite leaves the list. |
| Clip Mode | Visible Inside Mask | Inside shows the sprites only within the outline; Outside hides them there. |
| Clip Pixels Per Unit | 32 | Size of the mask's backing texture (edge accuracy is one pixel of this, bilinear-smoothed). |
| Line Material | builder's Line | WaterFront material for the pocket's water; a runtime copy draws only inside the outline. |
| Waterline Band | 1 | Height of the animated band under the Water Level (the tile waterline is one cell). The band is always cut out across the waterline's width, even where the outline's bottom sits right on the Water Level. |
| Waterline Segments Per Unit / Vertical Segments | 12 / 4 | Band mesh detail. |
| Ripple Simulation Material | builder's | The simulation material. |
| Ripple Grid / Ambient Ripples / Front Ripples / Wind | pocket defaults | The pocket's own simulation, separate from the lake's. Back and sides are shores; the front edge absorbs. |
| Contact Band Thickness / Contact Layers / Contact Ripple | 0.1 / Everything / defaults | The trigger band along the pocket's waterline (same behaviour as the builder's). |

The pocket's waterline lights the front water under and beside it (Surface Glow, Glow and Mist light of the Water Front material are measured from the nearer of the lake surface and the pocket's Water Level), up to 8 pockets per scene. The cut uses one stencil bit (128) of the camera's depth/stencil buffer, so the URP 2D Renderer needs its depth/stencil buffer (on by default). Moving the pocket in Play mode does not move its water; regenerate it.

### Water Mouse Ripples

`SpookyWater/Water Mouse Ripples`. Put it on the camera.

| Field | Default | Meaning |
|---|---|---|
| Camera | empty | Camera that aims the cursor ray. Empty = `Water.Camera` (see [Cameras from your own camera system](#cameras-from-your-own-camera-system)). |
| Ripple Strength | 0.4 | Click strength (left = up, right = down). |
| Wake Strength | 1 | Wake left while left-dragging (0 = none). |
| Raycast Mask | Everything | Layers the ray can hit. Only Surface tiles are used. |
| Debug Log | off | Logs every click and why it did or did not ripple. |

### Water Swimmer

`SpookyWater/Water Swimmer`, requires `Rigidbody2D`.

| Group | Field | Default | Meaning |
|---|---|---|---|
| Water | Float Height | 0.9 | Height above the feet that floats level with the surface. |
| | Surface Depth | 0.4 | Deeper than this below the float point = submerged (bubbles instead of spray). |
| | Swim Speed / Fast Speed | 3 / 9 | Normal swim speed, and the speed at which the dash trail is full. |
| Wake | Wake Strength / Wake Bulge / Wake Max Depth / Dash Wake Multiplier | 1 / 2 / 1.6 / 3 | Wake in the ripple sim while swimming near the surface. Wake Bulge multiplies the mound pushed up ahead of the swimmer (on top of the Ripple Grid's Wake Bow Ratio). |
| Bow Wave | Bow Wave Height / Length / Depth / Front Width / Trough | 0.6 / 0.9 / 1.5 / 0.6 / 0.2 | Size and shape of the mound pushed along the surface (0 height = none). |
| | Bow Wave Min Speed / Delay / Build Time | 0.5 x Swim Speed / 0.35 s / 1.4 s | Steady swimming that starts it, and how long it takes to grow. |
| | Bow Wave Start Behind / End Behind | 1.3 / 0 | Where it appears behind the character, and where it settles once built (0 = under them). |
| | Bow Wave Fade Time / Drag | 1.1 s / 1.2 | How long a let-go mound rolls on, and how fast it slows. |
| Surface Spray | Spray Rate / Bow Offset | 7 per s / 0.3 | Spray while swimming along the surface. |
| Bubbles | Bubbles Per Unit / Dash Bubble Multiplier / Breath Interval / Bubble Height | 4 / 3 / 1.2-2.6 s / 0.7 | Underwater trail and breath bubbles. |
| Events | OnEnterWater / OnExitWater | - | UnityEvents. |

API: read `InWater`, `SurfaceY`, `Depth`, `Submerged`; set `Dashing`, `FacingLeft`, `EffectsEnabled`; call `SetSpeedRange(swimSpeed, fastSpeed)`, `DashBurst(direction)`, `ResetTrail()`.

`WaterSwimmer` does not move the character - movement is your controller's job.

### Water Reflectable

`SpookyWater/Water Reflectable`.

| Field | Default | Meaning |
|---|---|---|
| Reflect | on | Mirror this object in the surface. |
| Visible From Below | on | Show it through the surface when the camera is underwater. |
| Include Children | on | Also cover child sprite/tilemap/sprite shape renderers. |
| Sync Sorting / Material / Draw Mode (+ intervals) | off (60 frames) | Keep re-copying these at an interval, for objects that change them at runtime. |

API: `SyncAllNow()`.

### Water Fog Sprite

`SpookyWater/Water Fog Sprite`, requires `SpriteRenderer` with the Sprite Fog material.

| Field | Default | Meaning |
|---|---|---|
| Side | Everywhere | Everywhere, Above Water Only, Below Water Only. |
| Edge Softness | 0.1 | Fade toward the cut (world units). |
| Override Waterline / Waterline Y | off / 0 | Use a fixed Y instead of the scene's water height. |
| Match Water Murk | off | Below Water Only: use the water's underwater murk colour/density instead of the sprite colour. |

### Water Light Shaft

`SpookyWater/Light Shaft`, adds `MeshFilter` + `MeshRenderer`. Scene-view handles for width and height.

| Group | Fields |
|---|---|
| Shape | Width (12), Height (9), Snap Top To Waterline, Top Offset (0.5), Angle (degrees from vertical, 30; leans the whole shape). |
| Light | Color (HDR), Intensity, Opacity, Fill. |
| Bands | Edge Blur Width (0.8), Glow (0.4), Glow Width (3), shared by both layers. |
| Band Layer A / Band Layer B | Texture, Strength, Band Scale (A 12, B 9.5), Scroll Speed (A 0.4, B -0.55). Combined as a union. |
| Fade | Brightness Along Height (curve), Side Fade, Surface Fade. |
| Water | Override Waterline, Waterline Y (always cut to below the waterline). |
| Rendering | Material (`LightShaft_Underwater.mat`), Sorting Layer, Sorting Order (500), Seed. |

Per-shaft looks go through a MaterialPropertyBlock, so one material serves every shaft.

### Water Light Shaft Occluder

`SpookyWater/Light Shaft Occluder`. Casts a shadow in every Water Light Shaft it overlaps, down along the shaft's angle. Selected, it outlines its shape in the Scene view.

| Group | Fields |
|---|---|
| Shape | Shape (Auto: Collider2D - Box and Circle exactly, others by bounds - else sprite bounds; Box; Circle), Size, Offset (Box/Circle, local space), Padding. |
| Shadow | Strength (0.85), Softness (world units at the object), Softness Growth (extra blur per unit below), Shadow Length (0 = to the bottom of the shaft). |

Each shaft takes the first 16 occluders that overlap it. Shadows combine as a union (overlapping shadows are no darker than the darkest one).

### Water Glow Light

`SpookyWater/Glow Light`, requires `Light2D` (set to Point, Normal Maps Accurate on Reset). Best on a child of the character. Drives the light's color, intensity, radius, falloff and halo every frame; edit those here, not on the Light 2D.

| Group | Fields |
|---|---|
| Above Water | Color (HDR), Intensity (0.7), Radius (4). |
| Underwater | Underwater Color, Underwater Intensity (1), Underwater Radius (6.5), Blend Time (0.4 s). |
| Shape | Core (fully bright fraction of the radius), Falloff (Light 2D falloff), Light Height (the Light 2D's Normal Map Distance; lower = stronger rim light), Halo (visible light volume). |
| Lit Layers | Lit Sorting Layers: the sorting layers the glow shines on (drives the Light 2D's *Target Sorting Layers*; edit it here, not on the Light 2D). Starts as the Light 2D's own targets; layers added later start ticked. |
| Pulse | Pulse Amount, Pulse Speed. |

Script access: `Intensity`, `UnderwaterIntensity`, `IsUnderwater`. Sprites need normals to show relief: a baked normal map ([Sprite Normal Baker](#sprite-normal-baker)) or a normal-aware material (Sprite Lit Auto Normals); others are lit flat.

### Sprite Normal Baker

An editor window (**Tools > SpookyWater > Sprite Normal Baker**) that bakes a real normal map for every sprite in the scene, for sprites that should catch 2D lights with more control than Sprite Lit Auto Normals' runtime guess. Each texture gets `<name>_n.png` next to it (imported as a Normal map) and, by default, it is assigned as the sprite's `_NormalMap` secondary texture. Baking again rewrites the same files.

1. Open the window and create (or pick) a **Default Profile**: a `SpriteNormalBakeProfile` asset holding the bake settings.
2. It lists every sprite texture used by SpriteRenderers and Tilemaps in the open scenes (or the open prefab; or only the **Selected Objects**), with a status: *Not baked*, *Baked, not assigned*, *Out of date* (the image or the profile's settings changed since the bake), *Baked*, *Has its own normal map* (a hand-made `_NormalMap`; left unchecked, baking replaces it), *In a read-only package*.
   With *Only Lit Layers* (on by default) it skips sprites on sorting layers that no normal-mapped 2D light targets, such as distant backgrounds kept out of the glow: their normals would never be used. It says how many textures it hid and on which layers.
3. Click a texture to preview it with its profile's settings below the list; drag in the Lit preview to move the light.
4. Textures that need different settings (small props vs big rocks) can get their own profile in the row's profile field; the texture is moved into that profile's list. Everything else uses the default profile, and baking adds it to that profile's list.
5. **Bake** bakes the checked textures in one batch (**Needing Bake** checks just the stale ones).

A profile can also be baked on its own: select the asset and press **Bake All** in its Inspector (same preview). **Assets > Create > SpookyWater > Sprite Normal Bake Profile** creates one.

How the map is built: the outline is rounded inward (exact distance from the edge through the *Bevel Shape* curve), lines and colour changes drawn inside the sprite cut grooves so each part rounds on its own, and the painting's local brightness adds surface detail (only changes smaller than *Detail Width*, so painted shadows don't become dents). Sprite sheets are baked per sprite rect.

| Group | Fields |
|---|---|
| Sources | Textures, Suffix (`_n`), Assign To Sprite. |
| Shape | Alpha Threshold, Bevel Shape (Round / Smooth / Linear), Bevel Width (px), Bevel Height, Shape Smoothing (px), Bevel Texture Borders (off for tiles that run off the edge). |
| Inner Edges | Inner Edges (groove depth, 0 = off), Inner Edge Threshold, Inner Edge Width (px). |
| Detail | Detail Bumps, Detail Width (px), Detail Softness (px), Invert Detail. |
| Output | Normal Strength, Flip X, Flip Y, Padding (px of edge normal spread past the outline for filtering). |

- The preview shows the lit sprite and its normal map side by side (or Lit, Normals, Source, or the Baked File on disk alone); drag in it to move the point light. Each row in the window also shows the baked normal map next to the sprite. Big textures preview at reduced size, the bake is full resolution.
- SpriteShape and UI Image sprites are not scanned; add their textures to a profile by hand.
- Use baked sprites with **Sprite-Lit-Default** and lights with Normal Maps enabled. Sprite Lit Auto Normals and Sprite Lit Depth Write add their own guess on top of a `_NormalMap`: set their *Edge Bevel* and *Detail Bumps* to 0 for baked sprites.
- Reads PNG/JPG files directly; other formats are read from the imported texture (after compression and size limits).
- Check flipped sprites (Flip X or negative scale) under a moving light: if they shade from the wrong side, their normals are not being mirrored with the art.

### Water Camera Source

`SpookyWater/Water Camera Source`. Put it on your gameplay camera, or on the camera prefab your camera system spawns, to make it the camera the water follows while the component is enabled. It runs in Edit mode too, so editor previews use the same camera.

| Field | Default | Meaning |
|---|---|---|
| Camera | empty | The camera to hand over. Empty = the `Camera` on this GameObject. |

With several enabled, the most recently enabled one wins; `Water.SetCamera` overrides them all. Full resolution order: [Cameras from your own camera system](#cameras-from-your-own-camera-system).

### Internal components

`WaterTile`, `WaterField`, `WaterContactRipple` and `WaterReflectionRenderer` are created by the builder on generated objects and hidden from the Add Component menu. Don't add them by hand; their settings live on the builder.

## Scripting API

Namespace `SpookyWater`, static class `Water` (`Runtime/Core/Water.cs`). Every effect call is a no-op without a level (or without a `WaterSplashEffects` for particles), so gameplay code never needs null checks.

| Member | Meaning |
|---|---|
| `bool Water.HasLevel` | A builder is registered (a level exists and is enabled). |
| `WaterTilemapBuilder Water.Level` | The active builder, or null. In Edit mode it finds and caches the scene's builder so editor-time effects work. |
| `WaterField Water.Field` | The level's ripple field, or null before Generate. |
| `WaterReflectionRenderer Water.Reflection` | The shared reflection renderer, or null. |
| `bool TryGetSurfaceY(Vector2 point, out float surfaceY)` | World Y of the water surface above `point`; false if the point is not in a water cell or is in a Water Pocket's air. Under a pocket it is the pocket's Water Level. |
| `bool TryGetTileSurfaceY(Vector2 point, out float surfaceY)` | The same from the painted tiles alone, ignoring Water Pockets. |
| `WaterField FieldAt(Vector3 worldPos)` | The ripple field for a point: a Water Pocket's own field when the point is over its waterline and nearer its level than the lake's, otherwise `Field`. |
| `IReadOnlyList<WaterPocket> Water.Pockets` | The enabled Water Pockets. |
| `bool IsUnderwater(Vector2 point)` | In a water cell and below its column's flat surface (ripples not included). |
| `bool TryGetWaterlineY(out float y)` | Y of the reflection plane (the level's average surface height). |
| `Camera Water.Camera` | The camera the water follows: the `SetCamera` override, else the latest enabled `WaterCameraSource`, else `CameraResolver`, else `Camera.main` (if *mainCameraFallback*). Can be null. |
| `void SetCamera(Camera camera)` | Explicit camera override; `null` clears it. |
| `Func<Camera> Water.CameraResolver` | Your own lookup, used when no override or Water Camera Source gives a camera. May return null until your camera exists. |
| `event Action<Camera> Water.CameraChanged` | Raised when `Water.Camera` resolves to a different camera (or to null). |
| `void RefreshCamera()` | Re-resolves now and raises `CameraChanged` if the camera changed. The package also re-resolves every frame on its own. |
| `void Ripple(Vector3 worldPos, float strength, bool pushUp = true, float radiusScale = 1f)` | A ripple. Ordinary contacts use 0.2-0.6. |
| `void RippleAlong(Vector3 from, Vector3 to, float strength, bool pushUp = true, float radiusScale = 1f)` | A ripple stamped along a segment. |
| `void Wake(Vector3 from, Vector3 to, float strength = 1f, float bulge = 1f)` | A wake trough along a segment, with a mound pushed up ahead (`bulge` multiplies the mound); scales with distance covered, so it is frame-rate independent. Call it every frame with last/current position. |
| `void Splash(Vector3 pos, float strength, Vector2 velocity)` | Crowns, strands and droplets. Strength ~ impact speed / Reference Impact Speed. Surface height looked up from `pos`. |
| `void Spray(Vector3 pos, float amount, Vector2 velocity)` | A miniature splash. |
| `void Bubbles(Vector3 pos, int count, float spread, float driftX = 0f, float sizeScale = 1f)` | A burst of bubbles that rise and pop at the surface. |
| `void BubbleTrail(Vector3 from, Vector3 to, int count, float spread, float driftX = 0f, float sizeScale = 1f)` | Bubbles along a segment. |

Notes:

- `Ripple`, `RippleAlong` and `Wake` take positions on the surface plane: X and **Z** matter; Y only picks between the lake and a Water Pocket (`FieldAt`). Z = 0 is the near edge; use the object's own Z for things inside the water.
- `WaterSplashEffects.Instance` exposes the same effects with explicit surface heights, plus `Impact`, `StartPlunge` and a dash `Wake(from, to, surfaceY, intensity)`.
- `WaterTilemapBuilder.Generate()` rebuilds the level, e.g. after changing tiles at runtime.

## Layers and sorting layers

Draw order comes from sorting layers; Z only drives parallax and perspective. The water needs one physics layer and two sorting layers (Project Setup creates them):

- Physics layer **`SpookyWater_Reflections`**: hidden proxy sprites from `WaterReflectable`; only the reflection camera renders it. Don't put your own objects on it.
- Sorting layers **`SpookyWater_Surface`** and **`SpookyWater_Front`**:
  - Everything on layers **before** `SpookyWater_Surface` is captured by the Camera Sorting Layer Texture and is refracted/tinted by the front glass.
  - Everything **after** `SpookyWater_Front` draws over the water and is never refracted.
  - `Default` (and `Player`) must come before `SpookyWater_Surface`.

Suggested order for a 2.5D scene (back to front; yours can differ as long as the rules hold):

| Band | Sorting layers | Notes |
|---|---|---|
| Sky | `SkyBox` | Sky gradient, far clouds. |
| Background | `Background_3`, `Background_3_Fog`, `Background_2`, `Background_2_Fog`, `Background_1`, `Background_1_Fog` | Each `_Fog` layer hazes everything behind it. |
| Mid (gameplay) | `MidGround`, `Mid_Fog`, `Default`, `Player` | Characters, props and banks. |
| Water | `SpookyWater_Surface`, `SpookyWater_Front` | Required. |
| Foreground | `Foreground`, `Foreground_Fog` | Never refracted. |

Sorting orders the package uses inside the water layers (configurable in the [settings asset](#settings-asset)): surface tiles 0, floating debris 10, bubbles and surface rings 1000 (Surface layer), underwater light shafts 500 and splash spray 1000 (Front layer).

Reflections are lit by your Light2Ds like normal sprites, so a light that targets a sorting layer also lights the reflections of objects on it.

## Settings asset

`SpookyWaterSettings` (`Resources/SpookyWaterSettings.asset`; **Assets > Create > SpookyWater > Settings**; edit it under **Project Settings > SpookyWater**) is the single place for layer names, sorting orders and the camera fallback:

| Field | Default |
|---|---|
| `reflectionLayer` | `SpookyWater_Reflections` |
| `surfaceSortingLayer` | `SpookyWater_Surface` |
| `frontSortingLayer` | `SpookyWater_Front` |
| `debrisSortingOrder` | 10 |
| `underwaterShaftSortingOrder` | 500 |
| `bubbleSortingOrder` | 1000 |
| `spraySortingOrder` | 1000 |
| `distortedSortingLayers` | every layer (see [Keeping sprites sharp behind the water](#keeping-sprites-sharp-behind-the-water)) |
| `mainCameraFallback` | on: use the camera tagged `MainCamera` when no other camera is given (see [Cameras from your own camera system](#cameras-from-your-own-camera-system)) |

It is loaded with `Resources.Load("SpookyWaterSettings")` from any `Resources` folder. If you rename a layer here, also rename it in **Tags and Layers** (Project Setup shows what is out of sync). Without an asset, the defaults above are used.

## Material settings

Every SpookyWater shader shares one material inspector: collapsible sections (state remembered), a search box, Expand All / Collapse All, dependent properties hidden until enabled, and an *Advanced* foldout (render queue, GPU instancing). Hover any property for a tooltip ending with its real property name.

Materials live in `Materials/`. Duplicate before tweaking when several scenes share them.

| Shader | Used by | Sections |
|---|---|---|
| `SpookyWater/Water Surface` (`WaterSurface.mat`) | Surface tiles | **Surface** (ripple height, distance and edge falloff, normal strength), **Color** (shallow-to-deep gradient, rim), **Glow** (self-lit HDR glow, ripple boost, underside), **Depth Fog** (fade to a fog colour by world Z; match your fog layers), **Reflection** (blur, distortion, tint, sky colour, Fresnel), **Contact Wavelets** (lapping strokes at object bases that smear the reflection), **Ripple Highlight**, **Surface Shimmer**, **Underside** (what the surface looks like from underwater), **Detail**, **Debug** (ripple heat map, click marker). |
| `SpookyWater/Water Front` (`WaterFront.mat`) | Line and Body tiles | **Surface** (Ripple Scale; keep equal to the Surface material's Ripple Height), **Color**, **Clarity** (how much shows through, fade with depth), **Glow** (self-lit HDR glow, strongest under the surface), **Mist** (lit haze behind the glass), **Waterline** (foam line, glow), **Distortion** (wobble, ripple refraction, calm band under the waterline; sharp layers via the settings asset), **Detail**, **Debug**. |
| `SpookyWater/Water Horizon` (`WaterHorizon.mat`) | Far fan | **Surface**, **Horizon** (near colour, horizon colour, haze distance, underwater fade, underwater haze colour), **Detail**. Reflection/fog/glow/underwater values are copied from the Surface material on Generate. |
| `SpookyWater/Surface Debris` (`SurfaceDebris.mat`) | Debris | **Debris** (tint, alpha cut-out, tilt shading, wet edge, fog). |
| `SpookyWater/Water Particle` (`Materials/Particles/`) | Splash particles | **Shape** (droplet, bubble, ring, crown, puff), **Color**, **Blending**. |
| `SpookyWater/Light Shaft` (`LightShaft.mat`, `LightShaft_Underwater.mat`) | Light shafts | **Blending**, **Bands** (default images for layers A and B); the rest is per shaft. |
| `SpookyWater/Sprite Fog` (`Fog.mat`) | Fog sheets | **Detail**, **Water Side** (set by `WaterFogSprite`). |
| `SpookyWater/Sprite Lit Depth Write` (`SpriteLitDepthWrite.mat`) | Sprites the water must clip against | **Auto Normals** (as below), **Detail** (alpha clip). Writes depth so the 3D water clips into the sprite. |
| `SpookyWater/Sprite Lit Auto Normals` (`SpriteLitAutoNormals.mat`) | Scenery lit by normal-mapped lights (e.g. Water Glow Light) | **Auto Normals** (Edge Bevel, Edge Width, Detail Bumps, Detail Width, Normal Map Strength), **Detail** (textures, alpha clip, Depth Write). Drop-in for Sprite-Lit-Default. |
| `Hidden/SpookyWater/Ripple Simulation` (`RippleSimulation.mat`) | The simulation | Driven by the builder. |

Common looks:

- **Calmer water:** lower *Displacement Scale* on the builder or *Ripple Height* on the Surface material (and Front *Ripple Scale* to match); raise *Damping*.
- **Heavier, slower waves:** raise *Impact Radius*, lower *Speed*, lower *Damping*.
- **Clearer water:** Front material *Clarity*; Surface *Color* alpha.
- **Murkier water:** Front *Mist*, Surface *Underside* murk, Horizon *underwater fade*.
- **Far water seen from below doesn't match hazy background art:** set Horizon *Underwater Haze Color* to the haze painted on the far art (alpha 1), so the underside fades to it instead of the dark Outside-Window colour.
- **Glowing water (water as the light source in a dark scene):** raise *Glow Intensity* on both the Surface and Front materials, pick a *Glow Color*, and raise *Self Illumination* so 2D lights no longer darken it. On the Front, *Glow Depth* sets how far the bright band under the surface reaches and *Deep Glow* how much the whole body glows. The soft halo around the water comes from URP Bloom: add a Volume with Bloom (threshold about 1, intensity 0.5-1) and push the HDR glow above 1.
- **Beaches that absorb ripples:** *Shore Reflectivity* < 1.

## Menus reference

| Menu | Does |
|---|---|
| **GameObject > SpookyWater > Water** | Water level with Grid, builder, two tilemaps, starter pond, splash effects. |
| **GameObject > SpookyWater > Floating Platform / Floating Object** | Sprite + collider + `Rigidbody2D` + `WaterFloater`. |
| **GameObject > SpookyWater > Floating Debris** | A lily pad `WaterSurfaceDebris`. |
| **GameObject > SpookyWater > Ripple Obstacle** | A post with `WaterRippleObstacle`. |
| **GameObject > SpookyWater > Water Pocket** | A `WaterPocket` (bezier air pocket with its own water). |
| **GameObject > SpookyWater > Mouse Ripples** | `WaterMouseRipples` on the water camera (`Water.Camera`), or on a new object when there is none. |
| **GameObject > SpookyWater > Fog Sheet** | Fog sprite with `WaterFogSprite`. |
| **GameObject > SpookyWater > Light Shaft** | `WaterLightShaft`. |
| **GameObject > SpookyWater > Add To Selection > Swimmer / Floater (Platform) / Floater (Object) / Reflection / Ripple Obstacle / Light Shaft Occluder** | Adds the component (and its requirements) to the selected objects. |
| **GameObject > SpookyWater > Add To Selection > Glow Light** | A "Glow Light" child with `WaterGlowLight` + Point `Light2D`. |
| **GameObject > SpookyWater > Add To Selection > Camera Source** | Adds `WaterCameraSource` to the selected cameras. |
| **GameObject > SpookyWater > Add To Selection > Auto Normals Material** | Puts `SpriteLitAutoNormals.mat` on the Sprite-Lit-Default sprite/tilemap/SpriteShape renderers in the selection and its children. |
| **Tools > SpookyWater > Project Setup** | The setup window. |
| **Assets > Create > SpookyWater > Tile Palette** | A palette with the two water tiles. |
| **Tools > SpookyWater > Sprite Normal Baker** | Window that lists the sprite textures in the open scenes and bakes `_n` normal maps for them, assigned as `_NormalMap`. See [Sprite Normal Baker](#sprite-normal-baker). |
| **Assets > Create > SpookyWater > Sprite Normal Bake Profile** | A bake settings profile for the Sprite Normal Baker. |
| **Tools > SpookyWater > Convert Material To Depth Clipping** | Converts a sprite material so it clips against the water. |
| **Assets > Create > SpookyWater > Settings** | A settings asset. |
| **Project Settings > SpookyWater** | Settings asset + Open Project Setup. |

## Samples

`WaterPlayerController` controls: **A/D** move, **W/S** swim up/down, **Space** or **J** jump (also out of the water), **K** dash in water, **L** shoot up out of the water / slam straight down from the air. `WasdCameraMover`: **Q/E** forward/back, **Z/C** up/down, **Shift** faster.

Sample scripts (`SpookyWater.Samples` assembly; safe to delete):

- `WaterPlayerController`: land + water platformer controller (run, jump with coyote time and buffering, swim, buoyancy, dash, slam dive) driving a `WaterSwimmer`.
- `WasdCameraMover`: a test camera that can follow a target.
- `SampleInput`: wraps the old Input Manager and the Input System.

## Performance and tuning

| Cost | Knob |
|---|---|
| Ripple simulation (GPU) | *Texels Per Unit* (32), *Max Resolution* (1024), *Tick Rate* (60). Halving Texels Per Unit quarters the cost. |
| Reflection (one extra camera render per frame) | *Reflection Resolution Scale* (0.5). Only `WaterReflectable` objects (and the *Reflection Mask*) are rendered into it. |
| Vertices | *Mesh Detail* group: the near surface rows dominate (*Surface Near Segments* 20 x 20 per cell, *Surface Detailed Row Count* 3). |
| Draw calls / culling | *Chunk Size* (16): smaller = more, better-culled meshes. |
| Particles | `WaterSplashEffects` per-strength counts; *Ambient Clusters Per Second* (0 to disable). |
| Auto normals | 8 texture reads per pixel in the normals pass, only while a normal-mapped light (e.g. Water Glow Light) is visible. Turn the light's Normal Maps to Fast or Disabled on low-end targets. |
| Light shafts | One sheet each, but every pixel reads 28 texture samples (two layers x edge blur + glow); keep shafts no larger than needed. |

Mobile: start with Texels Per Unit 16, Max Resolution 512, Reflection Resolution Scale 0.25, Surface Near Segments 10 x 10, fewer droplets.

## Limits and caps

- One water level (`WaterTilemapBuilder`) per scene.
- The front tilemap and its Grid must be at world Z = 0 (the near edge of the surface).
- One reflection plane at the level's average surface height; ponds should share a surface height.
- The reflection texture is sized at setup from the main camera's pixel size and is not resized if the window changes.
- 32 ripple obstacles; more are ignored with one warning.
- 16 impulses per simulation tick; beyond that the oldest are dropped (the newest splash always shows).
- 64 contact wavelet sources (reflected sprites crossing the water plus obstacles), nearest the camera first.
- 16 near-front micro ripples at once.
- The simulation covers at most `Max Resolution / Texels Per Unit` world units (32 units by default); larger levels simulate a window following the camera, so far ripples aren't simulated. The shoreline mask is baked once for the whole level (up to 4096 px a side).
- Debris drift and its ripple kicks ignore shore bounces; kicks are replayed for a few seconds.
- A Sprite Shape moved by script at runtime is not followed by its reflection copy (disable and re-enable its `WaterReflectable`).
- Leave **Regenerate On Start** on.
- Water Pockets have no planar reflection, are not moved by moving the object at runtime, and need the 2D Renderer's depth/stencil buffer (they use stencil bit 128). Floating debris on a pocket draws at the lake's height.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Pink materials | Not on URP, or a shader failed to compile. Check the Console and Project Setup > URP with a 2D Renderer. |
| No water in Play mode | Check the Console (second builder, missing Front Tilemap, missing materials). Project Setup > Fix All. Make sure the tilemaps have painted cells and the Grid is at Z = 0. |
| Water is black or very dark | No Global Light 2D in the scene (Project Setup > Fix All Scene Items). |
| I only see a thin strip / no surface | The camera is orthographic or looking straight on. Use a perspective camera tilted slightly down. Make sure the Surface tilemap is painted and assigned. |
| Reflections missing | Add `WaterReflectable` to the objects. Check the `SpookyWater_Reflections` layer exists, the water has a camera (tagged `MainCamera`, a `WaterCameraSource`, or `Water.CameraResolver`) and it is above the water. |
| Reflections or the ripple window follow the wrong camera | Another camera is tagged `MainCamera`. Add `WaterCameraSource` to the right one, set `Water.CameraResolver`, or turn off *mainCameraFallback* in the settings asset. |
| Nothing is refracted behind the front glass | Camera Sorting Layer Texture off or bound to the wrong layer: run Project Setup. Sprites must be on sorting layers before `SpookyWater_Surface`. |
| One sprite is not refracted | It is on a layer after `SpookyWater_Surface` (e.g. Foreground). By design. |
| Sorting layers missing or out of order | Project Setup > Fix. |
| Splashes don't appear | Add one `WaterSplashEffects`. Check *Min Impact Speed*: slow crossings only ripple. |
| Bubbles or splashes hidden under the water | Their sorting layers/orders changed; restore the defaults in the settings asset. |
| Characters don't ripple the water | Needs `Rigidbody2D` + `Collider2D` on a layer in **Contact Layers**, crossing the top edge of Line cells. |
| Mouse clicks do nothing | Add Mouse Ripples; the Surface tilemap must exist (it provides the colliders); nothing else should block the ray. Turn on *Debug Log*. |
| Water edges don't meet at the front | Surface *Ripple Height* and Front *Ripple Scale* differ. Keep them equal. |
| A sprite pokes through the surface oddly | Give it `SpriteLitDepthWrite.mat` (or use the converter) so it clips. |
| "Two builders" warning | Only one level per scene. Paint more ponds on the same tilemaps. |
| A hard fog line across the reflection | `WaterReflectable` on a fog sprite. Remove it. |
| Horizon material changes have no effect | Reflection, fog and underwater values are copied from the Surface material on Generate. Edit those there. |
| Can't edit materials / sample scene not created | Package installed from Git is read-only. Duplicate materials into `Assets/`; use the `.unitypackage` for the sample. |
| Floater sinks or flies off | Rigidbody2D must be Dynamic with sensible mass; check *Float Depth* and that the floater sits over Line cells. |

Verbose ripple logging: builder > *Debug*. Visual debugging: Water Surface / Water Front materials > *Debug* (ripple heat map, click marker).

## FAQ

**Can I use it in a pure 2D (orthographic) game?** Yes, but you only see the front glass and waterline (with ripples, splashes, floaters). The top surface and its reflection need a perspective camera.

**Can the water level change at runtime?** Paint/erase tiles with `Tilemap.SetTile` and call `Water.Level.Generate()`. Regeneration rebuilds meshes and restarts the simulation, so do it sparingly (e.g. on a level event, not every frame).

**Can I have waves without anything touching the water?** Yes: *Ambient Ripples* and *Front Ripples* on the builder, or call `Water.Ripple` from a script. There are deliberately no constant sine waves.

**Does it work with Cinemachine?** Yes. Cinemachine moves a real camera; the water follows that camera (`Water.Camera`).

**My camera is spawned after the scene loads. Does that work?** Yes. See [Cameras from your own camera system](#cameras-from-your-own-camera-system).

**Do my objects need special layers?** No. Reflection and refraction are opt-in by component (`WaterReflectable`) and sorting layer order. Physics layers only matter for *Contact Layers*.

**Does it support multiple cameras / split screen?** The reflection follows one camera, `Water.Camera`.

## Credits

The demo project (`SpookyWaterDemo`) uses these third-party assets:

- **Parallax forest background** by Digital Moons: [Parallax Forest Background](https://digitalmoons.itch.io/parallax-forest-background) (itch.io).
- **Sara character** from Unity's [Lost Crypt - 2D Sample Project](https://assetstore.unity.com/packages/templates/packs/lost-crypt-2d-sample-project-158673) (Unity Asset Store).
