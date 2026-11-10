using UnityEngine;

namespace SpookyWater
{
    [AddComponentMenu("")]
    public class WaterTile : MonoBehaviour
    {
        public WaterCellKind CellKind { get; private set; }
        public WaterField OwningField { get; private set; }

        public void Initialize(WaterCellKind cellKind, WaterField owningField)
        {
            CellKind = cellKind;
            OwningField = owningField;
        }
    }
}
