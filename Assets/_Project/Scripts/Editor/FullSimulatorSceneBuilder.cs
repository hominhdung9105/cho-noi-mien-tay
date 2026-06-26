#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChoNoi.Application;
using ChoNoi.Presentation;
using ChoNoiMienTay.Presentation;
using ChoNoiMienTay.UI;
using ChoNoiMienTay.Systems;
using ChoNoiMienTay.Data;
using ChoNoi.UI;
using ChoNoi.Presentation.NPC;

namespace ChoNoiMienTay.Editor
{
    public class FullSimulatorSceneBuilder
    {
        [MenuItem("ChoNoi/Scenes/Build Full UI Simulator Scene")]
        public static void BuildFullScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before rebuilding the scene.");
                return;
            }

            // 1. Build the base River Market Scene
            RiverMarketSceneBuilder.BuildScene();

            // 2. Open the scene
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Core/RiverMarketScene.unity");

            // 3. Find GameSystems
            GameObject systemsRoot = GameObject.Find("GameSystems");
            if (systemsRoot == null)
            {
                Debug.LogError("GameSystems not found in the scene.");
                return;
            }

            // 4. Attach BargainingPrototypeUI & BargainingSystem for Haggling
            BargainingSystem bargainingSystem = systemsRoot.AddComponent<BargainingSystem>();
            BargainingPrototypeUI prototypeUI = systemsRoot.AddComponent<BargainingPrototypeUI>();
            BargainingEconomyConfig config = AssetDatabase.LoadAssetAtPath<BargainingEconomyConfig>("Assets/_Project/ScriptableObjects/Bargaining/BargainingEconomyConfig.asset");
            
            PlayerStats playerStats = systemsRoot.GetComponent<PlayerStats>();
            InventoryManager inventoryManager = systemsRoot.GetComponent<InventoryManager>();
            EconomyManager economyManager = systemsRoot.GetComponent<EconomyManager>();
            
            if (config == null)
            {
                Debug.LogWarning("BargainingEconomyConfig not found, bargaining system may not work fully.");
            }
            
            bargainingSystem.Configure(config, playerStats, inventoryManager, economyManager);
            prototypeUI.Configure(bargainingSystem, playerStats, inventoryManager);

            // 5. Attach FullSimulatorUI for Tutorial, Settings, Dialogue, Marketing
            FullSimulatorUI fullUI = systemsRoot.GetComponent<FullSimulatorUI>();
            if (fullUI == null)
            {
                fullUI = systemsRoot.AddComponent<FullSimulatorUI>();
            }
            fullUI.inventoryManager = inventoryManager;
            fullUI.riverMarketHUD = systemsRoot.GetComponent<RiverMarketHUD>();

            // Load rasterized PNG sprites from Art/UI (supporting native 9-slicing)
            fullUI.panelBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/panel_lobby.png");
            fullUI.buttonSpriteNormal = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_soft_orange.png");
            fullUI.buttonSpriteHover = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_plain_orangeyellow.png");
            fullUI.buttonSpritePressed = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/UI/button_soft_blue.png");

            // Load and assign to RiverMarketHUD as well
            RiverMarketHUD hud = systemsRoot.GetComponent<RiverMarketHUD>();
            if (hud != null)
            {
                hud.panelBgSprite = fullUI.panelBgSprite;
                hud.buttonSpriteNormal = fullUI.buttonSpriteNormal;
                hud.buttonSpriteHover = fullUI.buttonSpriteHover;
                hud.buttonSpritePressed = fullUI.buttonSpritePressed;
            }

            // Load and assign to BargainingPrototypeUI as well
            if (prototypeUI != null)
            {
                prototypeUI.panelBgSprite = fullUI.panelBgSprite;
                prototypeUI.buttonSpriteNormal = fullUI.buttonSpriteNormal;
                prototypeUI.buttonSpriteHover = fullUI.buttonSpriteHover;
                prototypeUI.buttonSpritePressed = fullUI.buttonSpritePressed;
            }

            GameObject boat = GameObject.Find("PlayerBoat");
            if (boat != null)
            {
                fullUI.bambooPoleManager = boat.GetComponent<BambooPoleManager>();
            }

            // 6. Setup Contextual Interactions
            ConfigureTradeTarget("MerchantLargeBoat", InteractionTargetType.Bargain);
            ConfigureTradeTarget("FoodVendorSmallBoat", InteractionTargetType.Trade);
            ConfigureTradeTarget("ShoreVillagerNpc", InteractionTargetType.Trade);
            ConfigureTradeTarget("Villager_A", InteractionTargetType.Trade);
            ConfigureTradeTarget("Villager_B", InteractionTargetType.Trade);
            ConfigureTradeTarget("Villager_C", InteractionTargetType.Trade);
            ConfigureTradeTarget("WoodPost", InteractionTargetType.Upgrade);

            // Set dirty on modified components to ensure they are serialized and saved
            EditorUtility.SetDirty(systemsRoot);
            EditorUtility.SetDirty(fullUI);
            if (prototypeUI != null) EditorUtility.SetDirty(prototypeUI);
            if (bargainingSystem != null) EditorUtility.SetDirty(bargainingSystem);
            if (hud != null) EditorUtility.SetDirty(hud);

            // Save the scene
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Full UI Simulator Scene successfully built at Assets/_Project/Scenes/Core/RiverMarketScene.unity!");
        }

        private static void ConfigureTradeTarget(string gameObjectName, InteractionTargetType type)
        {
            GameObject obj = GameObject.Find(gameObjectName);
            if (obj != null)
            {
                NpcTradeTarget target = obj.GetComponentInChildren<NpcTradeTarget>();
                if (target == null)
                {
                    target = obj.AddComponent<NpcTradeTarget>();
                    target.Configure(gameObjectName, 5f);
                }
                
                // Use SerializedObject to set private serialized field
                SerializedObject so = new SerializedObject(target);
                so.FindProperty("targetType").enumValueIndex = (int)type;
                so.ApplyModifiedProperties();
            }
        }
    }
}
#endif
