#if UNITY_EDITOR
/**
 * Dev1CoreTestSceneBuilder: Dựng Scene kiểm thử độc lập "Dev1_CoreTest.unity" cho Thành viên 1.
 * [Chức năng]: Tạo hierarchy sạch gồm 5 nhóm gốc (_ENVIRONMENT_SYSTEM_, _LIGHTING_&_ATMOSPHERE_,
 *              _STATIC_ENVIRONMENT_, _DYNAMIC_WATER_, _POOLED_PROP_SHIPS_) + wire TimeManager,
 *              TideController, AtmosphereSkyBridge, FloatingMarketSpawner, EnvironmentHUD để test
 *              chu kỳ ngày/đêm + thủy triều + sinh ghe NPC độc lập với scene chính của Dev 2.
 * [Dependencies]: TimeManager (Application), AtmosphericProfileSO (Infrastructure),
 *                 TideController/AtmosphereSkyBridge/FloatingMarketSpawner (Environment),
 *                 EnvironmentHUD (UI), UnityEditor.
 */

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChoNoi.Application;
using ChoNoi.Infrastructure;
using ChoNoi.Presentation.Environment;
using ChoNoiMienTay.UI;

namespace ChoNoiMienTay.Editor
{
    public static class Dev1CoreTestSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Dev1_CoreTest.unity";
        private const string ProfilePath = "Assets/_Project/ScriptableObjects/RiverMarket/EnvironmentProfile.asset";

        // Model nền (ghe NPC + nông sản mẫu) — tái dùng asset model_mau có sẵn.
        private static readonly string[] BoatModelPaths =
        {
            "Assets/_Project/Art/model_mau/taubanhang/hủ tiếu/hủ tiếu (1).glb",
            "Assets/_Project/Art/model_mau/tauchokhach/tauchokhach (1).glb",
            "Assets/_Project/Art/model_mau/taubanhang/ghe tạp hóa/ghe tạp hóa (1).glb",
        };
        private static readonly string[] FruitModelPaths =
        {
            "Assets/_Project/Art/model_mau/thunghang/khom+cam/khom+cam (1).glb",
            "Assets/_Project/Art/model_mau/thunghang/duahau+dudu/duahau+dudu (1).glb",
            "Assets/_Project/Art/model_mau/thunghang/xoai+dua/xoai+dua (1).glb",
        };

        private const int RiverBedLayer = 1; // khớp BoatController.riverbedLayer = 1 << 1

