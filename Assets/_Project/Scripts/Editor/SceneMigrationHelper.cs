#if UNITY_EDITOR
/**
 * SceneMigrationHelper: Công cụ Editor tự động hóa việc di trú hệ thống game lên Scene môi trường
 *                       URP chất lượng cao "Main_ChoNoi" (gốc từ gói TerrainDemoScene_URP).
 * [Chức năng]: 4 menu dưới "Tools/Chợ Nổi/Migration/" —
 *              (1) Di trú GameSystems + Main Camera từ Assets/_Recovery/0.unity vào Main_ChoNoi,
 *                  xóa camera template, set Tag "MainCamera", khử trùng lặp EventSystem.
 *              (2) Sửa EventSystem: thay StandaloneInputModule -> InputSystemUIInputModule
 *                  (khắc phục InvalidOperationException khi Active Input Handling = Both).
 *              (3) Bake Lighting cho scene đang mở (dùng khi scene tối đen).
 *              (4) Dọn dự án: chuyển TerrainDemoScene_URP -> Assets/ThirdParty/ (giữ GUID) và
 *                  xóa file Main_ChoNoi.unity trùng lặp ở gốc Assets.
 * [Dependencies]: UnityEditor, UnityEditor.SceneManagement, UnityEngine.SceneManagement,
 *                 UnityEngine.EventSystems, (tùy chọn) UnityEngine.InputSystem.UI.
 */

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace ChoNoiMienTay.Editor
{
    public static class SceneMigrationHelper
    {
        private const string MainChoNoiPath = "Assets/_Project/Scenes/Sandbox/Main_ChoNoi.unity";
        private const string RecoveryScenePath = "Assets/_Recovery/0.unity";
        private const string StrayScenePath = "Assets/Main_ChoNoi.unity";

        private const string TerrainSrc = "Assets/TerrainDemoScene_URP";
        private const string ThirdPartyRoot = "Assets/ThirdParty";
        private const string TerrainDst = "Assets/ThirdParty/TerrainDemoScene_URP";

        // Các root object cần di trú từ scene _Recovery. Mở rộng nếu muốn kéo cả thế giới gameplay
        // (vd: "RiverMarketWorld", "FloatingMarketCrowd", "PlayerBoat", "PlayerOnFoot").
        private static readonly string[] MigrateRootNames = { "GameSystems", "Main Camera" };

        // Các root chứa camera bay/demo của template cần tắt để tránh xung đột render.
        private static readonly string[] DemoCameraRootNames = { "VirtualCameras", "HDRP Compositor" };

        private const string CameraTag = "MainCamera";

        // ────────────────────────────────────────────────────────────────────────────
        // 1. Di trú GameSystems + Main Camera vào Main_ChoNoi
        // ────────────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Chợ Nổi/Migration/1. Migrate GameSystems + Camera into Main_ChoNoi")]
        public static void MigrateIntoMainChoNoi()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SceneMigration] Hãy thoát Play Mode trước khi di trú scene.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainChoNoiPath) == null)
            {
                Debug.LogError($"[SceneMigration] Không tìm thấy scene đích: {MainChoNoiPath}");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RecoveryScenePath) == null)
            {
                Debug.LogError($"[SceneMigration] Không tìm thấy scene nguồn: {RecoveryScenePath}");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("[SceneMigration] Đã hủy: scene hiện tại chưa được lưu.");
                return;
            }

            Scene mainScene = EditorSceneManager.OpenScene(MainChoNoiPath, OpenSceneMode.Single);

            // (B2) Xóa Main Camera template + tắt các camera bay/demo trong Main_ChoNoi.
            GameObject templateCam = FindRootByName(mainScene, "Main Camera");
            if (templateCam != null)
            {
                Undo.DestroyObjectImmediate(templateCam);
                Debug.Log("[SceneMigration] Đã xóa Main Camera template của Main_ChoNoi.");
            }
            foreach (string demoRootName in DemoCameraRootNames)
            {
                GameObject demoRoot = FindRootByName(mainScene, demoRootName);
                if (demoRoot == null) continue;
                foreach (Camera cam in demoRoot.GetComponentsInChildren<Camera>(true))
                {
                    Undo.RecordObject(cam, "Disable demo camera");
                    cam.enabled = false;
                }
            }

            // (B3) Mở scene nguồn dạng additive.
            Scene recoveryScene = EditorSceneManager.OpenScene(RecoveryScenePath, OpenSceneMode.Additive);

            // (B4-B5) Tìm & di trú các root theo tên.
            int movedCount = 0;
            foreach (string name in MigrateRootNames)
            {
                GameObject root = FindRootByName(recoveryScene, name);
                if (root == null)
                {
                    Debug.LogError($"[SceneMigration] Không thấy root '{name}' trong {RecoveryScenePath} — bỏ qua.");
                    continue;
                }
                SceneManager.MoveGameObjectToScene(root, mainScene);
                movedCount++;
                Debug.Log($"[SceneMigration] Đã di trú root '{name}' -> Main_ChoNoi.");
            }

            // (B6) Set Tag MainCamera + đảm bảo chỉ 1 camera bật.
            EnsureSingleMainCamera(mainScene);

            // (B7) Khử trùng lặp EventSystem.
            DeduplicateEventSystem(mainScene);

            // (B2 menu) Sửa input module cho EventSystem còn lại.
            FixEventSystemInScene(mainScene, verbose: false);

            // (B8) Đóng scene nguồn KHÔNG lưu, rồi lưu scene đích.
            EditorSceneManager.CloseScene(recoveryScene, removeScene: true);
            EditorSceneManager.MarkSceneDirty(mainScene);
            EditorSceneManager.SaveScene(mainScene);

            int camCount = CountEnabledCameras(mainScene);
            Debug.Log($"[SceneMigration] HOÀN TẤT. Root đã di trú: {movedCount}/{MigrateRootNames.Length} | " +
                      $"Camera đang bật: {camCount} | Đã lưu {MainChoNoiPath}.");
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 2. Sửa EventSystem -> InputSystemUIInputModule
        // ────────────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Chợ Nổi/Migration/2. Fix EventSystem (Input System UI Module)")]
        public static void FixEventSystemMenu()
        {
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                Debug.LogError("[SceneMigration] Không có scene hợp lệ đang mở.");
                return;
            }
            FixEventSystemInScene(active, verbose: true);
            EditorSceneManager.MarkSceneDirty(active);
        }

        private static void FixEventSystemInScene(Scene scene, bool verbose)
        {
            EventSystem es = FindComponentInScene<EventSystem>(scene);
            if (es == null)
            {
                if (verbose) Debug.LogError("[SceneMigration] Không tìm thấy EventSystem trong scene.");
                return;
            }

#if ENABLE_INPUT_SYSTEM
            // Gỡ module cũ (đọc UnityEngine.Input -> gây InvalidOperationException khi dùng Input System).
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                Undo.DestroyObjectImmediate(legacy);
                Debug.Log("[SceneMigration] Đã gỡ StandaloneInputModule.");
            }

            if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                Undo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(es.gameObject);
                Debug.Log("[SceneMigration] Đã thêm InputSystemUIInputModule cho EventSystem.");
            }
            else if (verbose)
            {
                Debug.Log("[SceneMigration] EventSystem đã dùng InputSystemUIInputModule — không cần đổi.");
            }
