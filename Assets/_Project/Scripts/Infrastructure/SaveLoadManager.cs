using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ChoNoiMienTay.Presentation;
using ChoNoiMienTay.Infrastructure;
using ChoNoi.Application;
using ChoNoi.Presentation; 

namespace ChoNoi.Infrastructure
{
    [Serializable]
    public class InventoryItemSaveData
    {
        public string itemID;
        public int amount;
    }

    [Serializable]
    public class CargoSlotSaveData
    {
        public int slotIndex;
        public string itemID;
        public int amount;
    }

    [Serializable]
    public class GameSaveData
    {
        public int currentDay;
        public float currentTimeMinutes;
        public int currentMoney;
        public float currentStamina;
        public float maxStamina;
        public float maxBonusPriceRatio;

        public float maxWeightCapacity;
        public List<InventoryItemSaveData> inventoryItems = new List<InventoryItemSaveData>();
        public List<CargoSlotSaveData> cargoSlotsData = new List<CargoSlotSaveData>();

        public int storageLevel;
        public bool hasRoof;
        public int bambooPoleLevel;
        public int engineLevel;
        public float engineThrustMultiplier;

        public List<string> displayedItemIDs = new List<string>();

        public float currentDurability;
    }

    public class SaveLoadManager : MonoBehaviour
    {
        [Header("Managers")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private BoatCampManager boatCampManager;
        [SerializeField] private BambooPoleManager bambooPoleManager;
        [SerializeField] private DurabilityManager durabilityManager;
        [SerializeField] private TimeManager timeManager;

        [Header("Database")]
        [Tooltip("Kéo thả tất cả ItemData vào đây để Load được nhận diện")]
        [SerializeField] private List<ItemData> masterItemDatabase;

        private string SavePath => UnityEngine.Application.persistentDataPath + "/gamesave.json";

        public bool HasSaveFile => File.Exists(SavePath);

        public void NewGame()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
            }

            if (playerStats != null)
            {
                playerStats.LoadStats(100000, 100f, 100f, 0.5f);
            }

            if (timeManager != null)
            {
                timeManager.ResetTime();
            }

            if (boatCampManager != null)
            {
                boatCampManager.LoadData(0, false, 0, 0, 1f);
            }

            if (durabilityManager != null)
            {
                durabilityManager.LoadDurability(100f);
            }

            if (bambooPoleManager != null)
            {
                bambooPoleManager.ClearPole();
            }

            SeedFreshGame();
            SaveGame();
            Debug.Log("[SaveLoadManager] New game initialized and saved.");
        }

        private void OnEnable()
        {
            if (timeManager != null)
            {
                timeManager.OnSleep += SaveGame;
            }
        }

        private void OnDisable()
        {
            if (timeManager != null)
            {
                timeManager.OnSleep -= SaveGame;
            }
        }

        private void Start()
        {
            LoadGame();
        }

