#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using ChoNoi.Application;
using ChoNoi.Infrastructure;
using ChoNoi.Presentation;
using ChoNoi.Presentation.Environment;
using ChoNoi.Presentation.NPC;
using ChoNoi.Presentation.Player;
using ChoNoiMienTay.Infrastructure;
using ChoNoiMienTay.Presentation;
using ChoNoiMienTay.UI;

namespace ChoNoiMienTay.Editor
{
    public static class RiverMarketSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Core/RiverMarketScene.unity";
        private const string UpgradeCatalogPath = "Assets/_Project/ScriptableObjects/RiverMarket/BoatUpgradeCatalog.asset";
        private const string NewsDatabasePath = "Assets/_Project/ScriptableObjects/RiverMarket/MarketNewsDatabase.asset";
        private const string EnvironmentProfilePath = "Assets/_Project/ScriptableObjects/RiverMarket/EnvironmentProfile.asset";
        private const string BoatStatsPath = "Assets/_Project/ScriptableObjects/RiverMarket/RiverBoatStats.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const string MerchantAvatarPath = "Assets/_Project/Art/Avatars/merchant.png";
        private const string VillagerAvatarPath = "Assets/_Project/Art/Avatars/villager.png";
        private const string BoatModelPath = "Assets/_Project/Art/Models/thuyencoban.fbx";
        private const string NpcModelPath = "Assets/_Project/Art/Models/nvatdemo.fbx";
        private const string BargainingItemsFolder = "Assets/_Project/ScriptableObjects/Bargaining/Items";
        private const string TerrainArtFolder = "Assets/_Project/Art/Terrain";
        private const string GrassTexturePath = TerrainArtFolder + "/StylizedGrass_Texture.asset";
        private const string MudTexturePath = TerrainArtFolder + "/MekongMud_Texture.asset";
        private const string GrassLayerPath = TerrainArtFolder + "/StylizedGrass_TerrainLayer.terrainlayer";
        private const string MudLayerPath = TerrainArtFolder + "/MekongMud_TerrainLayer.terrainlayer";
        private const float TerrainWorldHeight = 18f;
        private const float TerrainWorldWidth = 240f;
        private const float TerrainWorldDepth = 220f;
        private const float WaterSurfaceY = 3.35f;
        private const float TerrainBaseHeight = 0.23f;
        private const float RiverBedHeight = 0.015f;
        private const float MainRiverHalfWidth = 18f;
        private const float ForkRiverHalfWidth = 14f;
        private const float BankBlendWidth = 38f;

