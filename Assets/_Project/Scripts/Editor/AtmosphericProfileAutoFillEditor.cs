#if UNITY_EDITOR
/**
 * AtmosphericProfileAutoFillEditor: CustomEditor cho AtmosphericProfileSO.
 * [Chức năng]: Thêm nút "Auto-Fill HEX từ Design Doc" vào Inspector. Khi nhấn,
 *              tự động gán 6 keyframe màu theo bảng thiết kế (cho_noi_can_tho_env_design.md)
 *              vào các Gradient field: skyColorGradient, equatorColorGradient, fogColorGradient,
 *              waterColorGradient, lightColorOverDay. Hỗ trợ Undo/Redo đầy đủ.
 * [Dependencies]: AtmosphericProfileSO (Infrastructure).
 */

using UnityEditor;
using UnityEngine;
using ChoNoi.Infrastructure;

namespace ChoNoi.Editor
{
    [CustomEditor(typeof(AtmosphericProfileSO))]
    public class AtmosphericProfileAutoFillEditor : UnityEditor.Editor
    {
        // 6 mốc thời gian chuẩn hoá (= giờ / 24) từ design doc.
        private static readonly float[] TimePoints =
        {
             3f / 24f,   // 03:00 — Bình Minh Tối
             5.5f/ 24f,  // 05:30 — Hừng Đông Rực Rỡ
             8f / 24f,   // 08:00 — Nắng Sáng Chợ Nổi
            13f / 24f,   // 13:00 — Đứng Bóng & Giông Nhiệt Đới
            16.5f/ 24f,  // 16:30 — Hoàng Hôn Thượng Nguồn
            18.5f/ 24f,  // 18:30 — Chạng Vạng Lên Đèn
        };

        // HexTable[mốc, cột]. Cột: 0=Sky  1=Equator/Horizon  2=Fog  3=Water  4=Light(DirectionalLight)
        private static readonly string[,] HexTable =
        {
            //   Sky        Equator     Fog        Water      Light
            { "#050B14", "#101B2B", "#0B121F", "#0A101A", "#1F2D42" }, // 03:00
            { "#2B4C7E", "#F26419", "#E76F51", "#8B6246", "#F4A261" }, // 05:30
            { "#4EA8DE", "#ADE8F4", "#CAF0F8", "#9C6644", "#FFF3D1" }, // 08:00
            { "#5C677D", "#ABC4FF", "#95A5A6", "#5C4033", "#E2E2E2" }, // 13:00
            { "#3D348B", "#F7B267", "#E27396", "#7A431D", "#F35B04" }, // 16:30
            { "#0B132B", "#1C2541", "#0B132B", "#050A14", "#000000" }, // 18:30
        };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Công cụ Design Doc", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tự động gán 6 keyframe màu HEX từ tài liệu thiết kế (cho_noi_can_tho_env_design.md) " +
                "vào các Gradient: Sky, Equator, Fog, Water, Light.\n" +
                "Thao tác này có thể Undo (Ctrl+Z).",
                MessageType.Info);

            if (GUILayout.Button("Auto-Fill HEX từ Design Doc", GUILayout.Height(32)))
            {
                ApplyDesignDocColors((AtmosphericProfileSO)target);
            }
        }

        private void ApplyDesignDocColors(AtmosphericProfileSO profile)
        {
            Undo.RecordObject(profile, "Auto-Fill Atmospheric Profile HEX");

            SerializedObject so = new SerializedObject(profile);

            FillGradient(so, "skyColorGradient",     columnIndex: 0);
            FillGradient(so, "equatorColorGradient", columnIndex: 1);
            FillGradient(so, "fogColorGradient",     columnIndex: 2);
            FillGradient(so, "waterColorGradient",   columnIndex: 3);
            FillGradient(so, "lightColorOverDay",    columnIndex: 4);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);
            Debug.Log("[AtmosphericProfileAutoFillEditor] Đã gán 6 mốc màu HEX từ Design Doc vào AtmosphericProfileSO.");
        }

        private static void FillGradient(SerializedObject so, string propertyName, int columnIndex)
        {
            SerializedProperty gradProp = so.FindProperty(propertyName);
            if (gradProp == null)
            {
                Debug.LogWarning($"[AutoFill] Không tìm thấy SerializedProperty '{propertyName}'. Kiểm tra tên field trong AtmosphericProfileSO.");
                return;
            }

            GradientColorKey[] colorKeys = new GradientColorKey[TimePoints.Length];
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[TimePoints.Length];

            for (int i = 0; i < TimePoints.Length; i++)
            {
                if (!ColorUtility.TryParseHtmlString(HexTable[i, columnIndex], out Color c))
                {
                    Debug.LogWarning($"[AutoFill] Không parse được HEX '{HexTable[i, columnIndex]}' tại mốc {i}.");
                    c = Color.white;
                }

                colorKeys[i] = new GradientColorKey(c, TimePoints[i]);
                alphaKeys[i] = new GradientAlphaKey(1f, TimePoints[i]);
            }

            Gradient gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);

            // gradientValue: strongly-typed SerializedProperty accessor cho Gradient field.
            gradProp.gradientValue = gradient;
        }
    }
}
#endif