        [MenuItem("ChoNoi/Scenes/Build Dev1 Core Test")]
        public static void BuildDev1CoreTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Dev1CoreTestSceneBuilder] Stop Play Mode truoc khi build scene test.");
                return;
            }

            Directory.CreateDirectory("Assets/_Project/Scenes");
            AtmosphericProfileSO profile = EnsureProfile();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureCamera();

            // ── _LIGHTING_&_ATMOSPHERE_ ─────────────────────────────
            GameObject lightingRoot = new GameObject("_LIGHTING_&_ATMOSPHERE_");
            Light directionalLight = Object.FindAnyObjectByType<Light>();
            if (directionalLight != null)
                directionalLight.transform.SetParent(lightingRoot.transform, true);

            // ── _DYNAMIC_WATER_ ─────────────────────────────────────
            GameObject waterRoot = new GameObject("_DYNAMIC_WATER_");
            GameObject water = BuildWater(waterRoot.transform, profile);
            Collider[] mudflats = BuildMudflats(waterRoot.transform);

            // ── _STATIC_ENVIRONMENT_ ────────────────────────────────
            GameObject staticRoot = new GameObject("_STATIC_ENVIRONMENT_");
            BuildGroundAndDock(staticRoot.transform);

            // ── _ENVIRONMENT_SYSTEM_ ────────────────────────────────
            GameObject systemRoot = new GameObject("_ENVIRONMENT_SYSTEM_");
            TimeManager timeManager = systemRoot.AddComponent<TimeManager>();

            TideController tide = systemRoot.AddComponent<TideController>();
            SetPrivate(tide, "timeManager", timeManager);
            SetPrivate(tide, "profile", profile);
            SetPrivate(tide, "waterTransform", water.transform);
            SetPrivate(tide, "mudflatColliders", mudflats);
            SetPrivate(tide, "groundingThreshold", 0.6f);

            // AtmosphereSkyBridge nằm trong nhóm Lighting (điều khiển Sun + khí quyển + màu nước).
            AtmosphereSkyBridge sky = lightingRoot.AddComponent<AtmosphereSkyBridge>();
            SetPrivate(sky, "timeManager", timeManager);
            SetPrivate(sky, "profile", profile);
            SetPrivate(sky, "directionalLight", directionalLight);
            SetPrivate(sky, "waterRenderer", water.GetComponent<Renderer>());

            // ── _POOLED_PROP_SHIPS_ ─────────────────────────────────
            GameObject shipsRoot = new GameObject("_POOLED_PROP_SHIPS_");
            shipsRoot.transform.position = new Vector3(0f, 2f, 0f);
            FloatingMarketSpawner spawner = shipsRoot.AddComponent<FloatingMarketSpawner>();
            SetPrivate(spawner, "boatPrefabs", LoadModels(BoatModelPaths));
            SetPrivate(spawner, "fruitPrefabs", LoadModels(FruitModelPaths));
            SetPrivate(spawner, "boatCount", 8);
            SetPrivate(spawner, "areaSize", new Vector3(36f, 0f, 36f));
            SetPrivate(spawner, "waterY", 2f);
            SetPrivate(spawner, "spawnOnStart", true);

            // ── UI debug môi trường (để xem giá trị khi Play) ───────
            EnvironmentHUD hud = systemRoot.AddComponent<EnvironmentHUD>();
            hud.Configure(timeManager, profile, null, null);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Dev1CoreTestSceneBuilder] Da dung scene: {ScenePath}");
        }

        [MenuItem("ChoNoi/Scenes/Build Dev1 Core Test", true)]
        private static bool ValidateBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static GameObject BuildWater(Transform parent, AtmosphericProfileSO profile)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "WaterSurface";
            water.transform.SetParent(parent, true);
            water.transform.position = new Vector3(0f, 2f, 0f);
            water.transform.localScale = new Vector3(8f, 1f, 8f);

            MeshCollider meshCol = water.GetComponent<MeshCollider>();
            if (meshCol != null) meshCol.enabled = false;

            Renderer renderer = water.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material mat = new Material(shader) { color = new Color(0.30f, 0.26f, 0.18f, 1f) };
            renderer.sharedMaterial = mat;
            return water;
        }

        // Bãi bùn ngầm (layer RiverBed, tắt sẵn) — TideController bật khi nước rút.
        private static Collider[] BuildMudflats(Transform parent)
        {
            GameObject root = new GameObject("Mudflats");
            root.transform.SetParent(parent, true);

            Vector3[] spots =
            {
                new Vector3(-8f, 0.4f, 6f),
                new Vector3(9f, 0.4f, -5f),
                new Vector3(2f, 0.4f, 11f),
            };

            var colliders = new System.Collections.Generic.List<Collider>();
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject mud = new GameObject($"Mudflat_{i}");
                mud.transform.SetParent(root.transform, true);
                mud.transform.position = spots[i];
                mud.layer = RiverBedLayer;
                BoxCollider box = mud.AddComponent<BoxCollider>();
                box.size = new Vector3(8f, 1.6f, 8f);
                box.enabled = false;
                colliders.Add(box);
            }
            return colliders.ToArray();
        }

        private static void BuildGroundAndDock(Transform parent)
        {
            // Đáy sông / nền đất (layer RiverBed) để ghe có thể chạm khi nước rút.
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "RiverBed";
            ground.transform.SetParent(parent, true);
            ground.transform.position = new Vector3(0f, -1f, 0f);
            ground.transform.localScale = new Vector3(90f, 1.5f, 90f);
            ground.layer = RiverBedLayer;
            Paint(ground, new Color(0.28f, 0.22f, 0.15f));

            // Bến gỗ ven bờ.
            GameObject dock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dock.name = "Dock";
            dock.transform.SetParent(parent, true);
            dock.transform.position = new Vector3(-20f, 2.2f, -18f);
            dock.transform.localScale = new Vector3(10f, 0.4f, 4f);
            Paint(dock, new Color(0.45f, 0.32f, 0.18f));
        }

        private static AtmosphericProfileSO EnsureProfile()
        {
            AtmosphericProfileSO profile = AssetDatabase.LoadAssetAtPath<AtmosphericProfileSO>(ProfilePath);
            if (profile != null) return profile;

            // Nếu chưa có (chưa chạy RiverMarket builder), tạo mới với cấu hình thủy triều tối thiểu.
            Directory.CreateDirectory("Assets/_Project/ScriptableObjects/RiverMarket");
            profile = ScriptableObject.CreateInstance<AtmosphericProfileSO>();
            SetPrivate(profile, "maxWaterHeight", 3f);
            SetPrivate(profile, "minWaterHeight", 0.2f);
            SetPrivate(profile, "waterLevelOverDay", new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.5f, 0.3f), new Keyframe(0.7f, 0.1f), new Keyframe(1f, 1f)));
            SetPrivate(profile, "sunIntensityCurve", new AnimationCurve(
                new Keyframe(0.21f, 0.4f), new Keyframe(0.5f, 1.5f), new Keyframe(0.79f, 0.1f)));
            SetPrivate(profile, "fogDensityOverDay", new AnimationCurve(
                new Keyframe(0.2f, 0.04f), new Keyframe(0.45f, 0.005f), new Keyframe(1f, 0.04f)));
            AssetDatabase.CreateAsset(profile, ProfilePath);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static GameObject[] LoadModels(string[] paths)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (string path in paths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null) list.Add(model);
            }
            return list.ToArray();
        }

        private static void ConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(0f, 14f, -26f);
            cam.transform.rotation = Quaternion.Euler(26f, 0f, 0f);
            cam.fieldOfView = 60f;
        }

        private static void Paint(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
#endif