#else
            Debug.LogWarning("[SceneMigration] ENABLE_INPUT_SYSTEM chưa bật. Bỏ qua nâng cấp input module. " +
                             "Hãy đặt Active Input Handling = Both/Input System Package rồi chạy lại.");
#endif
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 3. Bake Lighting
        // ────────────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Chợ Nổi/Migration/3. Bake Lighting (Active Scene)")]
        public static void BakeLighting()
        {
            if (Lightmapping.isRunning)
            {
                Debug.LogWarning("[SceneMigration] Lightmapping đang chạy.");
                return;
            }
            Debug.Log("[SceneMigration] Bắt đầu Bake Lighting (async)...");
            Lightmapping.BakeAsync();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // 4. Dọn dự án: chuyển TerrainDemoScene_URP -> ThirdParty + xóa scene trùng lặp
        // ────────────────────────────────────────────────────────────────────────────
        [MenuItem("Tools/Chợ Nổi/Migration/4. Organize: move TerrainDemoScene_URP to ThirdParty")]
        public static void OrganizeProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[SceneMigration] Hãy thoát Play Mode trước khi dọn thư mục.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ThirdPartyRoot))
            {
                AssetDatabase.CreateFolder("Assets", "ThirdParty");
                Debug.Log($"[SceneMigration] Đã tạo {ThirdPartyRoot}.");
            }

            if (AssetDatabase.IsValidFolder(TerrainSrc))
            {
                string err = AssetDatabase.MoveAsset(TerrainSrc, TerrainDst);
                if (string.IsNullOrEmpty(err))
                    Debug.Log($"[SceneMigration] Đã chuyển {TerrainSrc} -> {TerrainDst} (GUID giữ nguyên).");
                else
                    Debug.LogError($"[SceneMigration] Lỗi chuyển thư mục: {err}");
            }
            else
            {
                Debug.LogWarning($"[SceneMigration] Không thấy {TerrainSrc} (có thể đã chuyển).");
            }

            // Xóa file Main_ChoNoi.unity trùng lặp ở gốc Assets (bản chuẩn nằm trong Sandbox).
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(StrayScenePath) != null)
            {
                if (AssetDatabase.DeleteAsset(StrayScenePath))
                    Debug.Log($"[SceneMigration] Đã xóa scene trùng lặp: {StrayScenePath}.");
                else
                    Debug.LogError($"[SceneMigration] Không xóa được {StrayScenePath}.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ────────────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────────────
        private static GameObject FindRootByName(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
        {
            var list = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                list.AddRange(root.GetComponentsInChildren<T>(true));
            return list;
        }

        private static void EnsureSingleMainCamera(Scene scene)
        {
            GameObject mainCamGo = FindRootByName(scene, "Main Camera");
            Camera keep = mainCamGo != null ? mainCamGo.GetComponent<Camera>() : null;

            if (keep != null)
            {
                Undo.RecordObject(mainCamGo, "Set MainCamera tag");
                mainCamGo.tag = CameraTag;
                Undo.RecordObject(keep, "Enable main camera");
                keep.enabled = true;
            }
            else
            {
                Debug.LogWarning("[SceneMigration] Không thấy root 'Main Camera' sau di trú để set Tag.");
            }

            // Tắt mọi camera khác để tránh nhiều camera cùng render.
            foreach (Camera cam in FindComponentsInScene<Camera>(scene))
            {
                if (cam == keep) continue;
                Undo.RecordObject(cam, "Disable extra camera");
                cam.enabled = false;
            }
        }

        private static void DeduplicateEventSystem(Scene scene)
        {
            List<EventSystem> systems = FindComponentsInScene<EventSystem>(scene);
            if (systems.Count <= 1) return;

            // Giữ cái đầu tiên, xóa phần còn lại.
            for (int i = 1; i < systems.Count; i++)
            {
                Debug.Log($"[SceneMigration] Xóa EventSystem trùng lặp: {systems[i].name}.");
                Undo.DestroyObjectImmediate(systems[i].gameObject);
            }
        }

        private static int CountEnabledCameras(Scene scene)
        {
            int n = 0;
            foreach (Camera cam in FindComponentsInScene<Camera>(scene))
                if (cam.enabled) n++;
            return n;
        }
    }
}
#endif