        public void SaveGame()
        {
            GameSaveData data = new GameSaveData();

            if (playerStats != null)
            {
                data.currentDay = timeManager != null ? timeManager.CurrentDay : 1;
                data.currentTimeMinutes = timeManager != null ? timeManager.MinutesOfDay : 180f;
                data.currentMoney = playerStats.CurrentMoney;
                data.currentStamina = playerStats.CurrentStamina;
                data.maxStamina = playerStats.MaxStamina;
                data.maxBonusPriceRatio = playerStats.maxBonusPriceRatio;
            }

            if (inventoryManager != null)
            {
                data.maxWeightCapacity = inventoryManager.MaxWeightCapacity;
                // Lưu dữ liệu inventory tổng thể để tương thích ngược
                foreach (var kvp in inventoryManager.Inventory)
                {
                    data.inventoryItems.Add(new InventoryItemSaveData
                    {
                        itemID = kvp.Key.itemID,
                        amount = kvp.Value
                    });
                }

                // Lưu dữ liệu vị trí các ô hàng trong lưới
                var slots = inventoryManager.CargoSlots;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].item != null && slots[i].amount > 0)
                    {
                        data.cargoSlotsData.Add(new CargoSlotSaveData
                        {
                            slotIndex = i,
                            itemID = slots[i].item.itemID,
                            amount = slots[i].amount
                        });
                    }
                }
            }

            if (boatCampManager != null)
            {
                data.storageLevel = boatCampManager.storageLevel;
                data.hasRoof = boatCampManager.hasRoof;
                data.bambooPoleLevel = boatCampManager.bambooPoleLevel;
                data.engineLevel = boatCampManager.engineLevel;
            }
            
            var boatController = FindFirstObjectByType<ChoNoi.Presentation.BoatController>();
            if (boatController != null)
            {
                data.engineThrustMultiplier = boatController.EngineThrustMultiplier;
            }
            else if (data.engineThrustMultiplier == 0)
            {
                data.engineThrustMultiplier = 1f;
            }

            if (bambooPoleManager != null)
            {
                foreach (var item in bambooPoleManager.DisplayedItems)
                {
                    data.displayedItemIDs.Add(item.itemID);
                }
            }

            if (durabilityManager != null)
            {
                data.currentDurability = durabilityManager.CurrentDurability;
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log($"[SaveLoadManager] Game Saved to {SavePath}");
        }

        public void LoadGame()
        {
            if (!File.Exists(SavePath))
            {
                Debug.Log("[SaveLoadManager] No save file found. Starting fresh.");
                SeedFreshGame();
                return;
            }

            string json = File.ReadAllText(SavePath);
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

            Dictionary<string, ItemData> itemDict = new Dictionary<string, ItemData>();
            if (masterItemDatabase != null)
            {
                foreach (var item in masterItemDatabase)
                {
                    if (item != null && !string.IsNullOrEmpty(item.itemID))
                    {
                        itemDict[item.itemID] = item;
                    }
                }
            }

            if (playerStats != null)
            {
                playerStats.LoadStats(data.currentMoney, data.currentStamina, data.maxStamina, data.maxBonusPriceRatio);
            }

            if (timeManager != null)
            {
                timeManager.LoadDay(data.currentDay);
                timeManager.MinutesOfDay = data.currentTimeMinutes > 0f ? data.currentTimeMinutes : 180f;
            }

            if (inventoryManager != null)
            {
                inventoryManager.LoadCapacity(data.maxWeightCapacity);
                inventoryManager.ClearInventory(); // Reset sạch ô và dict

                if (data.cargoSlotsData != null && data.cargoSlotsData.Count > 0)
                {
                    // Nạp chính xác vào vị trí từng ô chứa
                    var slots = inventoryManager.CargoSlots;
                    foreach (var slotSave in data.cargoSlotsData)
                    {
                        if (slotSave.slotIndex >= 0 && slotSave.slotIndex < slots.Count)
                        {
                            if (itemDict.TryGetValue(slotSave.itemID, out ItemData itemData))
                            {
                                slots[slotSave.slotIndex].item = itemData;
                                slots[slotSave.slotIndex].amount = slotSave.amount;
                            }
                        }
                    }
                    inventoryManager.SyncInventoryFromSlots();
                }
                else
                {
                    // Fallback nạp theo danh sách kiểu cũ nếu file save cũ không chứa dữ liệu ô
                    foreach (var invItem in data.inventoryItems)
                    {
                        if (itemDict.TryGetValue(invItem.itemID, out ItemData itemData))
                        {
                            inventoryManager.SetItemAmount(itemData, invItem.amount);
                        }
                        else
                        {
                            Debug.LogWarning($"[SaveLoadManager] Item {invItem.itemID} not found in master database.");
                        }
                    }
                }
            }

            if (boatCampManager != null)
            {
                float thrust = data.engineThrustMultiplier > 0 ? data.engineThrustMultiplier : 1f;
                boatCampManager.LoadData(data.storageLevel, data.hasRoof, data.bambooPoleLevel, data.engineLevel, thrust);
            }

            if (bambooPoleManager != null)
            {
                bambooPoleManager.ClearPole();
                List<ItemData> loadedDisplayItems = new List<ItemData>();
                foreach (var id in data.displayedItemIDs)
                {
                    if (itemDict.TryGetValue(id, out ItemData itemData))
                    {
                        loadedDisplayItems.Add(itemData);
                    }
                }
                bambooPoleManager.LoadDisplayedItems(loadedDisplayItems);
            }

            if (durabilityManager != null)
            {
                durabilityManager.LoadDurability(data.currentDurability);
            }

            Debug.Log("[SaveLoadManager] Game Loaded successfully.");
        }

        private void SeedFreshGame()
        {
            if (inventoryManager == null || masterItemDatabase == null || masterItemDatabase.Count == 0)
            {
                return;
            }

            inventoryManager.ClearInventory();

            if (masterItemDatabase.Count > 0 && masterItemDatabase[0] != null)
            {
                inventoryManager.BuyItem(masterItemDatabase[0], 3);
            }

            if (masterItemDatabase.Count > 1 && masterItemDatabase[1] != null)
            {
                inventoryManager.BuyItem(masterItemDatabase[1], 2);
            }

            if (masterItemDatabase.Count > 2 && masterItemDatabase[2] != null)
            {
                inventoryManager.BuyItem(masterItemDatabase[2], 1);
            }
        }
    }
}