        [MenuItem("ChoNoi/Scenes/Build River Market Scene")]
        public static void BuildScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[RiverMarketSceneBuilder] Stop Play Mode before rebuilding the River Market scene.");
                return;
            }

            EnsureFolders();

            BoatUpgradeCatalogSO upgradeCatalog = EnsureUpgradeCatalog();
            MarketNewsDatabaseSO newsDatabase = EnsureNewsDatabase();
            AtmosphericProfileSO environmentProfile = EnsureEnvironmentProfile();
            BoatStats boatStats = EnsureBoatStats();
            List<ItemData> marketItems = LoadMarketItems();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureMainCamera();

            GameObject worldRoot = new GameObject("RiverMarketWorld");
            GameObject terrainObj = BuildTerrainAndRiver(worldRoot.transform);
            Terrain terrain = terrainObj.GetComponent<Terrain>();
            GameObject waterPlane = BuildWater(worldRoot.transform);
            BuildObstacles(worldRoot.transform);
            BuildNpcBoats(worldRoot.transform);
            BuildRiverLife(worldRoot.transform, terrain);
            BuildEnvironmentAssets(worldRoot.transform, terrain);
            BuildAmbientNpcCrowd(worldRoot.transform, terrain);
            BuildStiltHouses(worldRoot.transform, terrain);
            BuildFloatingMarketCrowd(worldRoot.transform);

            GameObject systemsRoot = new GameObject("GameSystems");
            TimeManager timeManager = systemsRoot.AddComponent<TimeManager>();
            // Mở scene vào ~6h sáng: bình minh sương ấm, nắng nghiêng — khớp ảnh chợ nổi.
            SetPrivate(timeManager, "startHour", 6f);
            PlayerStats playerStats = systemsRoot.AddComponent<PlayerStats>();
            InventoryManager inventoryManager = systemsRoot.AddComponent<InventoryManager>();
            EconomyManager economyManager = systemsRoot.AddComponent<EconomyManager>();
            DurabilityManager durabilityManager = systemsRoot.AddComponent<DurabilityManager>();
            MarketNewsController marketNewsController = systemsRoot.AddComponent<MarketNewsController>();
            SaveLoadManager saveLoadManager = systemsRoot.AddComponent<SaveLoadManager>();

            GameObject boat = BuildBoat(worldRoot.transform, boatStats, waterPlane.transform);
            GameObject shorePlayer = BuildShorePlayer(worldRoot.transform, boat);
            BoatFollowCamera followCamera = AttachFollowCamera(shorePlayer.transform);
            BoatCampManager boatCampManager = boat.AddComponent<BoatCampManager>();
            BambooPoleManager bambooPoleManager = boat.AddComponent<BambooPoleManager>();
            boatCampManager.GetType().GetField("upgradeCatalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boatCampManager, upgradeCatalog);
            boatCampManager.GetType().GetField("inventoryManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boatCampManager, inventoryManager);
            boatCampManager.GetType().GetField("playerStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boatCampManager, playerStats);
            boatCampManager.GetType().GetField("boatController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(boatCampManager, boat.GetComponent<BoatController>());
            bambooPoleManager.GetType().GetField("boatCampManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(bambooPoleManager, boatCampManager);
            bambooPoleManager.GetType().GetField("inventoryManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(bambooPoleManager, inventoryManager);

            SetupBoatVisualModules(boatCampManager, boat.transform);
            SetupBoardingFlow(shorePlayer, boat, followCamera);

            // AtmosphereSkyBridge: nội suy ánh sáng/sương mù/ambient/màu nước theo giờ (thay EnvironmentController).
            AtmosphereSkyBridge skyBridge = systemsRoot.AddComponent<AtmosphereSkyBridge>();
            SetPrivate(skyBridge, "timeManager", timeManager);
            SetPrivate(skyBridge, "profile", environmentProfile);
            SetPrivate(skyBridge, "directionalLight", Object.FindAnyObjectByType<Light>());
            SetPrivate(skyBridge, "waterRenderer", waterPlane.GetComponent<Renderer>());

            // TideController sở hữu mực nước (water-Y) + bãi cạn động (mudflat colliders).
            TideController tideController = systemsRoot.AddComponent<TideController>();
            tideController.GetType().GetField("timeManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tideController, timeManager);
            tideController.GetType().GetField("profile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tideController, environmentProfile);
            tideController.GetType().GetField("waterTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(tideController, waterPlane.transform);

            // Bãi cạn động: collider bãi bùn ngầm bật khi nước rút dưới ngưỡng (Thuỷ triều - Phase 3).
            Collider[] mudflats = BuildMudflats(worldRoot.transform);
            SetPrivate(tideController, "mudflatColliders", mudflats);
            SetPrivate(tideController, "groundingThreshold", 2.5f);

            marketNewsController.Configure(timeManager, newsDatabase);
            ConfigureSaveLoad(saveLoadManager, playerStats, inventoryManager, boatCampManager, bambooPoleManager, durabilityManager, timeManager, marketItems);

            RiverMarketHUD hud = systemsRoot.AddComponent<RiverMarketHUD>();
            hud.Configure(timeManager, playerStats, inventoryManager, boatCampManager, marketNewsController, economyManager, durabilityManager, marketItems);

            // UI Môi trường: panel hiển thị giờ/phase/thuỷ triều/sương mù + nhiên liệu/thể lực + nút tốc độ thời gian.
            EnvironmentHUD environmentHud = systemsRoot.AddComponent<EnvironmentHUD>();
            environmentHud.Configure(timeManager, environmentProfile, boatStats, playerStats);

            Selection.activeGameObject = worldRoot;
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Đặt RiverMarketScene làm scene 0 trong Build Settings + để là scene active
            // -> mở Unity lên là thấy ngay môi trường chợ nổi.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("ChoNoi/Scenes/Build River Market Scene", true)]
        private static bool ValidateBuildScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory("Assets/_Project/Scenes/Core");
            Directory.CreateDirectory("Assets/_Project/ScriptableObjects/RiverMarket");
            Directory.CreateDirectory(TerrainArtFolder);
        }

        private static List<ItemData> LoadMarketItems()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { BargainingItemsFolder });
            List<ItemData> items = new List<ItemData>();
            foreach (string guid in guids)
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item != null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private static BoatUpgradeCatalogSO EnsureUpgradeCatalog()
        {
            BoatUpgradeCatalogSO catalog = AssetDatabase.LoadAssetAtPath<BoatUpgradeCatalogSO>(UpgradeCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BoatUpgradeCatalogSO>();
                AssetDatabase.CreateAsset(catalog, UpgradeCatalogPath);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            FillUpgradeArray(serialized.FindProperty("storageTiers"), new (string, int, float)[] {
                ("Mo rong khoang nho", 18000, 25f),
                ("Mo rong khoang lon", 28000, 35f),
            });
            FillEngineArray(serialized.FindProperty("engineTiers"), new (string, int, float)[] {
                ("May duoi tom cap 1", 22000, 1.18f),
                ("May duoi tom cap 2", 34000, 1.35f),
            });
            FillRoofArray(serialized.FindProperty("roofTiers"), new (string, int)[] {
                ("Lop mai", 15000),
            });
            FillBambooArray(serialized.FindProperty("bambooPoleTiers"), new (string, int, float)[] {
                ("Cay Beo rong 1", 12000, 0.08f),
                ("Cay Beo rong 2", 16000, 0.10f),
            });
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static MarketNewsDatabaseSO EnsureNewsDatabase()
        {
            MarketNewsDatabaseSO database = AssetDatabase.LoadAssetAtPath<MarketNewsDatabaseSO>(NewsDatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<MarketNewsDatabaseSO>();
                AssetDatabase.CreateAsset(database, NewsDatabasePath);
            }

            Sprite merchantAvatar = AssetDatabase.LoadAssetAtPath<Sprite>(MerchantAvatarPath);
            Sprite villagerAvatar = AssetDatabase.LoadAssetAtPath<Sprite>(VillagerAvatarPath);

            SerializedObject serialized = new SerializedObject(database);
            SerializedProperty entries = serialized.FindProperty("entries");
            entries.arraySize = 3;

            WriteNewsEntry(entries.GetArrayElementAtIndex(0), 1, "Merchant NPC", merchantAvatar,
                "Khach tu Chau Doc dang san khom dep.",
                "Ngay 1: thuong lai rat chuong khom va cam ngon, gia mo cua se cao vao buoi sang.");
            WriteNewsEntry(entries.GetArrayElementAtIndex(1), 2, "Villager NPC", villagerAvatar,
                "Dan lang can mua bi dao va dua hau de dua cho.",
                "Ngay 2: cac ghe nho tra gia chat hon, nhung rat de mua dua hau cuoi ngay.");
            WriteNewsEntry(entries.GetArrayElementAtIndex(2), 3, "Merchant NPC", merchantAvatar,
                "Tin don co con nuoc rong som.",
                "Ngay 3: nuoc len cao luc rang dong, ghe de luon rach hep; hang nang duoc gia hon neu giao som.");

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            return database;
        }

        private static AtmosphericProfileSO EnsureEnvironmentProfile()
        {
            AtmosphericProfileSO profile = AssetDatabase.LoadAssetAtPath<AtmosphericProfileSO>(EnvironmentProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<AtmosphericProfileSO>();
                AssetDatabase.CreateAsset(profile, EnvironmentProfilePath);
            }

            // Màu nắng (Sun): đêm xanh lạnh -> bình minh cam ấm -> trưa trắng ấm -> hoàng hôn cam rực.
            SetPrivate(profile, "lightColorOverDay", BuildColorGradient(
                new Color(0.30f, 0.38f, 0.55f), new Color(1.00f, 0.80f, 0.58f),
                new Color(1.00f, 0.96f, 0.88f), new Color(1.00f, 0.62f, 0.34f),
                new Color(0.30f, 0.38f, 0.55f)));
            SetPrivate(profile, "lightIntensityOverDay", AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1.1f));
            SetPrivate(profile, "sunPitchOverDay", new AnimationCurve(
                new Keyframe(0f, -20f),
                new Keyframe(0.25f, 25f),
                new Keyframe(0.5f, 70f),
                new Keyframe(0.75f, 15f),
                new Keyframe(1f, -30f)));
            // Sương mù: DÀY lúc bình minh (~5-6h, t≈0.21-0.25 = 0.05), tan dần về trưa, ám nhẹ chiều/tối.
            SetPrivate(profile, "fogDensityOverDay", new AnimationCurve(
                new Keyframe(0.125f, 0.045f),  // 03h
                new Keyframe(0.21f, 0.05f),    // 05h sương dày nhất
                new Keyframe(0.25f, 0.045f),   // 06h (lúc mở scene)
                new Keyframe(0.42f, 0.012f),   // 10h
                new Keyframe(0.5f, 0.004f),    // 12h trong
                new Keyframe(0.75f, 0.02f),    // 18h chiều
                new Keyframe(1f, 0.035f)));
            SetPrivate(profile, "maxWaterHeight", 4f);
            SetPrivate(profile, "minWaterHeight", 1.6f);
            SetPrivate(profile, "waterLevelOverDay", new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.25f, 0.85f),
                new Keyframe(0.5f, 0.45f),
                new Keyframe(0.75f, 0.15f),
                new Keyframe(1f, 1f)));

            // --- Khí quyển mở rộng cho AtmosphereSkyBridge (màu sắc miền Tây) ---
            // Cường độ nắng: đỉnh ~1.5 lúc trưa, ~0.2 lúc 18h, tối về 0.
            SetPrivate(profile, "sunIntensityCurve", new AnimationCurve(
                new Keyframe(0.125f, 0.05f),   // 03h
                new Keyframe(0.21f, 0.45f),    // 05h bình minh
                new Keyframe(0.5f, 1.5f),      // 12h trưa gắt
                new Keyframe(0.71f, 0.55f),    // 17h
                new Keyframe(0.79f, 0.12f),    // 19h
                new Keyframe(1f, 0.0f)));
            // Vòm trời: đêm xanh thẫm -> bình minh ĐÀO hồng-cam -> trưa xanh ấm -> hoàng hôn cam rực.
            SetPrivate(profile, "skyColorGradient", BuildColorGradient(
                new Color(0.12f, 0.16f, 0.28f), new Color(0.98f, 0.80f, 0.66f),
                new Color(0.55f, 0.70f, 0.88f), new Color(0.97f, 0.52f, 0.28f),
                new Color(0.12f, 0.16f, 0.28f)));
            // Chân trời/ambient equator: tông ấm phù sa.
            SetPrivate(profile, "equatorColorGradient", BuildColorGradient(
                new Color(0.20f, 0.22f, 0.28f), new Color(0.90f, 0.70f, 0.52f),
                new Color(0.74f, 0.72f, 0.64f), new Color(0.88f, 0.56f, 0.34f),
                new Color(0.20f, 0.22f, 0.28f)));
            // Sương mù: bình minh KEM ẤM (nắng xuyên sương), trưa nhạt, hoàng hôn ám cam.
            SetPrivate(profile, "fogColorGradient", BuildColorGradient(
                new Color(0.70f, 0.72f, 0.74f), new Color(0.95f, 0.88f, 0.80f),
                new Color(0.85f, 0.88f, 0.90f), new Color(0.93f, 0.70f, 0.50f),
                new Color(0.55f, 0.58f, 0.64f)));
            // Nước phù sa nâu đục (~#8B5A2B) đổi theo góc sáng: bình minh nâu ấm -> trưa nâu olive -> chiều nâu cam.
            SetPrivate(profile, "waterColorGradient", BuildColorGradient(
                new Color(0.34f, 0.28f, 0.18f), new Color(0.40f, 0.33f, 0.20f),
                new Color(0.47f, 0.39f, 0.22f), new Color(0.45f, 0.30f, 0.17f),
                new Color(0.30f, 0.25f, 0.16f)));

            EditorUtility.SetDirty(profile);
            return profile;
        }

        // Tạo Gradient 5 mốc đều (0/0.25/0.5/0.75/1) — alpha = 1.
        private static Gradient BuildColorGradient(Color night, Color dawn, Color noon, Color dusk, Color lateNight)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(night, 0f),
                    new GradientColorKey(dawn, 0.25f),
                    new GradientColorKey(noon, 0.5f),
                    new GradientColorKey(dusk, 0.75f),
                    new GradientColorKey(lateNight, 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }

        private static BoatStats EnsureBoatStats()
        {
            BoatStats stats = AssetDatabase.LoadAssetAtPath<BoatStats>(BoatStatsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<BoatStats>();
                AssetDatabase.CreateAsset(stats, BoatStatsPath);
            }

            SetPrivate(stats, "thrustForce", 16f);
            SetPrivate(stats, "waterDrag", 1.35f);
            SetPrivate(stats, "sidewaysDrag", 7f);
            SetPrivate(stats, "turnTorque", 4.9f);
            SetPrivate(stats, "lateralThrust", 2.6f);
            SetPrivate(stats, "activeDragFactor", 1f);
            SetPrivate(stats, "coastDragFactor", 0.42f);
            SetPrivate(stats, "riverCurrent", new Vector3(0.14f, 0f, 0.26f));
            SetPrivate(stats, "maxPenaltyFactor", 0.42f);
            SetPrivate(stats, "baseMaxSpeed", 11f);
            EditorUtility.SetDirty(stats);
            return stats;
        }

        private static void BuildEnvironmentAssets(Transform parent, Terrain terrain)
        {
            GameObject envGroup = new GameObject("EnvironmentAssets");
            envGroup.transform.SetParent(parent);

            // Các đường dẫn tới asset cây cối
            string[] treePaths = new string[]
            {
                "Assets/_Project/Art/model_mau/Cay/palm_trees.glb",
                "Assets/_Project/Art/model_mau/Cay/mango_tree.glb",
                "Assets/_Project/Art/model_mau/Cay/tree_elm.glb",
                "Assets/_Project/Art/model_mau/Cay/small_trees.glb"
            };

            // Các đường dẫn tới asset lục bình (water hyacinth)
            string[] hyacinthPaths = new string[]
            {
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhlon/lucbinhlon (1).glb",
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhlon/lucbinhlon (2).glb",
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhlon/lucbinhlon (3).glb",
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhlon/lucbinhlon (4).glb",
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhdonle/lucbinhdonle (1).glb",
                "Assets/_Project/Art/model_mau/lucbinh/lucbinhdonle/lucbinhdonle (2).glb"
            };

            Random.InitState(42); // Seed cố định để sinh ngẫu nhiên nhưng ổn định giữa các lần build

            // Tọa độ định nghĩa sông ngòi để tránh đặt cây đè lên sông
            Vector2 mainStart = new Vector2(120f, 0f);
            Vector2 junction = new Vector2(120f, 92f);
            Vector2 leftForkEnd = new Vector2(36f, TerrainWorldDepth);
            Vector2 rightForkEnd = new Vector2(204f, TerrainWorldDepth);
            float mainHalfWidth = MainRiverHalfWidth;
            float forkHalfWidth = ForkRiverHalfWidth;
            float treeBankClearance = BankBlendWidth * 0.65f;

            // Rải cây cối trên đất liền dọc theo 2 bên bờ sông
            for (float x = 12f; x <= 228f; x += 14f)
            {
                for (float z = 12f; z <= 208f; z += 14f)
                {
                    // Thêm độ lệch ngẫu nhiên nhẹ để phân bố trông tự nhiên
                    float posX = x + Random.Range(-4f, 4f);
                    float posZ = z + Random.Range(-4f, 4f);
                    Vector2 point = new Vector2(posX, posZ);

                    float dMain = DistanceToSegment(point, mainStart, junction);
                    float dLeft = DistanceToSegment(point, junction, leftForkEnd);
                    float dRight = DistanceToSegment(point, junction, rightForkEnd);

                    // Nếu vị trí là đất liền (nằm ngoài phạm vi sông + biên an toàn)
                    if (dMain > mainHalfWidth + treeBankClearance && dLeft > forkHalfWidth + treeBankClearance && dRight > forkHalfWidth + treeBankClearance)
                    {
                        // Giu khu spawn va hanh lang camera thong thoang, tranh cay moc sat mat nguoi choi.
                        if (posX > 48f && posX < 108f && posZ < 72f)
                            continue;

                        float posY = terrain.SampleHeight(new Vector3(posX, 0f, posZ));
                        
                        // Chọn loại cây. Ưu tiên cây dừa (palm_trees) ở gần mép bờ sông hơn
                        string path = treePaths[Random.Range(0, treePaths.Length)];
                        float minEdgeToWater = Mathf.Min(dMain - mainHalfWidth, dLeft - forkHalfWidth, dRight - forkHalfWidth);
                        if (minEdgeToWater < 18f && Random.value < 0.7f)
                        {
                            path = "Assets/_Project/Art/model_mau/Cay/palm_trees.glb";
                        }

                        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (prefab != null)
                        {
                            GameObject treeInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                            if (treeInstance != null)
                            {
                                treeInstance.name = Path.GetFileNameWithoutExtension(path) + "_" + posX + "_" + posZ;
                                treeInstance.transform.SetParent(envGroup.transform);
                                treeInstance.transform.position = new Vector3(posX, posY, posZ);
                                treeInstance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                                ApplyTreeOrientationFix(treeInstance, path);

                                // Điều chỉnh tỉ lệ tương thích cho từng loại cây
                                float scaleMultiplier = 1f;
                                if (path.Contains("palm_trees")) scaleMultiplier = Random.Range(0.62f, 0.86f);
                                else if (path.Contains("mango_tree")) scaleMultiplier = Random.Range(0.38f, 0.58f);
                                else if (path.Contains("tree_elm")) scaleMultiplier = Random.Range(0.42f, 0.62f);
                                else if (path.Contains("small_trees")) scaleMultiplier = Random.Range(0.55f, 0.82f);

                                treeInstance.transform.localScale = Vector3.one * scaleMultiplier;
                            }
                        }
                    }
                }
            }

            // Rải các đám bèo lục bình (water hyacinth) trôi nổi trên mặt sông
            // Mặt nước có Y cố định khoảng 4f. Ưu tiên rải lục bình tụ lại dọc theo ven bờ sông (vùng nước nông)
            for (int i = 0; i < 45; i++)
            {
                float posX = Random.Range(30f, 210f);
                float posZ = Random.Range(25f, 205f);
                Vector2 point = new Vector2(posX, posZ);

                float dMain = DistanceToSegment(point, mainStart, junction);
                float dLeft = DistanceToSegment(point, junction, leftForkEnd);
                float dRight = DistanceToSegment(point, junction, rightForkEnd);

                bool inRiver = dMain < mainHalfWidth || dLeft < forkHalfWidth || dRight < forkHalfWidth;
                bool nearBank = false;

                if (inRiver)
                {
                    if (dMain < mainHalfWidth && dMain > mainHalfWidth - 6f) nearBank = true;
                    else if (dLeft < forkHalfWidth && dLeft > forkHalfWidth - 4f) nearBank = true;
                    else if (dRight < forkHalfWidth && dRight > forkHalfWidth - 4f) nearBank = true;
                }

                if (nearBank)
                {
                    string path = hyacinthPaths[Random.Range(0, hyacinthPaths.Length)];
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        GameObject hyacinthInstance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                        if (hyacinthInstance != null)
                        {
                            hyacinthInstance.name = "WaterHyacinth_" + i;
                            hyacinthInstance.transform.SetParent(envGroup.transform);
                            
                            // Đặt cao độ nổi mấp mé trên mặt nước (Y = 3.9f)
                            hyacinthInstance.transform.position = new Vector3(posX, 3.9f, posZ);
                            hyacinthInstance.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                            float scale = Random.Range(1.2f, 1.8f);
                            if (path.Contains("lucbinhdonle")) scale *= 0.5f;
                            hyacinthInstance.transform.localScale = Vector3.one * scale;

                            // Gắn AmbientBob để lục bình bập bềnh nhấp nhô sống động
                            AmbientBob bob = hyacinthInstance.AddComponent<AmbientBob>();
                            SetPrivate(bob, "bobAxis", new Vector3(0f, 0.08f, 0f));
                            SetPrivate(bob, "swayAxis", new Vector3(4f, 0f, 4f));
                            SetPrivate(bob, "bobSpeed", Random.Range(0.6f, 1.2f));
                            SetPrivate(bob, "phaseOffset", Random.Range(0f, Mathf.PI * 2f));
                        }
                    }
                }
            }
        }

        private static GameObject BuildTerrainAndRiver(Transform parent)
        {
            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = 513,
                alphamapResolution = 512,
                size = new Vector3(TerrainWorldWidth, TerrainWorldHeight, TerrainWorldDepth)
            };

            float[,] heights = new float[terrainData.heightmapResolution, terrainData.heightmapResolution];
            int res = terrainData.heightmapResolution;
            float submergedMudflatHeight = (WaterSurfaceY - 0.35f) / terrainData.size.y;

            for (int row = 0; row < res; row++)
            {
                for (int col = 0; col < res; col++)
                {
                    Vector2 point = new Vector2((float)col / (res - 1) * terrainData.size.x, (float)row / (res - 1) * terrainData.size.z);
                    RiverDistance river = GetNearestRiverDistance(point);
                    float edgeDistance = river.Distance - river.HalfWidth;
                    float shoreNoise = Mathf.PerlinNoise(point.x * 0.025f, point.y * 0.025f) * 0.018f;
                    float height;

                    if (edgeDistance < 0f)
                    {
                        float channelT = Mathf.InverseLerp(0f, -river.HalfWidth, edgeDistance);
                        channelT = Mathf.SmoothStep(0f, 1f, channelT);
                        height = Mathf.Lerp(submergedMudflatHeight, RiverBedHeight, channelT);
                    }
                    else
                    {
                        float bankT = Mathf.SmoothStep(0f, BankBlendWidth, edgeDistance);
                        height = Mathf.Lerp(submergedMudflatHeight, TerrainBaseHeight + shoreNoise, bankT);
                    }

                    // Khu bờ spawn phẳng, rộng để người chơi bắt đầu đi bộ trước khi lên ghe.
                    float spawnMask = SmoothRectMask(point, new Vector2(0f, 0f), new Vector2(101f, 52f), 12f);
                    spawnMask *= Mathf.SmoothStep(3f, 15f, edgeDistance);
                    height = Mathf.Lerp(height, TerrainBaseHeight - 0.02f, spawnMask * 0.92f);

                    // Mo them hanh lang tam nhin o khu spawn de camera khong bi vach dat xam chan mat.
                    float corridorMask = SmoothRectMask(point, new Vector2(70f, 0f), new Vector2(132f, 78f), 14f);
                    corridorMask *= Mathf.SmoothStep(8f, 18f, edgeDistance);
                    height = Mathf.Lerp(height, Mathf.Min(height, TerrainBaseHeight - 0.035f), corridorMask);

                    heights[row, col] = height;
                }
            }

            SmoothRiverbankHeights(heights, terrainData.size, 2, 0.26f);
            terrainData.SetHeights(0, 0, heights);
            ConfigureTerrainLayers(terrainData, heights);
            GameObject terrainRoot = Terrain.CreateTerrainGameObject(terrainData);
            terrainRoot.name = "RiverTerrain";
            terrainRoot.transform.SetParent(parent);
            terrainRoot.layer = 1;
            return terrainRoot;
        }

        private static GameObject BuildWater(Transform parent)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "WaterSurface";
            water.transform.SetParent(parent);
            water.transform.position = new Vector3(120f, WaterSurfaceY, 110f);
            water.transform.localScale = new Vector3(24f, 1f, 22f);

            // MeshCollider from Primitive Plane is concave and doesn't support trigger, disable it
            MeshCollider meshCol = water.GetComponent<MeshCollider>();
            if (meshCol != null) meshCol.enabled = false;

            Renderer renderer = water.GetComponent<Renderer>();
            Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            // Nước phù sa nâu đục (~#8B5A2B), ít trong — đúng sông Mekong. AtmosphereSkyBridge sẽ
            // ghi đè _BaseColor theo giờ qua waterColorGradient (vẫn tông nâu).
            waterMat.color = new Color(0.42f, 0.34f, 0.18f, 0.92f);
            renderer.sharedMaterial = waterMat;

            BoxCollider box = water.GetComponent<BoxCollider>();
            if (box == null) box = water.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, -0.04f, 0f);
            box.size = new Vector3(10f, 0.1f, 10f);
            box.isTrigger = true; // Must be trigger so it doesn't block boat physics
            return water;
        }

        // Rải ghe trái cây chen chúc giữa sông (mỗi ghe có Cây Bẹo treo nông sản) — đúng ảnh chợ nổi.
        private static void BuildFloatingMarketCrowd(Transform parent)
        {
            GameObject crowdRoot = new GameObject("FloatingMarketCrowd");
            crowdRoot.transform.SetParent(parent);
            crowdRoot.transform.position = new Vector3(120f, 3.6f, 100f);

            FloatingMarketSpawner spawner = crowdRoot.AddComponent<FloatingMarketSpawner>();
            SetPrivate(spawner, "boatPrefabs", LoadModels(new[]
            {
                "Assets/_Project/Art/model_mau/taubanhang/hủ tiếu/hủ tiếu (1).glb",
                "Assets/_Project/Art/model_mau/taubanhang/ghe tạp hóa/ghe tạp hóa (1).glb",
                "Assets/_Project/Art/model_mau/tauchokhach/tauchokhach (1).glb",
            }));
            SetPrivate(spawner, "fruitPrefabs", LoadModels(new[]
            {
                "Assets/_Project/Art/model_mau/thunghang/khom+cam/khom+cam (1).glb",
                "Assets/_Project/Art/model_mau/thunghang/duahau+dudu/duahau+dudu (1).glb",
                "Assets/_Project/Art/model_mau/thunghang/xoai+dua/xoai+dua (1).glb",
            }));
            SetPrivate(spawner, "boatCount", 14);
            SetPrivate(spawner, "areaSize", new Vector3(28f, 0f, 55f));
            SetPrivate(spawner, "waterY", 3.6f);
            SetPrivate(spawner, "minSpacing", 4f);
            SetPrivate(spawner, "spawnOnStart", true);
        }

        // Nhà sàn gỗ low-poly dọc 2 bờ sông (sàn nâng trên cọc + mái dốc).
        private static void BuildStiltHouses(Transform parent, Terrain terrain)
        {
            GameObject root = new GameObject("StiltHouses");
            root.transform.SetParent(parent);

            Vector2[] spots =
            {
                new Vector2(72f, 96f),
                new Vector2(168f, 96f),
                new Vector2(58f, 150f),
                new Vector2(184f, 150f),
                new Vector2(120f, 202f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                float groundY = SampleTerrainHeight(terrain, spots[i].x, spots[i].y, 3f);
                BuildStiltHouse(root.transform, $"StiltHouse_{i}", new Vector3(spots[i].x, groundY, spots[i].y), (i * 47f) % 360f);
            }
        }

        private static void BuildStiltHouse(Transform parent, string name, Vector3 groundPos, float yaw)
        {
            GameObject house = new GameObject(name);
            house.transform.SetParent(parent);
            house.transform.position = groundPos;
            house.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 4 cọc gỗ nâng sàn nhà lên khỏi mặt nước.
            float floorY = 2.0f;
            float[] sx = { -1.4f, 1.4f, -1.4f, 1.4f };
            float[] sz = { -1.0f, -1.0f, 1.0f, 1.0f };
            for (int i = 0; i < 4; i++)
            {
                GameObject stilt = CreatePrimitiveChild(house.transform, $"Stilt_{i}", PrimitiveType.Cylinder,
                    new Vector3(sx[i], floorY * 0.5f, sz[i]), new Vector3(0.12f, floorY * 0.5f, 0.12f), new Color(0.34f, 0.24f, 0.14f));
                stilt.GetComponent<Collider>().enabled = false;
            }

            // Sàn + thân nhà gỗ.
            GameObject body = CreatePrimitiveChild(house.transform, "Body", PrimitiveType.Cube,
                new Vector3(0f, floorY + 0.9f, 0f), new Vector3(3.4f, 1.8f, 2.6f), new Color(0.55f, 0.40f, 0.24f));
            body.GetComponent<Collider>().enabled = false;

            // Mái dốc (Cube xoay nghiêng) tông ngói/đỏ nâu.
            GameObject roof = CreatePrimitiveChild(house.transform, "Roof", PrimitiveType.Cube,
                new Vector3(0f, floorY + 2.05f, 0f), new Vector3(3.9f, 0.18f, 3.1f), new Color(0.62f, 0.28f, 0.18f));
            roof.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
            roof.GetComponent<Collider>().enabled = false;
        }

        // Nạp danh sách model (.glb/.fbx) theo đường dẫn, bỏ qua cái thiếu.
        private static GameObject[] LoadModels(string[] paths)
        {
            var list = new List<GameObject>();
            foreach (string path in paths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null) list.Add(model);
            }
            return list.ToArray();
        }

        private static void BuildObstacles(Transform parent)
        {
            GameObject obstacles = new GameObject("RiverObstacles");
            obstacles.transform.SetParent(parent);

            CreateObstacle(obstacles.transform, "HyacinthPatch", PrimitiveType.Sphere, new Vector3(103f, 3.25f, 122f), new Vector3(3f, 0.35f, 2.5f), new Color(0.20f, 0.55f, 0.25f), true);
            CreateObstacle(obstacles.transform, "WoodPost", PrimitiveType.Cylinder, new Vector3(139f, 3.4f, 84f), new Vector3(0.4f, 2.4f, 0.4f), new Color(0.36f, 0.23f, 0.13f), false);
            CreateObstacle(obstacles.transform, "BrokenBoat", PrimitiveType.Cube, new Vector3(73f, 3.35f, 154f), new Vector3(4f, 0.8f, 1.6f), new Color(0.25f, 0.25f, 0.25f), false);
        }

        // Tạo các bãi bùn ngầm (mudflat) ở nhánh sông nông. Collider tắt sẵn; TideController
        // sẽ BẬT khi mực nước rút dưới ngưỡng -> ghe chạm đáy, mắc cạn (Test Case 02).
        // Đặt trên layer 1 (RiverBed) để khớp BoatController.riverbedLayer = 1 << 1.
        private static Collider[] BuildMudflats(Transform parent)
        {
            GameObject mudflatRoot = new GameObject("Mudflats");
            mudflatRoot.transform.SetParent(parent);

            // Vị trí (x, y, z) tại các nhánh sông nông + gần ngã ba. Y đặt quanh mức nước thấp.
            Vector3[] spots =
            {
                new Vector3(78f,  2.4f, 150f),   // nhánh trái nông
                new Vector3(162f, 2.4f, 150f),   // nhánh phải nông
                new Vector3(120f, 2.4f, 104f),   // gần ngã ba
            };

            List<Collider> colliders = new List<Collider>();
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject mud = new GameObject($"Mudflat_{i}");
                mud.transform.SetParent(mudflatRoot.transform);
                mud.transform.position = spots[i];
                mud.layer = 1; // RiverBed layer

                BoxCollider box = mud.AddComponent<BoxCollider>();
                box.size = new Vector3(12f, 2.4f, 12f);
                box.enabled = false; // mặc định tắt, TideController bật khi nước rút
                colliders.Add(box);
            }

            return colliders.ToArray();
        }

        private static void BuildNpcBoats(Transform parent)
        {
            GameObject npcRoot = new GameObject("NpcBoats");
            npcRoot.transform.SetParent(parent);

            BuildNpcBoat(npcRoot.transform, "MerchantLargeBoat", new Vector3(140f, 3.55f, 118f), new Vector3(3.6f, 0.9f, 8.4f), new Color(0.48f, 0.20f, 0.12f), true);
            BuildNpcBoat(npcRoot.transform, "FoodVendorSmallBoat", new Vector3(111f, 3.45f, 84f), new Vector3(2.0f, 0.6f, 4.2f), new Color(0.20f, 0.34f, 0.52f), false);
        }

        private static void BuildNpcBoat(Transform parent, string name, Vector3 position, Vector3 hullScale, Color hullColor, bool isLargeBoat)
        {
            GameObject boatRoot = new GameObject(name);
            boatRoot.transform.SetParent(parent);
            boatRoot.transform.position = position;
            boatRoot.transform.rotation = Quaternion.Euler(0f, isLargeBoat ? -20f : 35f, 0f);
            boatRoot.AddComponent<AmbientBob>();

            string modelPath = isLargeBoat
                ? "Assets/_Project/Art/model_mau/tauchokhach/tauchokhach (1).glb"
                : "Assets/_Project/Art/model_mau/taubanhang/hủ tiếu/hủ tiếu (1).glb";
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (modelPrefab != null)
                {
                    GameObject model = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
                    if (model != null)
                    {
                        model.name = isLargeBoat ? "LargeBoatModel" : "VendorBoatModel";
                        model.transform.SetParent(boatRoot.transform, false);
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.identity;
                        model.transform.localScale = Vector3.one * (isLargeBoat ? 1.6f : 1.25f);
                        ApplyBoatOrientationFix(model);
                        DisableColliders(model);
                    }
                }
            else
            {
                GameObject hull = CreatePrimitiveChild(boatRoot.transform, "Hull", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), hullScale, hullColor);
                hull.GetComponent<Collider>().enabled = false;
            }

            GameObject canopy = CreatePrimitiveChild(boatRoot.transform, "Canopy", PrimitiveType.Cube, new Vector3(0f, 1.0f, 0f), new Vector3(hullScale.x * 0.85f, 0.14f, hullScale.z * 0.5f), isLargeBoat ? new Color(0.82f, 0.72f, 0.32f) : new Color(0.85f, 0.42f, 0.22f));
            canopy.GetComponent<Collider>().enabled = false;
            GameObject mast = CreatePrimitiveChild(boatRoot.transform, "Pole", PrimitiveType.Cylinder, new Vector3(hullScale.x * 0.28f, 1.2f, 0.6f), new Vector3(0.08f, 1.15f, 0.08f), new Color(0.58f, 0.46f, 0.26f));
            mast.GetComponent<Collider>().enabled = false;

            if (isLargeBoat)
            {
                GameObject cargoA = CreatePrimitiveChild(boatRoot.transform, "CargoCrateA", PrimitiveType.Cube, new Vector3(-0.9f, 0.7f, -1.2f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.62f, 0.42f, 0.18f));
                GameObject cargoB = CreatePrimitiveChild(boatRoot.transform, "CargoCrateB", PrimitiveType.Cube, new Vector3(0.9f, 0.7f, -0.4f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.62f, 0.42f, 0.18f));
                cargoA.GetComponent<Collider>().enabled = false;
                cargoB.GetComponent<Collider>().enabled = false;
            }
            else
            {
                GameObject soupPot = CreatePrimitiveChild(boatRoot.transform, "SoupPot", PrimitiveType.Sphere, new Vector3(0f, 0.7f, -0.4f), new Vector3(0.8f, 0.45f, 0.8f), new Color(0.16f, 0.16f, 0.18f));
                soupPot.GetComponent<Collider>().enabled = false;
            }

            GameObject npc = CreateNpcAvatar(boatRoot.transform, isLargeBoat ? "MerchantNpcOnBoat" : "VendorNpcOnBoat", new Vector3(0f, 1.35f, 0.9f), new Color(0.88f, 0.68f, 0.48f));
            SimpleNpcWander wander = npc.AddComponent<SimpleNpcWander>();
            wander.Configure(new[] { new Vector3(-0.55f, 0f, -0.65f), new Vector3(0.55f, 0f, 0.65f) }, isLargeBoat ? 0.45f : 0.35f);
            AddTradeTarget(npc, isLargeBoat ? "Thuong Lai" : "Ghe Ban Hang", 3.1f);
        }

        private static void BuildRiverLife(Transform parent, Terrain terrain)
        {
            GameObject lifeRoot = new GameObject("RiverLife");
            lifeRoot.transform.SetParent(parent);

            CreateReedCluster(lifeRoot.transform, "LeftBankReeds", terrain, new Vector3(83f, 0f, 88f), 5);
            CreateReedCluster(lifeRoot.transform, "RightBankReeds", terrain, new Vector3(157f, 0f, 74f), 6);
            CreateMistBand(lifeRoot.transform, "MorningMist", new Vector3(121f, 4.6f, 128f), new Vector3(14f, 0.7f, 5f));
        }

        private static void BuildAmbientNpcCrowd(Transform parent, Terrain terrain)
        {
            GameObject npcRoot = new GameObject("AmbientNpcs");
            npcRoot.transform.SetParent(parent);

            CreateAmbientNpc(npcRoot.transform, terrain, "DockWorker_A", new Vector3(70f, 0f, 52f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(6f, 0f, 2f),
                new Vector3(3f, 0f, 7f),
                new Vector3(-2f, 0f, 4f)
            }, 1.15f);

            CreateAmbientNpc(npcRoot.transform, terrain, "DockWorker_B", new Vector3(88f, 0f, 60f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, -1f),
                new Vector3(7f, 0f, 5f),
                new Vector3(1f, 0f, 6f)
            }, 1.05f);

            CreateAmbientNpc(npcRoot.transform, terrain, "Trader_A", new Vector3(104f, 0f, 72f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 3f),
                new Vector3(-3f, 0f, 5f),
                new Vector3(-5f, 0f, -2f)
            }, 1.10f);

            CreateAmbientNpc(npcRoot.transform, terrain, "Trader_B", new Vector3(132f, 0f, 72f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 4f),
                new Vector3(-4f, 0f, 6f),
                new Vector3(-2f, 0f, -3f)
            }, 1.08f);

            CreateAmbientNpc(npcRoot.transform, terrain, "Villager_A", new Vector3(154f, 0f, 66f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(6f, 0f, 3f),
                new Vector3(2f, 0f, 8f),
                new Vector3(-4f, 0f, 5f)
            }, 0.96f);

            CreateAmbientNpc(npcRoot.transform, terrain, "Villager_B", new Vector3(170f, 0f, 96f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, 2f),
                new Vector3(2f, 0f, 7f),
                new Vector3(-3f, 0f, 4f)
            }, 0.92f);

            CreateAmbientNpc(npcRoot.transform, terrain, "Villager_C", new Vector3(58f, 0f, 110f), new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(7f, 0f, 2f),
                new Vector3(5f, 0f, 7f),
                new Vector3(-1f, 0f, 8f)
            }, 1.02f);
        }

        private static void CreateAmbientNpc(Transform parent, Terrain terrain, string name, Vector3 worldSeedPosition, Vector3[] localWaypoints, float speed)
        {
            float y = terrain != null ? terrain.SampleHeight(worldSeedPosition) : worldSeedPosition.y;
            GameObject npc = CreateNpcAvatar(parent, name, new Vector3(worldSeedPosition.x, y, worldSeedPosition.z), new Color(0.72f, 0.42f, 0.32f));
            SimpleNpcWander wander = npc.AddComponent<SimpleNpcWander>();
            wander.Configure(localWaypoints, speed);
            AddTradeTarget(npc, name.Replace("_", " "), 3f);
        }

        private static void CreateReedCluster(Transform parent, string name, Terrain terrain, Vector3 center, int count)
        {
            GameObject cluster = new GameObject(name);
            cluster.transform.SetParent(parent);
            float centerY = SampleTerrainHeight(terrain, center.x, center.z, center.y);
            cluster.transform.position = new Vector3(center.x, centerY, center.z);

            for (int index = 0; index < count; index++)
            {
                float offsetX = (index % 3 - 1) * 1.15f;
                float offsetZ = (index / 3) * 1.15f;
                float halfHeight = 1.2f + index * 0.08f;
                float groundY = SampleTerrainHeight(terrain, center.x + offsetX, center.z + offsetZ, centerY);
                Vector3 localPosition = new Vector3(offsetX, groundY - centerY + halfHeight, offsetZ);
                GameObject reed = CreatePrimitiveChild(cluster.transform, $"Reed_{index}", PrimitiveType.Cylinder, localPosition, new Vector3(0.05f, halfHeight, 0.05f), new Color(0.45f, 0.60f, 0.20f));
                reed.GetComponent<Collider>().enabled = false;
                AmbientBob ambient = reed.AddComponent<AmbientBob>();
                SetPrivate(ambient, "bobAxis", new Vector3(0f, 0.05f, 0f));
                SetPrivate(ambient, "swayAxis", new Vector3(6f, 0f, 6f));
                SetPrivate(ambient, "phaseOffset", index * 0.4f);
            }
        }

        private static void CreateMistBand(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            GameObject mist = CreatePrimitiveChild(parent, name, PrimitiveType.Sphere, position, scale, new Color(0.86f, 0.90f, 0.94f, 0.35f));
            Collider collider = mist.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            AmbientBob ambient = mist.AddComponent<AmbientBob>();
            SetPrivate(ambient, "bobAxis", new Vector3(0f, 0.18f, 0f));
            SetPrivate(ambient, "swayAxis", new Vector3(0f, 4f, 0f));
            SetPrivate(ambient, "bobSpeed", 0.35f);
        }

        private static GameObject BuildBoat(Transform parent, BoatStats boatStats, Transform waterSurface)
        {
            GameObject boat = new GameObject("PlayerBoat");
            boat.transform.SetParent(parent);
            boat.transform.position = new Vector3(118f, 3.75f, 34f);
            boat.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            boat.AddComponent<BoxCollider>().size = new Vector3(1.8f, 1.2f, 5.4f);

            Rigidbody rb = boat.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.mass = 1.2f;
            rb.linearDamping = 0.6f;
            rb.angularDamping = 0.85f;

            PCBoatInput input = boat.AddComponent<PCBoatInput>();
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            SetPrivate(input, "inputActions", inputActions);

            BoatController controller = boat.AddComponent<BoatController>();
            SetPrivate(controller, "boatStats", boatStats);
            SetPrivate(controller, "riverbedLayer", (LayerMask)(1 << 1));

            BoatBuoyancy buoyancy = boat.AddComponent<BoatBuoyancy>();
            SetPrivate(buoyancy, "waterSurface", waterSurface);

            GameObject visualRoot = new GameObject("BoatVisualRoot");
            visualRoot.transform.SetParent(boat.transform, false);
            GameObject boatModel = AssetDatabase.LoadAssetAtPath<GameObject>(BoatModelPath);
            if (boatModel != null)
            {
                GameObject modelInstance = PrefabUtility.InstantiatePrefab(boatModel) as GameObject;
                if (modelInstance != null)
                {
                    modelInstance.name = "BoatModel";
                    modelInstance.transform.SetParent(visualRoot.transform, false);
                    modelInstance.transform.localScale = new Vector3(458f, 458f, 458f);
                    modelInstance.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    ApplyBoatOrientationFix(modelInstance);
                }
            }
            else
            {
                CreatePrimitiveChild(visualRoot.transform, "BoatHull", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.8f, 5.4f), new Color(0.49f, 0.31f, 0.18f));
            }

            return boat;
        }

        private static GameObject BuildShorePlayer(Transform parent, GameObject boat)
        {
            GameObject player = new GameObject("PlayerOnFoot");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(92f, 4.25f, 25f);
            player.transform.rotation = Quaternion.Euler(0f, 80f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            ShorePlayerController shoreController = player.AddComponent<ShorePlayerController>();
            player.AddComponent<PlayerNpcTradeInteractor>();

            GameObject visualRoot = new GameObject("PlayerVisualRoot");
            visualRoot.transform.SetParent(player.transform, false);
            CreatePrimitiveChild(visualRoot.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.55f, 0.9f, 0.55f), new Color(0.24f, 0.36f, 0.50f));
            CreatePrimitiveChild(visualRoot.transform, "Hat", PrimitiveType.Cylinder, new Vector3(0f, 1.85f, 0f), new Vector3(0.42f, 0.08f, 0.42f), new Color(0.82f, 0.68f, 0.36f));

            GameObject shoreVillager = CreateNpcAvatar(parent, "ShoreVillagerNpc", new Vector3(78f, 4.25f, 38f), new Color(0.72f, 0.42f, 0.32f));
            shoreVillager.AddComponent<SimpleNpcWander>()
                .Configure(new[] { new Vector3(0f, 0f, 0f), new Vector3(8f, 0f, 3f), new Vector3(2f, 0f, 8f), new Vector3(-5f, 0f, 4f) }, 1.1f);
            AddTradeTarget(shoreVillager, "Dan Lang", 3.2f);

            Transform standPoint = CreateMarker(boat.transform, "PlayerStandPoint", new Vector3(0f, 1.35f, -0.4f), Quaternion.identity);
            Transform dismountPoint = CreateMarker(boat.transform, "DismountPoint", new Vector3(-24f, 0.5f, -6f), Quaternion.Euler(0f, 80f, 0f));

            BoatBoardingController boarding = player.AddComponent<BoatBoardingController>();
            boarding.Configure(
                shoreController,
                visualRoot.transform,
                boat.transform,
                standPoint,
                dismountPoint,
                boat.GetComponent<BoatController>(),
                boat.GetComponent<PCBoatInput>(),
                Camera.main != null ? Camera.main.GetComponent<BoatFollowCamera>() : null);

            return player;
        }

        private static void SetupBoardingFlow(GameObject player, GameObject boat, BoatFollowCamera followCamera)
        {
            if (player == null || boat == null)
                return;

            BoatBoardingController boarding = player.GetComponent<BoatBoardingController>();
            if (boarding == null)
                return;

            boarding.Configure(
                player.GetComponent<ShorePlayerController>(),
                player.transform.Find("PlayerVisualRoot"),
                boat.transform,
                boat.transform.Find("PlayerStandPoint"),
                boat.transform.Find("DismountPoint"),
                boat.GetComponent<BoatController>(),
                boat.GetComponent<PCBoatInput>(),
                followCamera);
        }

        private static void SetupBoatVisualModules(BoatCampManager boatCampManager, Transform boat)
        {
            Transform visualRoot = boat.Find("BoatVisualRoot");
            if (visualRoot == null) return;

            GameObject storage = CreatePrimitiveChild(visualRoot, "StorageModule", PrimitiveType.Cube, new Vector3(0f, 0.95f, -0.4f), new Vector3(1.4f, 0.5f, 1.4f), new Color(0.58f, 0.42f, 0.22f));
            GameObject roof = CreatePrimitiveChild(visualRoot, "RoofModule", PrimitiveType.Cube, new Vector3(0f, 1.8f, -0.5f), new Vector3(2.1f, 0.18f, 2.8f), new Color(0.26f, 0.36f, 0.22f));
            GameObject engine1 = CreatePrimitiveChild(visualRoot, "EngineTier1", PrimitiveType.Cylinder, new Vector3(0.6f, 0.5f, -2.5f), new Vector3(0.25f, 0.35f, 0.25f), new Color(0.32f, 0.32f, 0.35f));
            GameObject engine2 = CreatePrimitiveChild(visualRoot, "EngineTier2", PrimitiveType.Cylinder, new Vector3(-0.6f, 0.5f, -2.5f), new Vector3(0.30f, 0.45f, 0.30f), new Color(0.18f, 0.18f, 0.20f));
            GameObject bamboo1 = CreatePrimitiveChild(visualRoot, "BambooPoleTier1", PrimitiveType.Cylinder, new Vector3(1.0f, 1.8f, 0.4f), new Vector3(0.06f, 1.6f, 0.06f), new Color(0.63f, 0.58f, 0.30f));
            GameObject bamboo2 = CreatePrimitiveChild(visualRoot, "BambooPoleTier2", PrimitiveType.Cylinder, new Vector3(-1.0f, 1.8f, 0.4f), new Vector3(0.06f, 1.7f, 0.06f), new Color(0.63f, 0.58f, 0.30f));

            storage.SetActive(false);
            roof.SetActive(false);
            engine1.SetActive(false);
            engine2.SetActive(false);
            bamboo1.SetActive(false);
            bamboo2.SetActive(false);

            SetPrivate(boatCampManager, "storageModuleObject", storage);
            SetPrivate(boatCampManager, "roofObject", roof);
            SetPrivate(boatCampManager, "bambooPoleObject", bamboo1);
            SetPrivate(boatCampManager, "bambooPoleLevelObjects", new List<GameObject> { bamboo1, bamboo2 });
            SetPrivate(boatCampManager, "engineLevelObjects", new List<GameObject> { engine1, engine2 });
        }

        private static void ConfigureMainCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cam.transform.position = new Vector3(92f, 8.5f, 12f);
            cam.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.1f;
        }

        private static BoatFollowCamera AttachFollowCamera(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null || target == null) return null;
            BoatFollowCamera followCamera = cam.GetComponent<BoatFollowCamera>();
            if (followCamera == null)
            {
                followCamera = cam.gameObject.AddComponent<BoatFollowCamera>();
            }

            followCamera.Configure(target);
            return followCamera;
        }

        private static void ConfigureSaveLoad(
            SaveLoadManager saveLoadManager,
            PlayerStats playerStats,
            InventoryManager inventoryManager,
            BoatCampManager boatCampManager,
            BambooPoleManager bambooPoleManager,
            DurabilityManager durabilityManager,
            TimeManager timeManager,
            List<ItemData> marketItems)
        {
            if (saveLoadManager == null)
                return;

            SetPrivate(saveLoadManager, "playerStats", playerStats);
            SetPrivate(saveLoadManager, "inventoryManager", inventoryManager);
            SetPrivate(saveLoadManager, "boatCampManager", boatCampManager);
            SetPrivate(saveLoadManager, "bambooPoleManager", bambooPoleManager);
            SetPrivate(saveLoadManager, "durabilityManager", durabilityManager);
            SetPrivate(saveLoadManager, "timeManager", timeManager);
            SetPrivate(saveLoadManager, "masterItemDatabase", marketItems);
        }

        private static Gradient BuildGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.38f, 0.52f, 0.76f), 0f),
                    new GradientColorKey(new Color(1.0f, 0.68f, 0.35f), 0.25f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.82f), 0.5f),
                    new GradientColorKey(new Color(0.97f, 0.58f, 0.32f), 0.75f),
                    new GradientColorKey(new Color(0.22f, 0.28f, 0.44f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static void FillUpgradeArray(SerializedProperty property, (string displayName, int costMoney, float capacityBonus)[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("displayName").stringValue = values[i].displayName;
                item.FindPropertyRelative("costMoney").intValue = values[i].costMoney;
                item.FindPropertyRelative("capacityBonus").floatValue = values[i].capacityBonus;
            }
        }

        private static void FillEngineArray(SerializedProperty property, (string displayName, int costMoney, float thrustMultiplier)[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("displayName").stringValue = values[i].displayName;
                item.FindPropertyRelative("costMoney").intValue = values[i].costMoney;
                item.FindPropertyRelative("thrustMultiplier").floatValue = values[i].thrustMultiplier;
            }
        }

        private static void FillRoofArray(SerializedProperty property, (string displayName, int costMoney)[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("displayName").stringValue = values[i].displayName;
                item.FindPropertyRelative("costMoney").intValue = values[i].costMoney;
            }
        }

        private static void FillBambooArray(SerializedProperty property, (string displayName, int costMoney, float hagglingBonusRatio)[] values)
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty item = property.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("displayName").stringValue = values[i].displayName;
                item.FindPropertyRelative("costMoney").intValue = values[i].costMoney;
                item.FindPropertyRelative("hagglingBonusRatio").floatValue = values[i].hagglingBonusRatio;
            }
        }

        private static void WriteNewsEntry(SerializedProperty property, int day, string name, Sprite avatar, string headline, string rumor)
        {
            property.FindPropertyRelative("dayNumber").intValue = day;
            property.FindPropertyRelative("npcName").stringValue = name;
            property.FindPropertyRelative("npcAvatar").objectReferenceValue = avatar;
            property.FindPropertyRelative("headline").stringValue = headline;
            property.FindPropertyRelative("marketRumor").stringValue = rumor;
        }

        private static GameObject CreateObstacle(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, bool flatten)
        {
            GameObject obstacle = GameObject.CreatePrimitive(type);
            obstacle.name = name;
            obstacle.transform.SetParent(parent);
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            if (flatten) obstacle.transform.rotation = Quaternion.Euler(0f, 0f, 12f);
            ApplyColorMaterial(obstacle.GetComponent<Renderer>(), color);
            return obstacle;
        }

        private static GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            ApplyColorMaterial(child.GetComponent<Renderer>(), color);
            return child;
        }

        private static GameObject CreateNpcAvatar(Transform parent, string name, Vector3 localPosition, Color tunicColor)
        {
            GameObject npc = new GameObject(name);
            npc.transform.SetParent(parent, false);
            npc.transform.localPosition = localPosition;

            GameObject npcModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcModelPath);
            if (npcModelPrefab != null)
            {
                GameObject model = PrefabUtility.InstantiatePrefab(npcModelPrefab) as GameObject;
                if (model != null)
                {
                    model.name = "NpcModel";
                    model.transform.SetParent(npc.transform, false);
                    model.transform.localPosition = Vector3.zero;
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.transform.localScale = Vector3.one * 110f;
                    DisableColliders(model);
                    return npc;
                }
            }

            GameObject body = CreatePrimitiveChild(npc.transform, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.8f, 0f), new Vector3(0.42f, 0.75f, 0.42f), tunicColor);
            GameObject head = CreatePrimitiveChild(npc.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.55f, 0f), new Vector3(0.34f, 0.34f, 0.34f), new Color(0.88f, 0.68f, 0.48f));
            GameObject hat = CreatePrimitiveChild(npc.transform, "ConicalHat", PrimitiveType.Cylinder, new Vector3(0f, 1.9f, 0f), new Vector3(0.55f, 0.07f, 0.55f), new Color(0.84f, 0.72f, 0.44f));

            body.GetComponent<Collider>().enabled = false;
            head.GetComponent<Collider>().enabled = false;
            hat.GetComponent<Collider>().enabled = false;
            return npc;
        }

        private static Transform CreateMarker(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localRotation = localRotation;
            return marker.transform;
        }

        private static void DisableColliders(GameObject root)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
                collider.enabled = false;
        }

        private static void AddTradeTarget(GameObject npcRoot, string displayName, float radius)
        {
            if (npcRoot == null)
                return;

            NpcTradeTarget target = npcRoot.GetComponent<NpcTradeTarget>();
            if (target == null)
                target = npcRoot.AddComponent<NpcTradeTarget>();

            target.Configure(displayName, radius);
        }

        private static void ApplyTreeOrientationFix(GameObject treeInstance, string assetPath)
        {
            if (treeInstance == null)
                return;

            Quaternion correction = Quaternion.identity;
            if (assetPath.Contains("palm_trees") || assetPath.Contains("mango_tree") || assetPath.Contains("tree_elm") || assetPath.Contains("small_trees"))
                correction = Quaternion.Euler(-90f, 0f, 0f);

            treeInstance.transform.rotation = treeInstance.transform.rotation * correction;
        }

        private static void ApplyBoatOrientationFix(GameObject boatInstance)
        {
            if (boatInstance == null)
                return;
        }

        private static void ApplyColorMaterial(Renderer renderer, Color color)
        {
            if (renderer == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Diffuse");
            Material material = new Material(shader)
            {
                color = color
            };
            renderer.sharedMaterial = material;
        }

        private struct RiverDistance
        {
            public float Distance;
            public float HalfWidth;

            public RiverDistance(float distance, float halfWidth)
            {
                Distance = distance;
                HalfWidth = halfWidth;
            }

            public float EdgeDistance => Distance - HalfWidth;
        }

        private static RiverDistance GetNearestRiverDistance(Vector2 point)
        {
            Vector2 mainStart = new Vector2(120f, 0f);
            Vector2 junction = new Vector2(120f, 92f);
            Vector2 leftForkEnd = new Vector2(36f, TerrainWorldDepth);
            Vector2 rightForkEnd = new Vector2(204f, TerrainWorldDepth);

            RiverDistance nearest = new RiverDistance(DistanceToSegment(point, mainStart, junction), MainRiverHalfWidth);
            float nearestEdge = nearest.EdgeDistance;

            RiverDistance left = new RiverDistance(DistanceToSegment(point, junction, leftForkEnd), ForkRiverHalfWidth);
            if (left.EdgeDistance < nearestEdge)
            {
                nearest = left;
                nearestEdge = left.EdgeDistance;
            }

            RiverDistance right = new RiverDistance(DistanceToSegment(point, junction, rightForkEnd), ForkRiverHalfWidth);
            if (right.EdgeDistance < nearestEdge)
                nearest = right;

            return nearest;
        }

        private static float SmoothRectMask(Vector2 point, Vector2 min, Vector2 max, float feather)
        {
            float left = Mathf.SmoothStep(min.x - feather, min.x + feather, point.x);
            float right = 1f - Mathf.SmoothStep(max.x - feather, max.x + feather, point.x);
            float bottom = Mathf.SmoothStep(min.y - feather, min.y + feather, point.y);
            float top = 1f - Mathf.SmoothStep(max.y - feather, max.y + feather, point.y);
            return Mathf.Clamp01(left * right * bottom * top);
        }

        private static void SmoothRiverbankHeights(float[,] heights, Vector3 terrainSize, int iterations, float strength)
        {
            int rows = heights.GetLength(0);
            int cols = heights.GetLength(1);

            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float[,] source = (float[,])heights.Clone();
                for (int row = 1; row < rows - 1; row++)
                {
                    for (int col = 1; col < cols - 1; col++)
                    {
                        Vector2 point = new Vector2((float)col / (cols - 1) * terrainSize.x, (float)row / (rows - 1) * terrainSize.z);
                        float edgeDistance = GetNearestRiverDistance(point).EdgeDistance;
                        float bankMask = Mathf.SmoothStep(-10f, 6f, edgeDistance) *
                            (1f - Mathf.SmoothStep(BankBlendWidth - 8f, BankBlendWidth + 8f, edgeDistance));

                        if (bankMask <= 0.001f)
                            continue;

                        float average =
                            source[row - 1, col - 1] + source[row - 1, col] + source[row - 1, col + 1] +
                            source[row, col - 1] + source[row, col] + source[row, col + 1] +
                            source[row + 1, col - 1] + source[row + 1, col] + source[row + 1, col + 1];
                        average /= 9f;
                        heights[row, col] = Mathf.Lerp(source[row, col], average, strength * bankMask);
                    }
                }
            }
        }

        private static void ConfigureTerrainLayers(TerrainData terrainData, float[,] heights)
        {
            Texture2D grassTexture = EnsureTerrainTexture(
                GrassTexturePath,
                "StylizedGrass_Texture",
                new Color32(0x4C, 0xAF, 0x50, 0xFF),
                new Color32(0x2E, 0x7D, 0x32, 0xFF),
                new Color32(0x8B, 0xC3, 0x4A, 0xFF),
                17f);

            Texture2D mudTexture = EnsureTerrainTexture(
                MudTexturePath,
                "MekongMud_Texture",
                new Color32(0x5D, 0x40, 0x37, 0xFF),
                new Color32(0x3E, 0x27, 0x23, 0xFF),
                new Color32(0x8D, 0x6E, 0x63, 0xFF),
                51f);

            TerrainLayer grassLayer = EnsureTerrainLayer(GrassLayerPath, "StylizedGrass_TerrainLayer", grassTexture, new Vector2(16f, 16f), 0.18f);
            TerrainLayer mudLayer = EnsureTerrainLayer(MudLayerPath, "MekongMud_TerrainLayer", mudTexture, new Vector2(10f, 10f), 0.08f);
            terrainData.terrainLayers = new[] { grassLayer, mudLayer };
            terrainData.SetAlphamaps(0, 0, BuildTerrainAlphamaps(terrainData, heights));
        }

        private static Texture2D EnsureTerrainTexture(string path, string textureName, Color baseColor, Color darkColor, Color lightColor, float seed)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                texture = new Texture2D(64, 64, TextureFormat.RGBA32, true)
                {
                    name = textureName
                };
                AssetDatabase.CreateAsset(texture, path);
            }

            texture.name = textureName;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            int width = Mathf.Max(1, texture.width);
            int height = Mathf.Max(1, texture.height);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float broadNoise = Mathf.PerlinNoise((x + seed) * 0.14f, (y - seed) * 0.14f);
                    float speckleNoise = Mathf.PerlinNoise((x - seed) * 0.48f, (y + seed) * 0.48f);
                    Color color = Color.Lerp(darkColor, lightColor, Mathf.SmoothStep(0.12f, 0.92f, broadNoise));
                    color = Color.Lerp(baseColor, color, 0.55f);
                    if (speckleNoise > 0.68f)
                        color = Color.Lerp(color, lightColor, 0.18f);
                    else if (speckleNoise < 0.24f)
                        color = Color.Lerp(color, darkColor, 0.12f);

                    pixels[y * width + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static TerrainLayer EnsureTerrainLayer(string path, string layerName, Texture2D texture, Vector2 tileSize, float smoothness)
        {
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer
                {
                    name = layerName
                };
                AssetDatabase.CreateAsset(layer, path);
            }

            layer.name = layerName;
            layer.diffuseTexture = texture;
            layer.tileSize = tileSize;
            layer.tileOffset = Vector2.zero;
            layer.specular = Color.black;
            layer.metallic = 0f;
            layer.smoothness = smoothness;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static float[,,] BuildTerrainAlphamaps(TerrainData terrainData, float[,] heights)
        {
            int resolution = terrainData.alphamapResolution;
            float[,,] alphamaps = new float[resolution, resolution, 2];

            for (int row = 0; row < resolution; row++)
            {
                for (int col = 0; col < resolution; col++)
                {
                    Vector2 point = new Vector2((float)col / (resolution - 1) * terrainData.size.x, (float)row / (resolution - 1) * terrainData.size.z);
                    RiverDistance river = GetNearestRiverDistance(point);
                    float outsideEdge = Mathf.Max(0f, river.EdgeDistance);
                    float normalizedHeight = SampleNormalizedHeight(heights, point, terrainData.size);

                    float bankInfluence = 1f - Mathf.SmoothStep(BankBlendWidth * 0.78f, BankBlendWidth + 9f, outsideEdge);
                    float slopeInfluence = 1f - Mathf.SmoothStep(TerrainBaseHeight - 0.035f, TerrainBaseHeight - 0.008f, normalizedHeight);
                    float underwaterInfluence = 1f - Mathf.SmoothStep((WaterSurfaceY - 0.08f) / terrainData.size.y, (WaterSurfaceY + 0.18f) / terrainData.size.y, normalizedHeight);
                    float mudWeight = Mathf.Clamp01(Mathf.Max(bankInfluence * slopeInfluence, bankInfluence * underwaterInfluence));

                    float topLand = Mathf.SmoothStep(TerrainBaseHeight - 0.012f, TerrainBaseHeight + 0.006f, normalizedHeight);
                    mudWeight = Mathf.Lerp(mudWeight, 0f, topLand);
                    mudWeight = Mathf.Clamp01(mudWeight + (Mathf.PerlinNoise(point.x * 0.11f, point.y * 0.11f) - 0.5f) * 0.08f * bankInfluence);

                    alphamaps[row, col, 0] = 1f - mudWeight;
                    alphamaps[row, col, 1] = mudWeight;
                }
            }

            return alphamaps;
        }

        private static float SampleNormalizedHeight(float[,] heights, Vector2 point, Vector3 terrainSize)
        {
            int rows = heights.GetLength(0);
            int cols = heights.GetLength(1);
            float x = Mathf.Clamp01(point.x / terrainSize.x) * (cols - 1);
            float z = Mathf.Clamp01(point.y / terrainSize.z) * (rows - 1);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, cols - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(z), 0, rows - 1);
            int x1 = Mathf.Clamp(x0 + 1, 0, cols - 1);
            int z1 = Mathf.Clamp(z0 + 1, 0, rows - 1);
            float tx = x - x0;
            float tz = z - z0;

            float a = Mathf.Lerp(heights[z0, x0], heights[z0, x1], tx);
            float b = Mathf.Lerp(heights[z1, x0], heights[z1, x1], tx);
            return Mathf.Lerp(a, b, tz);
        }

        private static float SampleTerrainHeight(Terrain terrain, float x, float z, float fallbackY = 0f)
        {
            return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : fallbackY;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f);
            t = Mathf.Clamp01(t);
            return Vector2.Distance(point, a + ab * t);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
#endif
