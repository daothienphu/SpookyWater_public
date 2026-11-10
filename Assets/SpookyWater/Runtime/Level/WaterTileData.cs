using UnityEngine;
using UnityEngine.Tilemaps;

namespace SpookyWater
{
    public enum WaterCellKind
    {
        Surface = 0,

        Body = 1,

        Line = 2
    }

    [CreateAssetMenu(fileName = "WaterTileData", menuName = "SpookyWater/Water Tile")]
    public class WaterTileData : Tile
    {
        [Tooltip("Which kind of water this tile paints. Surface goes on the surface tilemap (the flat XZ top of " +
                 "the water). Body goes on the front tilemap (the XY cross-section). Line is derived " +
                 "automatically from front cells that have open air above them, so do not pick it here.")]
        public WaterCellKind CellKind;
    }
}
