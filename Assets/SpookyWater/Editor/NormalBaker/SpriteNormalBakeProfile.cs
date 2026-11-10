using System.Collections.Generic;
using UnityEngine;

namespace SpookyWater.Editor
{
    public enum SpriteNormalBevelShape
    {
        Round,
        Smooth,
        Linear
    }

    [CreateAssetMenu(menuName = "SpookyWater/Sprite Normal Bake Profile", fileName = "SpriteNormalBakeProfile", order = 20)]
    [HelpURL(SpookyWaterDocs.SpriteNormalBaker)]
    public class SpriteNormalBakeProfile : ScriptableObject
    {
        [Group("Sources")]
        [Tooltip("Sprite textures baked with this profile. Each gets a normal map next to it, named after it plus the suffix. Sprite sheets are baked per sprite, so neighbours never bleed into each other's bevel.")]
        [SerializeField] private List<Texture2D> _textures = new();

        [Tooltip("Appended to the source file name for the baked normal map (Hero.png -> Hero_n.png). Must not be empty.")]
        [SerializeField] private string _suffix = "_n";

        [Tooltip("Assigns the baked map as the sprite's _NormalMap secondary texture, which Sprite-Lit-Default and 2D lights with Normal Maps enabled read. Off: only the file is written.")]
        [SerializeField] private bool _assignToSprite = true;

        [Group("Shape")]
        [Tooltip("Pixels with alpha below this are empty space; the outline is the border between them and the rest.")]
        [Range(0.01f, 1f)] [SerializeField] private float _alphaThreshold = 0.5f;

        [Tooltip("Cross-section of the bevel from the outline inward. Round: a quarter circle, a soft pillow with a steep rim. Smooth: eases in and out, softer rim. Linear: a straight chamfer.")]
        [SerializeField] private SpriteNormalBevelShape _bevelShape = SpriteNormalBevelShape.Round;

        [Tooltip("How far in from the outline the bevel reaches, in pixels of the source image. Past it the sprite is flat. Grow it with the art's resolution (a few percent of the sprite's size).")]
        [Range(0.5f, 128f)] [SerializeField] private float _bevelWidth = 12f;

        [Tooltip("Height of the bevel relative to its width. 1 = as tall as it is wide (a rounded form), lower = flatter, higher = a steeper rim.")]
        [Range(0f, 4f)] [SerializeField] private float _bevelHeight = 1f;

        [Tooltip("Blur applied to the shape before taking normals, in pixels. Removes the crease along the middle of thin shapes and stair-steps on pixel outlines.")]
        [Range(0f, 32f)] [SerializeField] private float _shapeSmoothing = 2f;

        [Tooltip("Treat the texture's (or each sprite's) border as an outline. Turn off for tiles and textures that run off the edge, so they stay flat there and tile seamlessly.")]
        [SerializeField] private bool _bevelTextureBorders = true;

        [Group("Inner Edges")]
        [Tooltip("Depth of the grooves along lines and colour changes drawn inside the sprite, so an arm over a body or a plank in a fence rounds on its own. 0 = the whole sprite is one shape.")]
        [Range(0f, 1f)] [SerializeField] private float _innerEdges = 0.4f;

        [Tooltip("How strong a brightness change must be to count as an inner edge (0-1 brightness step). Lower finds faint lines too, higher only hard outlines.")]
        [Range(0.01f, 1f)] [SerializeField] private float _innerEdgeThreshold = 0.2f;

        [Tooltip("How far the rounding next to an inner edge reaches, in pixels.")]
        [Range(0.5f, 64f)] [SerializeField] private float _innerEdgeWidth = 4f;

        [Group("Detail")]
        [Tooltip("Surface texture from the painting: brighter areas stand out of darker ones. Only local brightness changes count (wider than Detail Width is ignored), so painted shadows don't become dents.")]
        [Range(0f, 16f)] [SerializeField] private float _detailBumps = 2f;

        [Tooltip("Size of the brightness features read as bumps, in pixels. Small: grain and brush strokes. Large: broad painted forms.")]
        [Range(0.5f, 64f)] [SerializeField] private float _detailWidth = 4f;

        [Tooltip("Blur applied to the brightness before reading detail and inner edges, in pixels. Raise it for noisy or dithered art.")]
        [Range(0f, 8f)] [SerializeField] private float _detailSoftness = 0.5f;

        [Tooltip("Dark areas stand out instead of bright ones. For art whose highlights are dark, or line art.")]
        [SerializeField] private bool _invertDetail;

        [Group("Output")]
        [Tooltip("Multiplies every slope. Below 1 flattens the whole map, above 1 exaggerates it.")]
        [Range(0f, 4f)] [SerializeField] private float _normalStrength = 1f;

        [Tooltip("Mirror the horizontal component, if lights from the left shade the right side.")]
        [SerializeField] private bool _flipX;

        [Tooltip("Mirror the vertical component. Unity expects Y up (OpenGL style); flip for engines or tools that expect DirectX style.")]
        [SerializeField] private bool _flipY;

        [Tooltip("Pixels past the outline filled with the nearest edge normal, so texture filtering and mipmaps don't blend the rim with a flat normal.")]
        [Range(0, 32)] [SerializeField] private int _padding = 4;

        public List<Texture2D> Textures => _textures;
        public string Suffix => _suffix;
        public bool AssignToSprite => _assignToSprite;
        public float AlphaThreshold => _alphaThreshold;
        public SpriteNormalBevelShape BevelShape => _bevelShape;
        public float BevelWidth => _bevelWidth;
        public float BevelHeight => _bevelHeight;
        public float ShapeSmoothing => _shapeSmoothing;
        public bool BevelTextureBorders => _bevelTextureBorders;
        public float InnerEdges => _innerEdges;
        public float InnerEdgeThreshold => _innerEdgeThreshold;
        public float InnerEdgeWidth => _innerEdgeWidth;
        public float DetailBumps => _detailBumps;
        public float DetailWidth => _detailWidth;
        public float DetailSoftness => _detailSoftness;
        public bool InvertDetail => _invertDetail;
        public float NormalStrength => _normalStrength;
        public bool FlipX => _flipX;
        public bool FlipY => _flipY;
        public int Padding => _padding;
    }
}
