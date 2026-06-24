using UnityEngine;

namespace ChoNoiMienTay.Presentation
{
    public class PlayerStats : MonoBehaviour
    {
        [Header("Economy")]
        [SerializeField] private int currentMoney = 100000; // Tiền vốn ban đầu

        [Header("Stamina")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] private float currentStamina = 100f;

        [Header("Modifiers")]
        [Tooltip("Hệ số giảm tốn thể lực khi Nói ngọt (càng cao càng ít tốn)")]
        public float sweetTalkCostReduction = 0f;
        
        [Tooltip("Hệ số tăng giá bán tối đa khi thiện cảm đầy (0.5 = tăng 50% giá)")]
        public float maxBonusPriceRatio = 0.5f;

        public int CurrentMoney => currentMoney;
        public float CurrentStamina => currentStamina;
        public float MaxStamina => maxStamina;

        public void UpgradeMaxStamina(float amount)
        {
            maxStamina += amount;
            currentStamina += amount; // Hồi một lượng bằng lượng mới được cộng
            Debug.Log($"[PlayerStats] Đã nâng cấp Thể lực tối đa lên {maxStamina}");
        }

        public void UpgradeHagglingBonus(float extraRatio)
        {
            maxBonusPriceRatio += extraRatio;
            Debug.Log($"[PlayerStats] Đã nâng cấp kỹ năng Trả giá. Max Bonus Ratio mới: {maxBonusPriceRatio * 100}%");
        }

        public bool ConsumeStamina(float amount)
        {
            if (currentStamina >= amount)
            {
                currentStamina -= amount;
                Debug.Log($"[PlayerStats] Đã tiêu hao {amount} Thể lực. Còn lại: {currentStamina}/{maxStamina}");
                return true;
            }
            Debug.LogWarning($"[PlayerStats] KHÔNG ĐỦ THỂ LỰC! Cần {amount}, nhưng chỉ còn {currentStamina}");
            return false;
        }

        public void RestoreStamina(float amount)
        {
            currentStamina = Mathf.Clamp(currentStamina + amount, 0, maxStamina);
            Debug.Log($"[PlayerStats] Đã hồi phục {amount} Thể lực. Hiện tại: {currentStamina}/{maxStamina}");
        }

        public void AddMoney(int amount)
        {
            if (amount > 0)
            {
                currentMoney += amount;
                Debug.Log($"[PlayerStats] +{amount:N0} VNĐ. Số dư: {currentMoney:N0} VNĐ");
            }
        }

        public bool DeductMoney(int amount)
        {
            if (amount > 0 && currentMoney >= amount)
            {
                currentMoney -= amount;
                Debug.Log($"[PlayerStats] -{amount:N0} VNĐ. Số dư: {currentMoney:N0} VNĐ");
                return true;
            }
            Debug.LogWarning($"[PlayerStats] KHÔNG ĐỦ TIỀN! Cần {amount:N0} VNĐ, số dư chỉ còn {currentMoney:N0} VNĐ");
            return false;
        }
        public void LoadStats(int money, float stamina, float maxStam, float bonusRatio)
        {
            currentMoney = money;
            currentStamina = stamina;
            maxStamina = maxStam;
            maxBonusPriceRatio = bonusRatio;
        }
    }
}
