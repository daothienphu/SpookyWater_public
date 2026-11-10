#if UNITY_EDITOR
using UnityEditor;

namespace SpookyWater.Editor
{
    [CustomEditor(typeof(WaterLightShaftOccluder))]
    [CanEditMultipleObjects]
    public class WaterLightShaftOccluderEditor : GroupedInspector
    {
    }
}
#endif
