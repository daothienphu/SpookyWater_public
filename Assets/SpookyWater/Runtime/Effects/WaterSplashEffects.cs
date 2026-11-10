using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Splash Effects")]
    [HelpURL(SpookyWaterDocs.SplashEffects)]
    [DisallowMultipleComponent]
    public class WaterSplashEffects : MonoBehaviour
    {
        [Group("Materials")]
        [Tooltip("Level-wide particle layer on top of the ripple simulation: splashes, surface spray, wake spray, rising bubbles that pop at the waterline and ambient bubbles in on-screen water. Keep one per scene. Material for droplets and spray: SpookyWater/Water Particle with Shape = Droplet.")]
        [SerializeField] private Material _dropletMaterial;
        [Tooltip("Material for spray mist: SpookyWater/Water Particle with Shape = Puff.")]
        [SerializeField] private Material _mistMaterial;
        [Tooltip("Material for the sheet of water a splash throws up: SpookyWater/Water Particle with Shape = Crown.")]
        [SerializeField] private Material _crownMaterial;
        [Tooltip("Material for bubbles: SpookyWater/Water Particle with Shape = Bubble.")]
        [SerializeField] private Material _bubbleMaterial;
        [Tooltip("Material for the little rings droplets and bubbles leave where they meet the surface: SpookyWater/Water Particle with Shape = Ring.")]
        [SerializeField] private Material _ringMaterial;

        [Group("Impacts")]
        [Tooltip("Vertical speed in world units per second that makes a strength-1 splash. Anything crossing the waterline splashes in proportion to its speed.")]
        [SerializeField, Min(0.01f)] private float _referenceImpactSpeed = 10f;
        [Tooltip("Crossings slower than this (world units per second) do not splash at all, though they still ripple.")]
        [SerializeField, Min(0f)] private float _minImpactSpeed = 1.2f;
        [Tooltip("Splash strength multiplier for a body leaving the water, such as jumping out.")]
        [SerializeField, Min(0f)] private float _exitStrengthScale = 1f;
        [Tooltip("Largest splash strength. Crown height grows linearly with strength, so this caps how high a slam can splash.")]
        [SerializeField, Min(0.1f)] private float _maxStrength = 2.5f;

        [Group("Splash Crown")]
        [Tooltip("Width and height in world units of the crown sheet thrown up by a strength-1 splash. Height scales with the strength (so with the impact speed), width with its square root.")]
        [SerializeField] private Vector2 _crownSize = new(1.9f, 1.0f);
        [Tooltip("Seconds a crown lives before it has fallen back into the surface.")]
        [MinMaxRange(0.1f, 3f)] [SerializeField] private Vector2 _crownLifetime = new(0.65f, 0.85f);
        [Tooltip("Crowns per splash, a random number in this range: a main one plus smaller or wider ones layered around it, each with its own random lean, wall heights and lip.")]
        [MinMaxRange(1f, 4f)] [SerializeField] private Vector2Int _crownCount = new(2, 3);
        [Tooltip("Body color of the crown, strands and droplets. Their rims and foam use the Foam Color set on the materials.")]
        [SerializeField] private Color _waterColor = new(0.38f, 0.78f, 0.86f, 0.9f);

        [Group("Splash Strands")]
        [Tooltip("Big globs of water flung from the crown. Strands per unit of splash strength. 0 = none.")]
        [SerializeField, Min(0f)] private float _strandsPerStrength = 16f;
        [Tooltip("Size range of a strand in world units.")]
        [MinMaxRange(0.02f, 1f)] [SerializeField] private Vector2 _strandSize = new(0.12f, 0.26f);
        [Tooltip("Their launch speed as a share of the droplets', so they stay closer to the crown.")]
        [SerializeField, Min(0f)] private float _strandSpeedScale = 0.6f;
        [Tooltip("How much they stretch along their motion: long streaks while fast, beads at the top. 0 = round.")]
        [SerializeField, Min(0f)] private float _strandStretch = 0.07f;
        [Tooltip("Tiny droplets kicked up where a strand lands back on the water, a random count in this range. 0 = none.")]
        [MinMaxRange(0f, 10f)] [SerializeField] private Vector2Int _strandLandingSpray = new(2, 5);

        [Group("Splash Droplets")]
        [Tooltip("Droplets per unit of splash strength. 0 = none.")]
        [SerializeField, Min(0f)] private float _dropletsPerStrength = 60f;
        [Tooltip("Launch speed range at strength 1, in world units per second. It scales with the square root of the strength.")]
        [MinMaxRange(0f, 20f)] [SerializeField] private Vector2 _dropletSpeed = new(2.5f, 6.5f);
        [Tooltip("Angle range in degrees from straight up at which droplets leave the crown's rim, outward.")]
        [MinMaxRange(0f, 90f)] [SerializeField] private Vector2 _dropletAngle = new(8f, 50f);
        [Tooltip("How much of the impacting body's horizontal speed the droplets carry. 0 = none, 1 = all of it.")]
        [SerializeField, Range(0f, 1f)] private float _dropletInheritVelocity = 0.35f;
        [Tooltip("Speed into and out of the screen, in world units per second, for depth.")]
        [SerializeField, Min(0f)] private float _dropletDepthSpeed = 0.5f;
        [Tooltip("Droplet size range in world units. Most droplets are near the small end; a few reach the large end.")]
        [MinMaxRange(0.01f, 0.5f)] [SerializeField] private Vector2 _dropletSize = new(0.035f, 0.16f);
        [Tooltip("Gravity on droplets, strands and spray as a multiple of the physics gravity.")]
        [SerializeField, Min(0.01f)] private float _dropletGravity = 1.6f;
        [Tooltip("Width in world units of the tiny ring a droplet leaves where it lands back on the water. 0 = none.")]
        [SerializeField, Min(0f)] private float _dropletLandingRingSize = 0.25f;

        [Group("Droplet Ripples")]
        [Tooltip("Chance that a droplet or strand makes a real ripple in the water simulation where it lands, so a splash leaves small rings scattered around the big one. 0 = none.")]
        [Range(0f, 1f)] [SerializeField] private float _dropletRippleChance = 0.5f;
        [Tooltip("Ripple strength for the smallest and largest droplet. Strands use the large end.")]
        [MinMaxRange(0f, 1f)] [SerializeField] private Vector2 _dropletRippleStrength = new(0.06f, 0.25f);
        [Tooltip("Ripple radius, as a multiple of the water's impact radius, for the smallest and largest droplet.")]
        [MinMaxRange(0.05f, 2f)] [SerializeField] private Vector2 _dropletRippleRadius = new(0.3f, 0.7f);
        [Tooltip("Most droplet ripples started per frame. The simulation takes only 16 impulses per tick and drops the oldest past that, so a burst of landings must not push out bigger ones. Landings over the limit are skipped.")]
        [SerializeField, Min(0)] private int _maxDropletRipplesPerFrame = 3;

        [Group("Splash Mist and Foam")]
        [Tooltip("Faint mist puffs per unit of splash strength. 0 = none.")]
        [SerializeField, Min(0f)] private float _mistPerStrength = 5f;
        [Tooltip("Mist puff size range in world units.")]
        [MinMaxRange(0.05f, 3f)] [SerializeField] private Vector2 _mistSize = new(0.6f, 1.3f);
        [Tooltip("Seconds a mist puff lives.")]
        [MinMaxRange(0.1f, 3f)] [SerializeField] private Vector2 _mistLifetime = new(0.7f, 1.3f);
        [Tooltip("Color and opacity of the mist.")]
        [SerializeField] private Color _mistColor = new(0.85f, 0.97f, 1f, 0.22f);
        [Tooltip("Ring height divided by width for the little landing and pop rings. It flattens them so they lie on the surface.")]
        [Range(0.05f, 1f)] [SerializeField] private float _foamRingFlatten = 0.28f;
        [Tooltip("Color of the little landing and pop rings.")]
        [SerializeField] private Color _foamColor = new(0.92f, 0.98f, 1f, 0.9f);

        [Group("Wake Spray")]
        [Tooltip("Spray for fast movers along the surface, such as a dash: small, low droplets peeling off the mover's sides that barely move in the world, so it leaves them behind in a thin trail. Droplets per world unit travelled at full intensity.")]
        [SerializeField, Min(0f)] private float _wakeDropletsPerUnit = 9f;
        [Tooltip("Upward launch speed at full intensity, in world units per second. Low, so the spray hugs the water.")]
        [MinMaxRange(0f, 10f)] [SerializeField] private Vector2 _wakeLift = new(1f, 2.7f);
        [Tooltip("World speed along the direction of travel, negative = backwards. Near zero, so the droplets peel off the mover's sides and it leaves them behind.")]
        [MinMaxRange(-5f, 5f)] [SerializeField] private Vector2 _wakeDrift = new(-1f, 0.8f);
        [Tooltip("How far back from the leading edge, in world units, droplets peel off. About the mover's length.")]
        [SerializeField, Min(0f)] private float _wakeLength = 0.5f;

        [Group("Plunge Bubbles")]
        [Tooltip("A body that fell in trails bubbles along its path down for a moment. Bubbles per world unit it travels underwater, at splash strength 1. 0 = none.")]
        [SerializeField, Min(0f)] private float _plungeBubblesPerUnit = 30f;
        [Tooltip("Seconds after entering the water that the trail lasts, thinning out to nothing.")]
        [SerializeField, Min(0.01f)] private float _plungeTrailDuration = 1.2f;
        [Tooltip("Radius in world units of the trail around the body's path.")]
        [SerializeField, Min(0f)] private float _plungeTrailSpread = 0.25f;

        [Group("Bubbles")]
        [Tooltip("Rise speed range in world units per second. Bigger bubbles rise faster.")]
        [MinMaxRange(0.1f, 5f)] [SerializeField] private Vector2 _bubbleRiseSpeed = new(0.9f, 1.8f);
        [Tooltip("Bubble size range in world units.")]
        [MinMaxRange(0.01f, 0.5f)] [SerializeField] private Vector2 _bubbleSize = new(0.03f, 0.09f);
        [Tooltip("Side-to-side wobble while rising. 0 = straight up.")]
        [SerializeField, Min(0f)] private float _bubbleWobble = 0.6f;
        [Tooltip("Color and opacity of bubbles.")]
        [SerializeField] private Color _bubbleColor = new(0.85f, 0.97f, 1f, 1f);
        [Tooltip("Width in world units of the little ring a bubble leaves as it pops at the surface. 0 = none.")]
        [SerializeField, Min(0f)] private float _popRingSize = 0.16f;

        [Group("Ambient Bubbles")]
        [Tooltip("Bubble clusters per second, spread over the water in view. 0 = no ambient bubbles.")]
        [SerializeField, Min(0f)] private float _ambientClustersPerSecond = 9f;
        [Tooltip("Bubbles in one cluster, a random count in this range.")]
        [MinMaxRange(1f, 12f)] [SerializeField] private Vector2Int _ambientClusterSize = new(2, 7);
        [Tooltip("World Z range ambient bubbles spawn in. The front glass is at the water tilemap's Z; use most of the water's depth so they are scattered through it rather than in one sheet.")]
        [MinMaxRange(0f, 10f)] [SerializeField] private Vector2 _ambientDepthZ = new(0.1f, 4.5f);
        [Tooltip("Opacity of a bubble at the far end of Ambient Depth Z, relative to one at the front glass. Applies to any bubble, not only ambient ones: farther ones fade into the water.")]
        [Range(0f, 1f)] [SerializeField] private float _bubbleFarOpacity = 0.35f;

        private static WaterSplashEffects _instance;

        private ParticleSystem _crowns;
        private ParticleSystem _strands;
        private ParticleSystem _droplets;
        private ParticleSystem _wakeDroplets;
        private ParticleSystem _mist;
        private ParticleSystem _bubbles;
        private float _ambientTimer;

        private class Plunge
        {
            public Rigidbody2D Body;
            public Vector3 Last;
            public float StartTime;
            public float Strength;
            public float Accumulator;
        }

        private readonly List<Plunge> _plunges = new();

        private struct PendingRipple
        {
            public float Time;
            public Vector3 Position;
            public float Strength;
            public float RadiusScale;
        }

        private readonly List<PendingRipple> _pendingRipples = new();

        public static WaterSplashEffects Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<WaterSplashEffects>();
                return _instance;
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _dropletMaterial = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.SplashDropletMaterial);
            _mistMaterial = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.SplashMistMaterial);
            _crownMaterial = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.SplashCrownMaterial);
            _bubbleMaterial = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.BubbleMaterial);
            _ringMaterial = WaterEditorDefaults.Load<Material>(WaterEditorDefaults.FoamRingMaterial);
        }
