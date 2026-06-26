#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ChoNoiMienTay.Editor
{
    public static class TestLoadSprite
    {
        [MenuItem("ChoNoi/Test Load SVG Sprite")]
        public static void RunTest()
        {
            string panelPath = "Assets/Skyden_Games/Free_Casual_GUI/Resource/Free_Casual_GUI/Pannel/panel_lobby.svg.svg";
            string buttonPath = "Assets/Skyden_Games/Free_Casual_GUI/Resource/Free_Casual_GUI/Buttons/button_soft_orange.svg";
            
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(panelPath);
            Sprite buttonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(buttonPath);
            
            Debug.Log($"[TestLoadSprite] Panel Sprite: {(panelSprite != null ? panelSprite.name : "NULL")}");
            Debug.Log($"[TestLoadSprite] Button Sprite: {(buttonSprite != null ? buttonSprite.name : "NULL")}");
            
            // Check sub-assets
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(panelPath);
            Debug.Log($"[TestLoadSprite] All assets loaded at panel path count: {allAssets.Length}");
            foreach (var asset in allAssets)
            {
                Debug.Log($"  - Asset name: {asset.name}, type: {asset.GetType()}");
            }
        }
    }
}
#endif
