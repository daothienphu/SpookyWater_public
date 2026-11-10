using SpookyWater.Editor;
using UnityEditor;

namespace SpookyWater.Samples.Editor
{
    [CustomEditor(typeof(WaterPlayerController))]
    [CanEditMultipleObjects]
    public class WaterPlayerControllerEditor : GroupedInspector
    {
    }
}