#endif

        private float Gravity => Mathf.Abs(Physics.gravity.y) * _dropletGravity;

        private void Awake()
        {
            _instance = this;

            _crowns = CreateSystem("SplashCrowns", _crownMaterial, 300, 1, true);
            AnchorAtBottom(_crowns);
            SetSizeOverLifetime(_crowns,
                new AnimationCurve(new Keyframe(0f, 0.45f, 4f, 4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.2f)),
                new AnimationCurve(new Keyframe(0f, 0.1f, 6f, 6f), new Keyframe(0.3f, 1f, 0f, 0f), new Keyframe(1f, 0f, -2.2f, -2.2f)));
            SetFade(_crowns, 0.02f, 1f);

            _strands = CreateSystem("SplashStrands", _dropletMaterial, 600, 2, true);
            var strandRenderer = _strands.GetComponent<ParticleSystemRenderer>();
            strandRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            strandRenderer.velocityScale = _strandStretch;
            strandRenderer.lengthScale = 1f;
            var strandMain = _strands.main;
            strandMain.gravityModifier = _dropletGravity;
            SetSizeOverLifetime(_strands, AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
            if (_strandLandingSpray.y > 0) AddLandingSpray(_strands);
            if (_dropletLandingRingSize > 0f) AddRingSubEmitter(_strands, "StrandLandings", _dropletLandingRingSize * 1.6f, 1);

            _droplets = CreateSystem("SplashDroplets", _dropletMaterial, 3000, 2, true);
            _droplets.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Stretch;
            _droplets.GetComponent<ParticleSystemRenderer>().velocityScale = 0.012f;
            _droplets.GetComponent<ParticleSystemRenderer>().lengthScale = 1f;
            var dropletMain = _droplets.main;
            dropletMain.gravityModifier = _dropletGravity;
            SetSizeOverLifetime(_droplets, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));
            if (_dropletLandingRingSize > 0f) AddRingSubEmitter(_droplets, "DropletLandings", _dropletLandingRingSize, 1);

            _wakeDroplets = CreateSystem("WakeDroplets", _dropletMaterial, 600, 2, true);
            var wakeRenderer = _wakeDroplets.GetComponent<ParticleSystemRenderer>();
            wakeRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            wakeRenderer.velocityScale = 0.012f;
            wakeRenderer.lengthScale = 1f;
            var wakeMain = _wakeDroplets.main;
            wakeMain.gravityModifier = _dropletGravity;
            SetSizeOverLifetime(_wakeDroplets, AnimationCurve.Linear(0f, 1f, 1f, 0.7f));

            _mist = CreateSystem("SplashMist", _mistMaterial ? _mistMaterial : _dropletMaterial, 400, 3, true);
            SetSizeOverLifetime(_mist, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));
            SetFade(_mist, 0.2f, 0.4f);

            _bubbles = CreateSystem("Bubbles", _bubbleMaterial, 2500, 0);
            SetSizeOverLifetime(_bubbles, AnimationCurve.Linear(0f, 0.7f, 1f, 1.15f));
            SetFade(_bubbles, 0.08f, 1f);
            var noise = _bubbles.noise;
            noise.enabled = true;
            noise.separateAxes = true;
            noise.strengthX = _bubbleWobble;
            noise.strengthY = 0f;
            noise.strengthZ = _bubbleWobble * 0.3f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;
            if (_popRingSize > 0f) AddRingSubEmitter(_bubbles, "BubblePops", _popRingSize, 1);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            UpdatePlunges();
            UpdateDropletRipples();
            if (_ambientClustersPerSecond <= 0f) return;

            _ambientTimer -= Time.deltaTime;
            while (_ambientTimer <= 0f)
            {
                _ambientTimer += Random.Range(0.3f, 1.7f) / _ambientClustersPerSecond;
                SpawnAmbientCluster();
            }
        }

        public void Impact(Vector3 position, float surfaceY, Vector2 velocity, float sizeScale = 1f)
        {
            if (TryGetImpactStrength(velocity, sizeScale, out var strength)) Splash(position, surfaceY, strength, velocity);
        }

        public void Impact(Rigidbody2D body, float surfaceY, float sizeScale = 1f) =>
            Impact(body, surfaceY, body.linearVelocity, sizeScale);

        public void Impact(Rigidbody2D body, float surfaceY, Vector2 velocity, float sizeScale)
        {
            if (!TryGetImpactStrength(velocity, sizeScale, out var strength)) return;

            Splash(body.transform.position, surfaceY, strength, velocity);
            if (velocity.y < 0f) StartPlunge(body, strength);
        }

        public void StartPlunge(Rigidbody2D body, float strength)
        {
            var plunge = _plunges.Find(p => p.Body == body);
            if (plunge == null) _plunges.Add(plunge = new Plunge { Body = body });

            plunge.Last = PlungePoint(body);
            plunge.StartTime = Time.time;
            plunge.Strength = Mathf.Clamp(strength, 0f, _maxStrength);
            plunge.Accumulator = 0f;
        }

        private static Vector3 PlungePoint(Rigidbody2D body)
        {
            var center = body.worldCenterOfMass;
            return new Vector3(center.x, center.y, body.transform.position.z);
        }

        private bool TryGetImpactStrength(Vector2 velocity, float sizeScale, out float strength)
        {
            strength = 0f;
            var speed = Mathf.Abs(velocity.y);
            if (speed < _minImpactSpeed) return false;

            strength = speed / Mathf.Max(_referenceImpactSpeed, 0.01f) * sizeScale;
            if (velocity.y > 0f) strength *= _exitStrengthScale;
            return true;
        }

        public void Splash(Vector3 position, float surfaceY, float strength, Vector2 velocity)
        {
            strength = Mathf.Clamp(strength, 0f, _maxStrength);
            if (strength <= 0.01f) return;

            var origin = new Vector3(position.x, surfaceY, position.z);
            var speedScale = Mathf.Sqrt(strength);
            var crownWidth = _crownSize.x * speedScale;
            var crownHeight = _crownSize.y * strength;

            var lifetimeScale = Mathf.Lerp(0.8f, 1.2f, speedScale - 0.5f);

            var crowns = Random.Range(_crownCount.x, _crownCount.y + 1);
            for (var i = 0; i < crowns; i++)
            {
                var size = new Vector2(crownWidth, crownHeight);
                var lifetime = RandomRange(_crownLifetime) * lifetimeScale;
                var offset = new Vector3(Random.Range(-0.08f, 0.08f) * crownWidth, 0f, Random.Range(-0.05f, 0.05f));
                if (i == 1)
                {
                    size = Vector2.Scale(size, new Vector2(Random.Range(0.45f, 0.65f), Random.Range(0.95f, 1.15f)));
                    lifetime *= 0.7f;
                    offset.z -= 0.08f;
                }
                else if (i >= 2)
                {
                    size = Vector2.Scale(size, new Vector2(Random.Range(1.2f, 1.5f), Random.Range(0.4f, 0.6f)));
                    lifetime *= 1.25f;
                    offset.z += 0.15f;
                }
                else size = Vector2.Scale(size, new Vector2(Random.Range(0.85f, 1.15f), Random.Range(0.9f, 1.1f)));
                EmitSheet(_crowns, origin + offset, size, lifetime);
            }

            var bias = Random.Range(-0.35f, 0.35f) + Mathf.Clamp(velocity.x * 0.05f, -0.3f, 0.3f);
            var amount = strength * Random.Range(0.7f, 1.3f);
            var drift = velocity.x * _dropletInheritVelocity;
            EmitDroplets(_droplets, origin, crownWidth * 0.45f, Mathf.RoundToInt(_dropletsPerStrength * amount),
                _dropletSpeed * speedScale * Random.Range(0.85f, 1.15f), _dropletAngle, drift, _dropletSize, bias);
            EmitDroplets(_strands, origin, crownWidth * 0.4f, Mathf.RoundToInt(_strandsPerStrength * amount),
                _dropletSpeed * (speedScale * _strandSpeedScale), _dropletAngle, drift, _strandSize, bias);

            var mistCount = Mathf.CeilToInt(_mistPerStrength * strength);
            for (var i = 0; i < mistCount; i++)
            {
                var offset = new Vector3(Random.Range(-0.5f, 0.5f) * crownWidth, Random.Range(0.1f, 0.6f) * speedScale, Random.Range(-0.1f, 0.3f));
                Emit(_mist, origin + offset,
                    new Vector3(Random.Range(-0.4f, 0.4f) + velocity.x * 0.1f, Random.Range(0.2f, 0.8f) * speedScale, 0f),
                    Random.Range(_mistSize.x, _mistSize.y) * speedScale, RandomRange(_mistLifetime), _mistColor);
            }

        }

        public void Spray(Vector3 position, float surfaceY, float amount, Vector2 velocity)
        {
            if (amount <= 0f) return;

            var origin = new Vector3(position.x, surfaceY, position.z);
            amount = Mathf.Min(amount, 3f);
            var scale = Mathf.Sqrt(amount);
            var back = velocity.x > 0f ? -0.7f : velocity.x < 0f ? 0.7f : 0f;
            if (Random.value < 0.6f)
            {
                EmitSheet(_crowns, origin + new Vector3(0f, 0f, Random.Range(-0.05f, 0.05f)),
                    new Vector2(_crownSize.x * 0.3f * scale * Random.Range(0.8f, 1.2f), _crownSize.y * 0.22f * Mathf.Min(amount, 1.5f) * Random.Range(0.8f, 1.2f)),
                    RandomRange(_crownLifetime) * 0.55f);
            }
            EmitDroplets(_strands, origin, 0.12f, Mathf.RoundToInt(Random.Range(1f, 2.5f) * amount),
                _dropletSpeed * (0.3f * scale), new Vector2(10f, 45f), -velocity.x * 0.3f, _strandSize * 0.6f, back);
            EmitDroplets(_droplets, origin, 0.15f, Mathf.Max(1, Mathf.RoundToInt(8f * amount)),
                _dropletSpeed * (0.35f * scale), new Vector2(5f, 45f), -velocity.x * 0.25f, _dropletSize, back);
        }

        public void Wake(Vector3 from, Vector3 to, float surfaceY, float intensity)
        {
            intensity = Mathf.Clamp01(intensity);
            var distance = Mathf.Abs(to.x - from.x);
            if (intensity <= 0.01f || distance <= 0.0001f) return;

            var dir = Mathf.Sign(to.x - from.x);
            var lift = Mathf.Sqrt(intensity);
            var droplets = RandomCount(_wakeDropletsPerUnit * distance * intensity);
            for (var i = 0; i < droplets; i++)
            {
                var sizeT = Mathf.Pow(Random.value, 3f);
                var start = Vector3.Lerp(from, to, Random.value);
                start.x -= dir * Random.Range(0f, _wakeLength);
                start.y = surfaceY + Random.Range(0f, 0.05f);
                start.z += Random.Range(-0.12f, 0.12f);
                var launch = new Vector3(dir * RandomRange(_wakeDrift),
                    RandomRange(_wakeLift) * lift * Mathf.Lerp(1f, 0.7f, sizeT), Random.Range(-1f, 1f) * _dropletDepthSpeed);
                EmitFalling(_wakeDroplets, start, launch, Mathf.Lerp(_dropletSize.x, _dropletSize.y * 0.5f, sizeT), surfaceY, -1f);
            }
        }

        public void Bubbles(Vector3 position, int count, float spread, float driftX = 0f, float sizeScale = 1f)
        {
            BubbleTrail(position, position, count, spread, driftX, sizeScale);
        }

        public void BubbleTrail(Vector3 from, Vector3 to, int count, float spread, float driftX = 0f, float sizeScale = 1f)
        {
            for (var i = 0; i < count; i++)
            {
                var point = Vector3.Lerp(from, to, Random.value) + (Vector3)(Random.insideUnitCircle * spread);
                if (Water.TryGetSurfaceY(point, out var surfaceY)) EmitBubble(point, surfaceY, driftX + Random.Range(-0.2f, 0.2f), sizeScale);
            }
        }

        private void UpdatePlunges()
        {
            for (var i = _plunges.Count - 1; i >= 0; i--)
            {
                var plunge = _plunges[i];
                var age = Time.time - plunge.StartTime;
                if (!plunge.Body || age > _plungeTrailDuration)
                {
                    _plunges.RemoveAt(i);
                    continue;
                }

                var position = PlungePoint(plunge.Body);
                var fade = 1f - age / Mathf.Max(_plungeTrailDuration, 0.01f);
                plunge.Accumulator += Vector3.Distance(plunge.Last, position) * _plungeBubblesPerUnit * plunge.Strength * fade;
                var count = Mathf.FloorToInt(plunge.Accumulator);
                if (count > 0)
                {
                    plunge.Accumulator -= count;
                    BubbleTrail(plunge.Last, position, count, _plungeTrailSpread * Mathf.Lerp(0.5f, 1f, fade), 0f,
                        Mathf.Lerp(0.8f, 1.2f, fade));
                }
                plunge.Last = position;
            }
        }

        private void EmitDroplets(ParticleSystem system, Vector3 origin, float halfWidth, int count, Vector2 speedRange,
            Vector2 angleRange, float driftX, Vector2 sizeRange, float sideBias)
        {
            for (var i = 0; i < count; i++)
            {
                var side = Random.value < 0.5f + sideBias * 0.5f ? 1f : -1f;
                var along = Random.Range(0.3f, 1f);
                var angle = side * Mathf.Lerp(angleRange.x, angleRange.y, along * Random.Range(0.6f, 1f)) * Mathf.Deg2Rad;
                var sizeT = Mathf.Pow(Random.value, 3f);
                var size = Mathf.Lerp(sizeRange.x, sizeRange.y, sizeT);
                var speed = Random.Range(speedRange.x, speedRange.y) * Mathf.Lerp(1f, 0.6f, sizeT);
                var velocity = new Vector3(Mathf.Sin(angle) * speed + driftX,
                    Mathf.Cos(angle) * speed, Random.Range(-_dropletDepthSpeed, _dropletDepthSpeed));
                var start = origin + new Vector3(side * along * halfWidth, Random.Range(0.05f, 0.25f) * (1f - along * 0.5f), Random.Range(-0.1f, 0.1f));
                EmitFalling(system, start, velocity, size, origin.y, system == _strands ? 1f : sizeT);
            }
        }

        private void EmitFalling(ParticleSystem system, Vector3 start, Vector3 velocity, float size, float surfaceY, float rippleSizeT)
        {
            var g = Gravity;
            var h = start.y - surfaceY;
            var lifetime = (velocity.y + Mathf.Sqrt(Mathf.Max(velocity.y * velocity.y + 2f * g * h, 0f))) / g;
            if (lifetime <= 0.02f) return;

            Emit(system, start, velocity, size, lifetime, _waterColor);
            if (rippleSizeT >= 0f) ScheduleLandingRipple(start + velocity * lifetime, lifetime, rippleSizeT);
        }

        private static int RandomCount(float expected)
        {
            var count = Mathf.FloorToInt(expected);
            return count + (Random.value < expected - count ? 1 : 0);
        }

        private void ScheduleLandingRipple(Vector3 landing, float delay, float sizeT)
        {
            if (_dropletRippleChance <= 0f || Random.value > _dropletRippleChance) return;

            _pendingRipples.Add(new PendingRipple
            {
                Time = Time.time + delay,
                Position = landing,
                Strength = Mathf.Lerp(_dropletRippleStrength.x, _dropletRippleStrength.y, sizeT),
                RadiusScale = Mathf.Lerp(_dropletRippleRadius.x, _dropletRippleRadius.y, sizeT),
            });
        }

        private void UpdateDropletRipples()
        {
            if (_pendingRipples.Count == 0) return;

            var started = 0;
            for (var i = _pendingRipples.Count - 1; i >= 0; i--)
            {
                var ripple = _pendingRipples[i];
                if (ripple.Time > Time.time) continue;

                _pendingRipples.RemoveAt(i);
                if (started >= _maxDropletRipplesPerFrame) continue;

                var position = ripple.Position;
                var field = Water.FieldAt(position);
                if (!field) continue;
                position.z = Mathf.Max(position.z, 0.05f);
                field.CreateContactRippleAt(position, ripple.Strength, false, ripple.RadiusScale);
                started++;
            }
        }

        private void EmitSheet(ParticleSystem system, Vector3 origin, Vector2 size, float lifetime)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = origin,
                velocity = Vector3.zero,
                startSize3D = new Vector3(size.x, size.y, 1f),
                startLifetime = lifetime,
                startColor = _waterColor,
            };
            system.Emit(emit, 1);
        }

        private void EmitBubble(Vector3 point, float surfaceY, float driftX, float sizeScale)
        {
            var rise = RandomRange(_bubbleRiseSpeed);
            var lifetime = (surfaceY - point.y) / rise;
            if (lifetime <= 0.05f) return;

            var size = RandomRange(_bubbleSize) * sizeScale;
            rise *= Mathf.Lerp(0.85f, 1.2f, Mathf.InverseLerp(_bubbleSize.x, _bubbleSize.y, size));
            lifetime = (surfaceY - point.y) / rise;
            var color = _bubbleColor;
            color.a *= Mathf.Lerp(1f, _bubbleFarOpacity, Mathf.InverseLerp(_ambientDepthZ.x, _ambientDepthZ.y, point.z));
            Emit(_bubbles, point, new Vector3(driftX, rise, 0f), size, Mathf.Min(lifetime, 12f), color);
        }

        private void SpawnAmbientCluster()
        {
            var cam = Water.Camera;
            if (!cam || !Water.Level) return;

            var z = RandomRange(_ambientDepthZ);
            var distance = z - cam.transform.position.z;
            if (distance <= 0f) return;

            var min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            var max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, distance));
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var point = new Vector3(Random.Range(min.x, max.x), Random.Range(min.y, max.y), z);
                if (!Water.TryGetSurfaceY(point, out var surfaceY)) continue;

                var count = Random.Range(_ambientClusterSize.x, _ambientClusterSize.y + 1);
                var spacing = Random.Range(0.12f, 0.35f);
                for (var i = 0; i < count; i++)
                {
                    var p = point + new Vector3(Random.Range(-0.05f, 0.05f), -i * spacing, 0f);
                    EmitBubble(p, surfaceY, Random.Range(-0.1f, 0.1f), Random.Range(0.6f, 1f));
                }
                return;
            }
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
            };
            system.Emit(emit, 1);
        }

        private static float RandomRange(Vector2 range) => Random.Range(range.x, range.y);

        private ParticleSystem CreateSystem(string systemName, Material material, int maxParticles, int orderOffset,
            bool aboveWater = false)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = aboveWater ? SpookyWaterSettings.Instance.FrontSortingLayerId : SpookyWaterSettings.Instance.SurfaceSortingLayerId;
            renderer.sortingOrder = (aboveWater ? SpookyWaterSettings.Instance.spraySortingOrder : SpookyWaterSettings.Instance.bubbleSortingOrder) + orderOffset;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 10f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX,
            });

            system.Play();
            return system;
        }

        private void AddRingSubEmitter(ParticleSystem parent, string systemName, float ringSize, int orderOffset)
        {
            var ring = CreateSystem(systemName, _ringMaterial, 800, orderOffset);
            ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ring.transform.SetParent(parent.transform, false);

            var main = ring.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(ringSize * 0.6f, ringSize * 1.2f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(ringSize * 0.6f * _foamRingFlatten, ringSize * 1.2f * _foamRingFlatten);
            main.startSizeZ = 1f;
            main.startColor = _foamColor;
            var emission = ring.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            SetSizeOverLifetime(ring, new AnimationCurve(new Keyframe(0f, 0.2f, 3f, 3f), new Keyframe(1f, 1f)));
            SetFade(ring, 0.02f);

            var subEmitters = parent.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(ring, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private void AddLandingSpray(ParticleSystem parent)
        {
            var spray = CreateSystem("StrandLandingSpray", _dropletMaterial, 1500, 2, true);
            spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            spray.transform.SetParent(parent.transform, false);

            var main = spray.main;
            main.loop = false;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.gravityModifier = _dropletGravity;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(_dropletSize.x * 0.6f, _dropletSize.x * 1.2f);
            main.startColor = _waterColor;
            var shape = spray.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var emission = spray.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)_strandLandingSpray.x, (short)_strandLandingSpray.y) });

            var subEmitters = parent.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(spray, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        private static void AnchorAtBottom(ParticleSystem system)
        {
            system.GetComponent<ParticleSystemRenderer>().pivot = new Vector3(0f, 0.5f, 0f);
        }

        private static void SetSizeOverLifetime(ParticleSystem system, AnimationCurve x, AnimationCurve y)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, x);
            size.y = new ParticleSystem.MinMaxCurve(1f, y);
            size.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
        }

        private static void SetSizeOverLifetime(ParticleSystem system, AnimationCurve curve)
        {
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetFade(ParticleSystem system, float fadeIn, float fadeOutStart = 0.55f)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, Mathf.Max(fadeOutStart, fadeIn)), new GradientAlphaKey(fadeOutStart >= 1f ? 1f : 0f, 1f),
                });
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
        }
    }
}
