using System;
using UnityEngine;

namespace SpookyWater
{
    /// <summary>
    /// Per-body settings for crossing the waterline, read by the contact bands of the water it crosses.
    /// </summary>
    [Serializable]
    public class WaterCrossing
    {
        [Tooltip("Use the ripple settings below for this body instead of the ones on the water it crosses " +
                 "(the builder's or pocket's Contact Ripple).")]
        public bool overrideRipple;

        [Tooltip("Ripple settings used for this body when Override Ripple is on.")]
        public ContactRippleSettings ripple = new();

        [Tooltip("Multiplies the size of the splash particles this body throws when it falls in or comes out " +
                 "(1 = as set on WaterSplashEffects, 0 = no particles).")]
        [Min(0f)] public float splashScale = 1f;

        public ContactRippleSettings RippleOr(ContactRippleSettings fallback) => overrideRipple ? ripple : fallback;
    }

    /// <summary>
    /// Implemented by components that carry a <see cref="WaterCrossing"/> for their Rigidbody2D.
    /// </summary>
    public interface IWaterCrossingBody
    {
        WaterCrossing Crossing { get; }
    }

    [AddComponentMenu("SpookyWater/Water Crossing")]
    [HelpURL(SpookyWaterDocs.Splashes)]
    [DisallowMultipleComponent]
    public class WaterCrossingBody : MonoBehaviour, IWaterCrossingBody
    {
        [SerializeField] private WaterCrossing _crossing = new();

        public WaterCrossing Crossing => _crossing;
    }
}
