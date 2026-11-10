using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("SpookyWater/Water Camera Source")]
    [HelpURL(SpookyWaterDocs.CameraSource)]
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class WaterCameraSource : MonoBehaviour
    {
        [Tooltip("The camera the water follows while this component is enabled: the reflection camera mirrors it, the ripple simulation window and contact-line culling follow its view, ambient bubbles spawn in it and mouse ripples aim through it. Empty = the Camera on this GameObject. Put this component on your gameplay camera, or on the camera prefab your camera system spawns, so no MainCamera tag is needed. When several are enabled, the most recently enabled one wins; Water.SetCamera overrides them all.")]
        [SerializeField] private Camera _camera;

        private Camera _ownCamera;

        public Camera TargetCamera
        {
            get
            {
                if (_camera) return _camera;
                if (!_ownCamera) TryGetComponent(out _ownCamera);
                return _ownCamera;
            }
            set
            {
                _camera = value;
                if (isActiveAndEnabled) Water.RefreshCamera();
            }
        }

        private void OnEnable()
        {
            Water.RegisterCameraSource(this);
        }

        private void OnDisable()
        {
            Water.UnregisterCameraSource(this);
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Water.RefreshCamera();
        }
    }
}
