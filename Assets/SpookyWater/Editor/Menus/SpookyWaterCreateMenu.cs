using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace SpookyWater.Editor
{
    public static class SpookyWaterCreateMenu
    {
        private const string Root = "GameObject/SpookyWater/";
        private const string SelectedRoot = "GameObject/SpookyWater/Add To Selection/";
        private const int StarterColumns = 8;
        private const int StarterDepth = 3;
        private const int StarterSurfaceRows = 4;
        private const float SurfaceTilemapOffsetY = 3f;

        [MenuItem(Root + "Water", false, 0)]
        private static void MenuWater(MenuCommand command)
        {
            var existing = Object.FindAnyObjectByType<WaterTilemapBuilder>();
            if (existing)
            {
                var choice = EditorUtility.DisplayDialogComplex("Water already in this scene",
                    "A scene supports a single water level. Paint more ponds on the existing water's tilemaps instead of creating a second builder.",
                    "Select Existing", "Create Anyway", "Cancel");
                if (choice == 0)
                {
                    Selection.activeGameObject = existing.gameObject;
                    EditorGUIUtility.PingObject(existing);
                }
                if (choice != 1) return;
            }

            var group = BeginGroup();
            var water = CreateWater(ParentOf(command), PickPoint(command, false));
            EnsureSplashEffects(water.transform);
            EndGroup(group, "Create Water", water);
        }

        [MenuItem(Root + "Floating Platform", false, 20)]
        private static void MenuFloatingPlatform(MenuCommand command)
        {
            var group = BeginGroup();
            EndGroup(group, "Create Floating Platform", CreateFloater(ParentOf(command), PickPoint(command, true), WaterFloaterMode.Platform));
        }

        [MenuItem(Root + "Floating Object", false, 21)]
        private static void MenuFloatingObject(MenuCommand command)
        {
            var group = BeginGroup();
            EndGroup(group, "Create Floating Object", CreateFloater(ParentOf(command), PickPoint(command, true), WaterFloaterMode.Object));
        }

        [MenuItem(Root + "Floating Debris", false, 22)]
        private static void MenuFloatingDebris(MenuCommand command)
        {
            var group = BeginGroup();
            var point = PickPoint(command, true);
            point.z = 1.5f;
            EndGroup(group, "Create Floating Debris", CreateDebris(ParentOf(command), point));
        }

        [MenuItem(Root + "Ripple Obstacle", false, 23)]
        private static void MenuRippleObstacle(MenuCommand command)
        {
            var group = BeginGroup();
            EndGroup(group, "Create Ripple Obstacle", CreateRippleObstacle(ParentOf(command), PickPoint(command, true)));
        }

        [MenuItem(Root + "Water Pocket", false, 24)]
        private static void MenuWaterPocket(MenuCommand command)
        {
            var group = BeginGroup();
            var point = PickPoint(command, false);
            point.z = 0f;
            EndGroup(group, "Create Water Pocket", CreateWaterPocket(ParentOf(command), point));
        }

        [MenuItem(Root + "Mouse Ripples", false, 40)]
        private static void MenuMouseRipples()
        {
            var existing = Object.FindAnyObjectByType<WaterMouseRipples>();
            if (existing)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                return;
            }
            var group = BeginGroup();
            EndGroup(group, "Add Mouse Ripples", AddMouseRipples());
        }

        [MenuItem(Root + "Fog Sheet", false, 41)]
        private static void MenuFogSheet(MenuCommand command)
        {
            var group = BeginGroup();
            EndGroup(group, "Create Fog Sheet", CreateFogSheet(ParentOf(command), PickPoint(command, false)));
        }

        [MenuItem(Root + "Light Shaft", false, 42)]
        private static void MenuLightShaft(MenuCommand command)
        {
            var group = BeginGroup();
            var point = PickPoint(command, false);
            point.y += 4f;
            EndGroup(group, "Create Light Shaft", CreateLightShaft(ParentOf(command), point));
        }

        [MenuItem(SelectedRoot + "Swimmer", false, 60)]
        private static void MenuAddSwimmer()
        {
            AddToSelection("Add Swimmer", AddSwimmer);
        }

        [MenuItem(SelectedRoot + "Floater (Platform)", false, 61)]
        private static void MenuAddPlatform()
        {
            AddToSelection("Add Floater", go => AddFloater(go, WaterFloaterMode.Platform));
        }

        [MenuItem(SelectedRoot + "Floater (Object)", false, 62)]
        private static void MenuAddObject()
        {
            AddToSelection("Add Floater", go => AddFloater(go, WaterFloaterMode.Object));
        }

        [MenuItem(SelectedRoot + "Reflection", false, 63)]
        private static void MenuAddReflectable()
        {
            AddToSelection("Add Reflection", go => AddOnce<WaterReflectable>(go));
        }

        [MenuItem(SelectedRoot + "Ripple Obstacle", false, 64)]
        private static void MenuAddObstacle()
        {
            AddToSelection("Add Ripple Obstacle", go => AddOnce<WaterRippleObstacle>(go));
        }

        [MenuItem(SelectedRoot + "Light Shaft Occluder", false, 65)]
        private static void MenuAddLightShaftOccluder()
        {
            AddToSelection("Add Light Shaft Occluder", go => AddOnce<WaterLightShaftOccluder>(go));
        }

        [MenuItem(SelectedRoot + "Glow Light", false, 66)]
        private static void MenuAddGlowLight()
        {
            AddToSelection("Add Glow Light", AddGlowLight);
        }

        [MenuItem(SelectedRoot + "Camera Source", false, 68)]
        private static void MenuAddCameraSource()
        {
            AddToSelection("Add Water Camera Source", go =>
            {
                if (go.TryGetComponent<Camera>(out _)) AddOnce<WaterCameraSource>(go);
            });
        }

        [MenuItem(SelectedRoot + "Camera Source", true)]
        private static bool ValidateCameraSource()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go.TryGetComponent<Camera>(out _)) return true;
            }
            return false;
        }

        [MenuItem(SelectedRoot + "Auto Normals Material", false, 67)]
        private static void MenuAssignAutoNormals()
        {
            var material = SpookyWaterDefaults.Load<Material>(SpookyWaterDefaults.SpriteLitAutoNormalsMaterial);
            if (!material)
            {
                Debug.LogError("SpookyWater: Materials/Sprites/SpriteLitAutoNormals.mat is missing.");
                return;
            }

            var group = BeginGroup();
            int assigned = 0, skipped = 0;
            foreach (var go in Selection.gameObjects)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is SpriteRenderer || renderer is TilemapRenderer || renderer.GetType().Name == "SpriteShapeRenderer")) continue;
                    var current = renderer.sharedMaterial;
                    if (current == material) continue;
                    if (current && current.shader && current.shader.name != "Universal Render Pipeline/2D/Sprite-Lit-Default")
                    {
                        skipped++;
                        continue;
                    }
                    Undo.RecordObject(renderer, "Assign Auto Normals Material");
                    renderer.sharedMaterial = material;
                    assigned++;
                }
            }
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Assign Auto Normals Material");
            Debug.Log($"SpookyWater: Sprite Lit Auto Normals assigned to {assigned} renderer(s)." + (skipped > 0 ? $" {skipped} with other materials (e.g. Sprite Lit Depth Write, fog) were left alone. Sprite Lit Depth Write already has auto normals, so sprites that clip into the water need no change." : ""));
        }

        [MenuItem(SelectedRoot + "Glow Light", true)]
        [MenuItem(SelectedRoot + "Auto Normals Material", true)]
        private static bool ValidateLightingSelection()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Swimmer", true)]
        private static bool ValidateSwimmer()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Floater (Platform)", true)]
        private static bool ValidateFloaterPlatform()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Floater (Object)", true)]
        private static bool ValidateFloaterObject()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Reflection", true)]
        private static bool ValidateReflection()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Ripple Obstacle", true)]
        private static bool ValidateRippleObstacle()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(SelectedRoot + "Light Shaft Occluder", true)]
        private static bool ValidateLightShaftOccluder()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem("Assets/Create/SpookyWater/Tile Palette")]
        private static void MenuCreateTilePaletteAsset()
        {
            CreateTilePalette();
        }

        public static void CreateTilePalette()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Water Tile Palette", "WaterTilePalette", "prefab",
                "Choose where to save the palette. It will contain the Water Body and Water Surface tiles.");
            if (string.IsNullOrEmpty(path)) return;

            var folder = Path.GetDirectoryName(path).Replace('\\', '/');
            var paletteName = Path.GetFileNameWithoutExtension(path);
            var palette = GridPaletteUtility.CreateNewPalette(folder, paletteName, GridLayout.CellLayout.Rectangle,
                GridPalette.CellSizing.Automatic, new Vector3(1f, 1f, 0f), GridLayout.CellSwizzle.XYZ);
            if (!palette) return;

            var palettePath = AssetDatabase.GetAssetPath(palette);
            var contents = PrefabUtility.LoadPrefabContents(palettePath);
            var tilemap = contents.GetComponentInChildren<Tilemap>();
            if (tilemap)
            {
                tilemap.SetTile(new Vector3Int(0, 0, 0), SpookyWaterDefaults.Load<WaterTileData>(SpookyWaterDefaults.WaterBodyTile));
                tilemap.SetTile(new Vector3Int(1, 0, 0), SpookyWaterDefaults.Load<WaterTileData>(SpookyWaterDefaults.WaterSurfaceTile));
            }
            PrefabUtility.SaveAsPrefabAsset(contents, palettePath);
            PrefabUtility.UnloadPrefabContents(contents);

            Selection.activeObject = palette;
            EditorGUIUtility.PingObject(palette);
        }

        public static GameObject CreateWater(Transform parent, Vector3 position, bool paintStarterPond = true)
        {
            var root = new GameObject("Water", typeof(Grid), typeof(WaterTilemapBuilder));
            Place(root, parent, new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), 0f));
            root.GetComponent<Grid>().cellSize = Vector3.one;

            var front = CreateTilemap("Front Tilemap", root.transform);
            var surface = CreateTilemap("Surface Tilemap", root.transform);
            surface.transform.localPosition = new Vector3(0f, SurfaceTilemapOffsetY, 0f);

            var builder = root.GetComponent<WaterTilemapBuilder>();
            var so = new SerializedObject(builder);
            so.FindProperty("_tilemap").objectReferenceValue = front;
            so.FindProperty("_surfaceTilemap").objectReferenceValue = surface;
            SpookyWaterDefaults.AssignBuilderMaterials(so);
            so.FindProperty("_horizonDistance").floatValue = 12f;
            so.FindProperty("_displacementScale").floatValue = 0.25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (paintStarterPond) PaintStarterPond(front, surface);

            Undo.RegisterCreatedObjectUndo(root, "Create Water");
            return root;
        }

        public static void PaintStarterPond(Tilemap front, Tilemap surface)
        {
            var body = SpookyWaterDefaults.Load<WaterTileData>(SpookyWaterDefaults.WaterBodyTile);
            var top = SpookyWaterDefaults.Load<WaterTileData>(SpookyWaterDefaults.WaterSurfaceTile);
            if (!body || !top)
            {
                Debug.LogWarning("SpookyWater: the Water Body and Water Surface tile assets were not found, so the starter pond was not painted.");
                return;
            }

            for (var x = 0; x < StarterColumns; x++)
            {
                for (var y = 0; y > -StarterDepth; y--) front.SetTile(new Vector3Int(x, y, 0), body);
                for (var row = 0; row < StarterSurfaceRows; row++) surface.SetTile(new Vector3Int(x, row, 0), top);
            }
        }

        public static WaterSplashEffects EnsureSplashEffects(Transform water)
        {
            var existing = Object.FindAnyObjectByType<WaterSplashEffects>();
            if (existing) return existing;

            var go = new GameObject("Water Splash Effects");
            Place(go, water, water ? water.position : Vector3.zero);
            var splash = go.AddComponent<WaterSplashEffects>();
            var so = new SerializedObject(splash);
            SpookyWaterDefaults.AssignSplashMaterials(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(go, "Create Splash Effects");
            return splash;
        }

        public static Light2D EnsureGlobalLight()
        {
            foreach (var light in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            {
                if (light.lightType == Light2D.LightType.Global) return light;
            }

            var go = new GameObject("Global Light 2D");
            Place(go, null, Vector3.zero);
            var created = go.AddComponent<Light2D>();
            created.lightType = Light2D.LightType.Global;
            Undo.RegisterCreatedObjectUndo(go, "Create Global Light 2D");
            return created;
        }

        public static GameObject CreateSpriteBlock(string name, Transform parent, Vector3 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            Place(go, parent, position);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpookyWaterDefaults.Load<Sprite>(SpookyWaterDefaults.WaterBodySprite);
            sr.color = color;
            return go;
        }

        public static GameObject CreateFloater(Transform parent, Vector3 position, WaterFloaterMode mode)
        {
            var isPlatform = mode == WaterFloaterMode.Platform;
            var go = CreateSpriteBlock(isPlatform ? "Floating Platform" : "Floating Object", parent, position,
                isPlatform ? new Vector2(3f, 0.4f) : new Vector2(1f, 1f),
                isPlatform ? new Color(0.45f, 0.3f, 0.18f) : new Color(0.6f, 0.4f, 0.22f));
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<Rigidbody2D>();
            var floater = go.AddComponent<WaterFloater>();
            var so = new SerializedObject(floater);
            so.FindProperty("_mode").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<WaterReflectable>();
            Undo.RegisterCreatedObjectUndo(go, "Create Floater");
            return go;
        }

        public static GameObject CreateDebris(Transform parent, Vector3 position)
        {
            var go = new GameObject("Floating Debris");
            Place(go, parent, position);
            var debris = go.AddComponent<WaterSurfaceDebris>();
            var so = new SerializedObject(debris);
            SpookyWaterDefaults.AssignDebrisDefaults(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(go, "Create Floating Debris");
            return go;
        }

        public static GameObject CreateRippleObstacle(Transform parent, Vector3 position)
        {
            var go = CreateSpriteBlock("Ripple Obstacle", parent, position, new Vector2(0.4f, 2f), new Color(0.3f, 0.25f, 0.2f));
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<WaterRippleObstacle>();
            go.AddComponent<WaterReflectable>();
            Undo.RegisterCreatedObjectUndo(go, "Create Ripple Obstacle");
            return go;
        }

        public static GameObject CreateWaterPocket(Transform parent, Vector3 position)
        {
            var go = new GameObject("Water Pocket");
            Place(go, parent, position);
            go.AddComponent<WaterPocket>();
            Undo.RegisterCreatedObjectUndo(go, "Create Water Pocket");
            return go;
        }

        public static GameObject CreateFogSheet(Transform parent, Vector3 position)
        {
            var go = CreateSpriteBlock("Fog Sheet", parent, position, new Vector2(24f, 5f), new Color(0.75f, 0.85f, 0.95f, 0.4f));
            go.GetComponent<SpriteRenderer>().sharedMaterial = SpookyWaterDefaults.Load<Material>(SpookyWaterDefaults.FogMaterial);
            go.AddComponent<WaterFogSprite>();
            Undo.RegisterCreatedObjectUndo(go, "Create Fog Sheet");
            return go;
        }

        public static GameObject CreateLightShaft(Transform parent, Vector3 position)
        {
            var go = new GameObject("LightShaft");
            Place(go, parent, position);
            var shaft = go.AddComponent<WaterLightShaft>();
            var so = new SerializedObject(shaft);
            SpookyWaterDefaults.AssignLightShaftDefaults(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            Undo.RegisterCreatedObjectUndo(go, "Create Light Shaft");
            return go;
        }

        public static GameObject AddMouseRipples()
        {
            var camera = Water.Camera;
            if (camera)
            {
                Undo.AddComponent<WaterMouseRipples>(camera.gameObject);
                return camera.gameObject;
            }

            var go = new GameObject("Water Mouse Ripples");
            Place(go, null, Vector3.zero);
            go.AddComponent<WaterMouseRipples>();
            Undo.RegisterCreatedObjectUndo(go, "Add Mouse Ripples");
            return go;
        }

        public static void AddSwimmer(GameObject go)
        {
            if (go.GetComponent<WaterSwimmer>()) return;
            if (!go.GetComponent<Collider2D>()) Undo.AddComponent<BoxCollider2D>(go);
            Undo.AddComponent<WaterSwimmer>(go);
        }

        public static void AddGlowLight(GameObject go)
        {
            if (go.GetComponentInChildren<WaterGlowLight>(true)) return;
            var child = new GameObject("Glow Light");
            Undo.RegisterCreatedObjectUndo(child, "Add Glow Light");
            Undo.SetTransformParent(child.transform, go.transform, "Add Glow Light");
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            Undo.AddComponent<WaterGlowLight>(child);
        }

        public static void AddFloater(GameObject go, WaterFloaterMode mode)
        {
            if (go.GetComponent<WaterFloater>()) return;
            if (!go.GetComponent<Collider2D>()) Undo.AddComponent<BoxCollider2D>(go);
            var floater = Undo.AddComponent<WaterFloater>(go);
            var so = new SerializedObject(floater);
            so.FindProperty("_mode").enumValueIndex = (int)mode;
            so.ApplyModifiedProperties();
        }

        private static void AddOnce<T>(GameObject go) where T : Component
        {
            if (!go.GetComponent<T>()) Undo.AddComponent<T>(go);
        }

        private static void AddToSelection(string undoName, System.Action<GameObject> add)
        {
            var group = BeginGroup();
            foreach (var go in Selection.gameObjects) add(go);
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(undoName);
        }

        private static Tilemap CreateTilemap(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Tilemap>();
        }

        private static void Place(GameObject go, Transform parent, Vector3 position)
        {
            if (parent) go.transform.SetParent(parent, false);
            else StageUtility.PlaceGameObjectInCurrentStage(go);
            go.transform.position = position;
            GameObjectUtility.EnsureUniqueNameForSibling(go);
        }

        private static Transform ParentOf(MenuCommand command)
        {
            return command.context is GameObject go ? go.transform : null;
        }

        private static Vector3 PickPoint(MenuCommand command, bool onWater)
        {
            if (command.context is GameObject parent) return parent.transform.position;

            if (onWater && TryGetWaterTop(out var top)) return top;

            var view = SceneView.lastActiveSceneView;
            var pivot = view ? view.pivot : Vector3.zero;
            pivot.z = 0f;
            return pivot;
        }

        private static bool TryGetWaterTop(out Vector3 point)
        {
            point = default;
            var builder = Object.FindAnyObjectByType<WaterTilemapBuilder>();
            if (!builder || !builder.FrontTilemap || builder.FrontTilemap.GetUsedTilesCount() == 0) return false;

            var tilemap = builder.FrontTilemap;
            var bounds = tilemap.cellBounds;
            var left = tilemap.CellToWorld(new Vector3Int(bounds.xMin, bounds.yMax, 0));
            point = new Vector3(left.x + bounds.size.x * tilemap.cellSize.x * 0.5f, left.y + 0.5f, 0f);
            return true;
        }

        private static int BeginGroup()
        {
            Undo.IncrementCurrentGroup();
            return Undo.GetCurrentGroup();
        }

        private static void EndGroup(int group, string undoName, GameObject selected)
        {
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName(undoName);
            if (!selected) return;
            Selection.activeGameObject = selected;
            EditorGUIUtility.PingObject(selected);
        }
    }
}
