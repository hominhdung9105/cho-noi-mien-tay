using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChoNoi.Application;
using ChoNoi.Domain;
using ChoNoiMienTay.Presentation;
using ChoNoiMienTay.Infrastructure;
using ChoNoi.Presentation;
using ChoNoi.UI;
using System.Collections.Generic;

namespace ChoNoiMienTay.UI
{
    public class RiverMarketHUD : MonoBehaviour
    {
        [SerializeField] private TimeManager timeManager;
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private InventoryManager inventoryManager;
        [SerializeField] private BoatCampManager boatCampManager;
        [SerializeField] private MarketNewsController marketNewsController;
        [SerializeField] private EconomyManager economyManager;
        [SerializeField] private DurabilityManager durabilityManager;
        [SerializeField] private List<ItemData> marketItems = new List<ItemData>();

        [Header("Casual GUI Sprites")]
        public Sprite panelBgSprite;
        public Sprite buttonSpriteNormal;
        public Sprite buttonSpriteHover;
        public Sprite buttonSpritePressed;

        private Canvas canvas;
        private Text statsText;
        private Text timeText;
        private Text upgradeText;
        private Text newsText;
        private Text newsNpcNameText;
        private Text tradeText;
        private Text tradeTitleText;
        private Image newsAvatarImage;
        private GameObject upgradePanel;
        private GameObject tradePanel;
        private GameObject newsPanel;
        private GameObject phaseTransitionPanel;
        private Text phaseTransitionText;
        private CanvasGroup phaseTransitionCanvasGroup;
        private Coroutine phaseTransitionCoroutine;
        private int selectedMarketIndex;
        private bool isNpcTradeOpen;
        private string currentTradeNpcName = "Thuong Lai";

        public bool IsNpcTradeOpen => isNpcTradeOpen;
        public bool IsUpgradeOpen => upgradePanel != null && upgradePanel.activeSelf;
        public bool IsNewsOpen => newsPanel != null && newsPanel.activeSelf;

        public void SetCanvasActive(bool active)
        {
            BuildUIIfNeeded();
            if (canvas != null)
            {
                canvas.gameObject.SetActive(active);
            }
        }

        public void Configure(
            TimeManager timeSource,
            PlayerStats player,
            InventoryManager inventory,
            BoatCampManager camp,
            MarketNewsController newsController,
            EconomyManager economy,
            DurabilityManager durability,
            List<ItemData> items)
        {
            timeManager = timeSource;
            playerStats = player;
            inventoryManager = inventory;
            boatCampManager = camp;
            marketNewsController = newsController;
            economyManager = economy;
            durabilityManager = durability;
            marketItems = items ?? new List<ItemData>();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            if (timeManager == null) timeManager = FindAnyObjectByType<TimeManager>();
            if (playerStats == null) playerStats = FindAnyObjectByType<PlayerStats>();
            if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();
            if (boatCampManager == null) boatCampManager = FindAnyObjectByType<BoatCampManager>();
            if (marketNewsController == null) marketNewsController = FindAnyObjectByType<MarketNewsController>();
            if (economyManager == null) economyManager = FindAnyObjectByType<EconomyManager>();
            if (durabilityManager == null) durabilityManager = FindAnyObjectByType<DurabilityManager>();

            BuildUIIfNeeded();
            SubscribeEvents();
            RefreshAll();
        }

        private void SubscribeEvents()
        {
            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager.OnTimeChanged += HandleTimeChanged;
                timeManager.OnPhaseChanged -= HandlePhaseChanged;
                timeManager.OnPhaseChanged += HandlePhaseChanged;
                timeManager.OnDayChanged -= HandleDayChanged;
                timeManager.OnDayChanged += HandleDayChanged;
            }

            if (marketNewsController != null)
            {
                marketNewsController.OnNewsChanged -= HandleNewsChanged;
                marketNewsController.OnNewsChanged += HandleNewsChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (timeManager != null)
            {
                timeManager.OnTimeChanged -= HandleTimeChanged;
                timeManager.OnPhaseChanged -= HandlePhaseChanged;
                timeManager.OnDayChanged -= HandleDayChanged;
            }

            if (marketNewsController != null)
            {
                marketNewsController.OnNewsChanged -= HandleNewsChanged;
            }
        }

        private void BuildUIIfNeeded()
        {
            if (canvas != null) return;

            EnsureEventSystem();

            GameObject canvasObject = new GameObject("RiverMarketHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject topBar = CreatePanel("TopBar", canvasObject.transform, new Color(0f, 0f, 0f, 0.65f));
            Stretch(topBar.GetComponent<RectTransform>(), new Vector2(0.02f, 0.90f), new Vector2(0.98f, 0.98f));
            
            statsText = CreateText("StatsText", topBar.transform, 24, TextAnchor.MiddleLeft);
            Stretch(statsText.rectTransform, new Vector2(0.02f, 0.15f), new Vector2(0.60f, 0.85f));

            timeText = CreateText("TimeText", topBar.transform, 24, TextAnchor.MiddleRight);
            Stretch(timeText.rectTransform, new Vector2(0.70f, 0.15f), new Vector2(0.98f, 0.85f));

            upgradePanel = CreatePanel("UpgradePanel", canvasObject.transform, new Color(0.08f, 0.16f, 0.18f, 0.86f));
            Stretch(upgradePanel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.08f), new Vector2(0.26f, 0.86f));
            CreateText("UpgradeTitle", upgradePanel.transform, 26, TextAnchor.MiddleCenter).text = "TRẠI GHE XÓM NƯỚC";
            Stretch(upgradePanel.transform.Find("UpgradeTitle").GetComponent<RectTransform>(), new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));

            upgradeText = CreateText("UpgradeText", upgradePanel.transform, 22, TextAnchor.UpperLeft);
            Stretch(upgradeText.rectTransform, new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.82f));

            CreateActionButton(upgradePanel.transform, "Khoang Chứa", new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.53f), () =>
            {
                if (boatCampManager != null && boatCampManager.TryBuyNextStorageUpgrade()) RefreshAll();
            });
            CreateActionButton(upgradePanel.transform, "Động Cơ", new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.43f), () =>
            {
                if (boatCampManager != null && boatCampManager.TryBuyNextEngineUpgrade()) RefreshAll();
            });
            CreateActionButton(upgradePanel.transform, "Lợp Mái", new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.33f), () =>
            {
                if (boatCampManager != null && boatCampManager.TryBuyRoofUpgrade()) RefreshAll();
            });
            CreateActionButton(upgradePanel.transform, "Cây Bẹo", new Vector2(0.08f, 0.15f), new Vector2(0.92f, 0.23f), () =>
            {
                if (boatCampManager != null && boatCampManager.TryBuyNextBambooPoleUpgrade()) RefreshAll();
            });
            CreateActionButton(upgradePanel.transform, "Đóng", new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.13f), CloseAllPanels);

            tradePanel = CreatePanel("TradePanel", canvasObject.transform, new Color(0.12f, 0.12f, 0.08f, 0.88f));
            Stretch(tradePanel.GetComponent<RectTransform>(), new Vector2(0.31f, 0.08f), new Vector2(0.69f, 0.44f));
            tradeTitleText = CreateText("TradeTitle", tradePanel.transform, 24, TextAnchor.MiddleCenter);
            tradeTitleText.text = "CHỢ NỔI TRUNG TÂM";
            Stretch(tradeTitleText.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));

            tradeText = CreateText("TradeText", tradePanel.transform, 20, TextAnchor.UpperLeft);
            Stretch(tradeText.rectTransform, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.82f));

            CreateActionButton(tradePanel.transform, "Mặt Hàng Trước", new Vector2(0.08f, 0.34f), new Vector2(0.44f, 0.42f), SelectPreviousItem);
            CreateActionButton(tradePanel.transform, "Mặt Hàng Sau", new Vector2(0.56f, 0.34f), new Vector2(0.92f, 0.42f), SelectNextItem);
            CreateActionButton(tradePanel.transform, "Mua 1", new Vector2(0.08f, 0.22f), new Vector2(0.44f, 0.30f), BuySelectedItem);
            CreateActionButton(tradePanel.transform, "Bán 1", new Vector2(0.56f, 0.22f), new Vector2(0.92f, 0.30f), SellSelectedItem);
            CreateActionButton(tradePanel.transform, "Sửa Ghe", new Vector2(0.08f, 0.10f), new Vector2(0.44f, 0.18f), RepairBoat);
            CreateActionButton(tradePanel.transform, "Hồi Thể Lực", new Vector2(0.56f, 0.10f), new Vector2(0.92f, 0.18f), RestoreStamina);

            newsPanel = CreatePanel("NewsPanel", canvasObject.transform, new Color(0.14f, 0.10f, 0.06f, 0.88f));
            Stretch(newsPanel.GetComponent<RectTransform>(), new Vector2(0.72f, 0.08f), new Vector2(0.98f, 0.44f));
            CreateText("NewsTitle", newsPanel.transform, 24, TextAnchor.MiddleCenter).text = "TIN ĐỒN THỊ TRƯỜNG";
            Stretch(newsPanel.transform.Find("NewsTitle").GetComponent<RectTransform>(), new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));

            newsAvatarImage = CreateImage("NpcAvatar", newsPanel.transform);
            Stretch(newsAvatarImage.rectTransform, new Vector2(0.06f, 0.46f), new Vector2(0.34f, 0.82f));

            newsNpcNameText = CreateText("NpcName", newsPanel.transform, 20, TextAnchor.MiddleCenter);
            Stretch(newsNpcNameText.rectTransform, new Vector2(0.04f, 0.38f), new Vector2(0.36f, 0.46f));

            newsText = CreateText("NewsText", newsPanel.transform, 20, TextAnchor.UpperLeft);
            Stretch(newsText.rectTransform, new Vector2(0.40f, 0.10f), new Vector2(0.94f, 0.82f));

            CreateActionButton(newsPanel.transform, "Ngủ Đến Sáng", new Vector2(0.06f, 0.08f), new Vector2(0.34f, 0.18f), () =>
            {
                if (timeManager != null)
                {
                    timeManager.Sleep();
                    RefreshAll();
                }
            });

            SetPanelVisible(upgradePanel, false);
            SetPanelVisible(tradePanel, false);
            SetPanelVisible(newsPanel, false);

            // Phase Transition Panel (Middle of screen)
            phaseTransitionPanel = CreatePanel("PhaseTransitionPanel", canvasObject.transform, new Color(0f, 0f, 0f, 0.8f));
            Stretch(phaseTransitionPanel.GetComponent<RectTransform>(), new Vector2(0.32f, 0.42f), new Vector2(0.68f, 0.58f));
            
            
            phaseTransitionCanvasGroup = phaseTransitionPanel.AddComponent<CanvasGroup>();
            phaseTransitionCanvasGroup.alpha = 0f;
            
            phaseTransitionText = CreateText("PhaseTransitionText", phaseTransitionPanel.transform, 32, TextAnchor.MiddleCenter);
            phaseTransitionText.font = FontHelper.GameBoldFont;
            phaseTransitionText.color = new Color(0.92f, 0.82f, 0.55f, 1f); // Gold/Yellow
            Stretch(phaseTransitionText.rectTransform, Vector2.zero, Vector2.one);
            
            phaseTransitionPanel.SetActive(false);
        }

        private void HandleTimeChanged(int _, int __) => RefreshStatus();
        private void HandlePhaseChanged(GamePhase phase)
        {
            RefreshStatus();
            ShowPhaseTransition(phase);
        }
        private void HandleDayChanged(int _) => RefreshAll();
        private void HandleNewsChanged(Infrastructure.MarketNewsEntry _) => RefreshNews();

        private void ShowPhaseTransition(GamePhase phase)
        {
            if (phaseTransitionPanel == null) return;

            bool isEn = FullSimulatorUI.CurrentLanguage == "en";
            string phaseStr = "";
            switch (phase)
            {
                case GamePhase.Dawn: phaseStr = isEn ? "DAWN" : "BÌNH MINH"; break;
                case GamePhase.Day: phaseStr = isEn ? "DAYTIME" : "BAN NGÀY"; break;
                case GamePhase.Dusk: phaseStr = isEn ? "DUSK" : "CHIỀU TÀ"; break;
                case GamePhase.Night: phaseStr = isEn ? "NIGHT" : "BAN ĐÊM"; break;
            }

            phaseTransitionText.text = phaseStr;

            if (phaseTransitionCoroutine != null)
            {
                StopCoroutine(phaseTransitionCoroutine);
            }
            phaseTransitionCoroutine = StartCoroutine(FadePhaseTransition());
        }

        private System.Collections.IEnumerator FadePhaseTransition()
        {
            phaseTransitionPanel.SetActive(true);
            phaseTransitionCanvasGroup.alpha = 0f;

            // Fade in
            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                phaseTransitionCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                yield return null;
            }
            phaseTransitionCanvasGroup.alpha = 1f;

            // Hold
            yield return new WaitForSeconds(2.0f);

            // Fade out
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                phaseTransitionCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                yield return null;
            }
            phaseTransitionCanvasGroup.alpha = 0f;
            phaseTransitionPanel.SetActive(false);
        }

        public void RefreshAll()
        {
            TranslateHUD();
            RefreshStatus();
            RefreshUpgrades();
            RefreshTrade();
            RefreshNews();
        }

        private void RefreshStatus()
        {
            UpdateTopBarText();
        }

        private void UpdateTopBarText()
        {
            if (statsText == null || timeText == null) return;

            bool isEn = FullSimulatorUI.CurrentLanguage == "en";

            string money = playerStats != null ? playerStats.CurrentMoney.ToString("N0") : "0";
            string stamina = playerStats != null ? $"{playerStats.CurrentStamina:0}/{playerStats.MaxStamina:0}" : "0/0";
            string durability = durabilityManager != null ? $"{durabilityManager.CurrentDurability:0}/{durabilityManager.MaxDurability:0}" : "0/0";
            
            float weight = inventoryManager != null ? inventoryManager.CurrentTotalWeight : 0f;
            float maxWeight = inventoryManager != null ? inventoryManager.MaxWeightCapacity : 100f;

            string phaseName = isEn ? "Dawn" : "Bình Minh";
            if (timeManager != null)
            {
                switch (timeManager.CurrentPhase)
                {
                    case ChoNoi.Domain.GamePhase.Dawn: phaseName = isEn ? "Dawn" : "Bình Minh"; break;
                    case ChoNoi.Domain.GamePhase.Day: phaseName = isEn ? "Daytime" : "Ban Ngày"; break;
                    case ChoNoi.Domain.GamePhase.Dusk: phaseName = isEn ? "Dusk" : "Chiều Tà"; break;
                    case ChoNoi.Domain.GamePhase.Night: phaseName = isEn ? "Night" : "Ban Đêm"; break;
                }
            }
            string timeClock = timeManager != null ? $"{timeManager.CurrentHour:00}:{timeManager.CurrentMinute:00}" : "03:00";
            string dayPhase = timeManager != null 
                ? (isEn ? $"Day {timeManager.CurrentDay} | {phaseName} ({timeClock})" : $"Ngày {timeManager.CurrentDay} | {phaseName} ({timeClock})")
                : (isEn ? $"Day 1 | Dawn (03:00)" : "Ngày 1 | Bình Minh (03:00)");

            statsText.text = isEn 
                ? $"Money: {money}đ | Stamina: {stamina} | Weight: {weight:0}/{maxWeight:0} kg | Durability: {durability}"
                : $"Tiền: {money}đ | Thể lực: {stamina} | Tải trọng: {weight:0}/{maxWeight:0} kg | Độ bền: {durability}";
            timeText.text = $"{dayPhase}";
        }

        private void TranslateHUD()
        {
            bool isEn = FullSimulatorUI.CurrentLanguage == "en";
            
            if (upgradePanel != null)
            {
                Transform title = upgradePanel.transform.Find("Title");
                if (title != null) title.GetComponent<Text>().text = isEn ? "BOAT UPGRADES" : "NÂNG CẤP GHE";

                string[] viUpgrades = { "Khoang Chứa", "Động Cơ", "Lợp Mái", "Cây Bẹo", "Đóng" };
                string[] enUpgrades = { "Storage", "Engine", "Roof", "Bamboo Pole", "Close" };

                for (int i = 0; i < viUpgrades.Length; i++)
                {
                    Transform btn = upgradePanel.transform.Find(viUpgrades[i]);
                    if (btn == null) btn = upgradePanel.transform.Find(enUpgrades[i]);
                    if (btn != null)
                    {
                        btn.name = isEn ? enUpgrades[i] : viUpgrades[i];
                        btn.transform.Find("Label").GetComponent<Text>().text = isEn ? enUpgrades[i] : viUpgrades[i];
                    }
                }
            }

            if (tradePanel != null)
            {
                string[] viTrades = { "Mặt Hàng Trước", "Mặt Hàng Sau", "Mua 1", "Bán 1", "Sửa Ghe", "Hồi Thể Lực" };
                string[] enTrades = { "Prev Item", "Next Item", "Buy 1", "Sell 1", "Repair Boat", "Rest Stamina" };

                for (int i = 0; i < viTrades.Length; i++)
                {
                    Transform btn = tradePanel.transform.Find(viTrades[i]);
                    if (btn == null) btn = tradePanel.transform.Find(enTrades[i]);
                    if (btn != null)
                    {
                        btn.name = isEn ? enTrades[i] : viTrades[i];
                        btn.transform.Find("Label").GetComponent<Text>().text = isEn ? enTrades[i] : viTrades[i];
                    }
                }
            }

            if (newsPanel != null)
            {
                Transform btn = newsPanel.transform.Find("Ngủ Đến Sáng");
                if (btn == null) btn = newsPanel.transform.Find("Sleep Till Dawn");
                if (btn != null)
                {
                    btn.name = isEn ? "Sleep Till Dawn" : "Ngủ Đến Sáng";
                    btn.transform.Find("Label").GetComponent<Text>().text = isEn ? "Sleep Till Dawn" : "Ngủ Đến Sáng";
                }
            }
        }

        private void RefreshUpgrades()
        {
            if (upgradeText == null || boatCampManager == null || boatCampManager.UpgradeCatalog == null) return;

            Infrastructure.StorageUpgradeTier storageTier = boatCampManager.UpgradeCatalog.GetStorageTier(boatCampManager.StorageLevel);
            Infrastructure.EngineUpgradeTier engineTier = boatCampManager.UpgradeCatalog.GetEngineTier(boatCampManager.engineLevel);
            Infrastructure.BambooPoleUpgradeTier bambooTier = boatCampManager.UpgradeCatalog.GetBambooPoleTier(boatCampManager.bambooPoleLevel);
            Infrastructure.RoofUpgradeTier roofTier = boatCampManager.UpgradeCatalog.GetRoofTier(boatCampManager.hasRoof ? 1 : 0);

            upgradeText.text =
                $"Khoang chứa: Cấp {boatCampManager.StorageLevel} | Tiếp: {(storageTier != null ? storageTier.costMoney.ToString("N0") + "đ" : "TỐI ĐA")}\n" +
                $"Động cơ: Cấp {boatCampManager.engineLevel} | Tiếp: {(engineTier != null ? engineTier.costMoney.ToString("N0") + "đ" : "TỐI ĐA")}\n" +
                $"Mái che: {(boatCampManager.hasRoof ? "Đã lợp" : (roofTier != null ? roofTier.costMoney.ToString("N0") + "đ" : "TỐI ĐA"))}\n" +
                $"Cây Bẹo: Cấp {boatCampManager.bambooPoleLevel} | Tiếp: {(bambooTier != null ? bambooTier.costMoney.ToString("N0") + "đ" : "TỐI ĐA")}";
        }

        private void RefreshTrade()
        {
            if (tradeText == null)
                return;

            ItemData currentItem = GetSelectedMarketItem();
            if (currentItem == null)
            {
                tradeText.text = "Chưa có dữ liệu hàng hóa để giao dịch.";
                return;
            }

            int currentOwned = 0;
            if (inventoryManager != null && inventoryManager.Inventory.TryGetValue(currentItem, out int amount))
            {
                currentOwned = amount;
            }

            int buyPrice = currentItem.basePrice;
            int sellPrice = Mathf.RoundToInt(currentItem.basePrice * GetSaleMultiplier(currentItem));
            int repairCost = GetRepairCost();
            tradeText.text =
                $"{currentItem.itemName} ({currentItem.itemID})\n" +
                $"Giá mua: {buyPrice:N0}đ | Giá bán: {sellPrice:N0}đ\n" +
                $"Tồn kho: {currentOwned} | Nặng: {currentItem.weight:0.0} kg\n" +
                $"Sửa ghe: {repairCost:N0}đ | Thể lực hồi: 8,000";
        }

        private void RefreshNews()
        {
            if (newsText == null) return;

            Infrastructure.MarketNewsEntry entry = marketNewsController != null ? marketNewsController.CurrentNews : null;
            if (entry == null)
            {
                newsNpcNameText.text = "Không có NPC";
                newsAvatarImage.sprite = null;
                newsAvatarImage.color = new Color(1f, 1f, 1f, 0.15f);
                newsText.text = "Chưa có tin đồn thị trường được cập nhật.";
                return;
            }

            newsNpcNameText.text = entry.npcName;
            newsAvatarImage.sprite = entry.npcAvatar;
            newsAvatarImage.color = Color.white;
            newsText.text = $"{entry.headline}\n\n{entry.marketRumor}";
        }

        private ItemData GetSelectedMarketItem()
        {
            if (marketItems == null || marketItems.Count == 0)
            {
                return null;
            }

            selectedMarketIndex = Mathf.Clamp(selectedMarketIndex, 0, marketItems.Count - 1);
            return marketItems[selectedMarketIndex];
        }

        private void SelectPreviousItem()
        {
            if (marketItems == null || marketItems.Count == 0) return;
            selectedMarketIndex = (selectedMarketIndex - 1 + marketItems.Count) % marketItems.Count;
            RefreshTrade();
        }

        private void SelectNextItem()
        {
            if (marketItems == null || marketItems.Count == 0) return;
            selectedMarketIndex = (selectedMarketIndex + 1) % marketItems.Count;
            RefreshTrade();
        }

        private void BuySelectedItem()
        {
            ItemData currentItem = GetSelectedMarketItem();
            if (currentItem != null && economyManager != null && inventoryManager != null)
            {
                economyManager.BuyItemToInventory(currentItem, 1, inventoryManager);
                RefreshAll();
            }
        }

        private void SellSelectedItem()
        {
            ItemData currentItem = GetSelectedMarketItem();
            if (currentItem == null || economyManager == null || inventoryManager == null)
                return;

            int finalRevenue = Mathf.RoundToInt(currentItem.basePrice * GetSaleMultiplier(currentItem));
            economyManager.SellItemWholesale(currentItem, 1, inventoryManager, finalRevenue);
            RefreshAll();
        }

        private void RepairBoat()
        {
            if (economyManager == null || durabilityManager == null)
                return;

            ServiceData repairService = ScriptableObject.CreateInstance<ServiceData>();
            repairService.serviceName = "Sua Ghe";
            repairService.costMoney = GetRepairCost();
            repairService.durabilityRestoreAmount = 18f;
            economyManager.BuyService(repairService);
            Destroy(repairService);
            RefreshAll();
        }

        private void RestoreStamina()
        {
            if (economyManager == null)
                return;

            ServiceData mealService = ScriptableObject.CreateInstance<ServiceData>();
            mealService.serviceName = "Bun Nuoc Leo";
            mealService.costMoney = 8000;
            mealService.staminaRestoreAmount = 18f;
            economyManager.BuyService(mealService);
            Destroy(mealService);
            RefreshAll();
        }

        private int GetRepairCost()
        {
            if (durabilityManager == null)
                return 12000;

            float missingDurability = durabilityManager.MaxDurability - durabilityManager.CurrentDurability;
            return 6000 + Mathf.RoundToInt(missingDurability * 220f);
        }

        private float GetSaleMultiplier(ItemData item)
        {
            float multiplier = 1.05f + Mathf.Min(0.18f, (timeManager != null ? (timeManager.CurrentDay - 1) : 0) * 0.04f);
            string rumor = marketNewsController != null && marketNewsController.CurrentNews != null
                ? marketNewsController.CurrentNews.marketRumor.ToLowerInvariant()
                : string.Empty;

            string itemName = item != null && !string.IsNullOrEmpty(item.itemName)
                ? item.itemName.ToLowerInvariant()
                : string.Empty;

            if (!string.IsNullOrEmpty(itemName) && rumor.Contains(itemName))
            {
                multiplier += 0.12f;
            }

            return multiplier;
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            bool useSVG = (name != "LeftPromptPanel" && name != "RightPromptPanel" && name != "TopBar" &&
                           name != "Background" && name != "Fill" && name != "Bg" &&
                           !name.Contains("Slider") && !name.Contains("Scroll"));

            GameObject panel;
            Sprite sp = GetPanelBackgroundSprite();
            if (useSVG && sp != null)
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                Image img = panel.GetComponent<Image>();
                img.sprite = sp;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                panel.GetComponent<Image>().color = color;
            }

            return panel;
        }

        private Text CreateText(string name, Transform parent, int fontSize, TextAnchor alignment)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = FontHelper.GameFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Image CreateImage(string name, Transform parent)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.15f);
            return image;
        }

        private Button CreateActionButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonObject;
            if (buttonSpriteNormal != null)
            {
                buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(parent, false);
                Stretch(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

                Image img = buttonObject.GetComponent<Image>();
                img.sprite = buttonSpriteNormal;
                img.type = Image.Type.Sliced;
                img.color = Color.white;

                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = img;
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = new SpriteState();
                state.highlightedSprite = buttonSpriteHover != null ? buttonSpriteHover : buttonSpriteNormal;
                state.pressedSprite = buttonSpritePressed != null ? buttonSpritePressed : buttonSpriteNormal;
                button.spriteState = state;
            }
            else
            {
                buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(parent, false);
                Stretch(buttonObject.GetComponent<RectTransform>(), anchorMin, anchorMax);

                Image img = buttonObject.GetComponent<Image>();
                img.color = new Color(0.88f, 0.71f, 0.34f, 1f);

                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = img;
            }

            Button btnComp = buttonObject.GetComponent<Button>();
            if (onClick != null)
            {
                btnComp.onClick.RemoveAllListeners();
                btnComp.onClick.AddListener(onClick);
            }

            Text text = CreateText("Label", buttonObject.transform, 20, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
            text.font = FontHelper.GameBoldFont;
            text.text = label;
            text.color = Color.white;

            btnComp.onClick.AddListener(() => {
                SoundManager.Instance.PlaySFX("click");
            });

            EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = buttonObject.AddComponent<EventTrigger>();
            
            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => {
                SoundManager.Instance.PlaySFX("hover");
            });
            trigger.triggers.Add(entry);

            return btnComp;
        }

        private Sprite GetPanelBackgroundSprite()
        {
            return panelBgSprite;
        }

        private void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public void OpenUpgradePanel()
        {
            CloseAllPanels();
            SetPanelVisible(upgradePanel, true);
        }

        public void OpenNewsPanel()
        {
            CloseAllPanels();
            SetPanelVisible(newsPanel, true);
        }

        public void CloseAllPanels()
        {
            SetPanelVisible(upgradePanel, false);
            SetPanelVisible(tradePanel, false);
            SetPanelVisible(newsPanel, false);
            isNpcTradeOpen = false;
        }

        private void ToggleUpgradePanel()
        {
            bool wasActive = upgradePanel != null && upgradePanel.activeSelf;
            CloseAllPanels();
            if (!wasActive) SetPanelVisible(upgradePanel, true);
        }

        private void ToggleTradePanel()
        {
            bool wasActive = tradePanel != null && tradePanel.activeSelf;
            CloseAllPanels();
            if (!wasActive) SetPanelVisible(tradePanel, true);
        }

        private void ToggleNewsPanel()
        {
            bool wasActive = newsPanel != null && newsPanel.activeSelf;
            CloseAllPanels();
            if (!wasActive) SetPanelVisible(newsPanel, true);
        }

        public void OpenNpcTrade(string npcName)
        {
            currentTradeNpcName = string.IsNullOrWhiteSpace(npcName) ? "Thuong Lai" : npcName;
            isNpcTradeOpen = true;
            if (tradeTitleText != null)
                tradeTitleText.text = $"GIAO DICH - {currentTradeNpcName.ToUpperInvariant()}";
            SetPanelVisible(tradePanel, true);
            RefreshTrade();
        }

        public void CloseNpcTrade()
        {
            isNpcTradeOpen = false;
            if (tradeTitleText != null)
                tradeTitleText.text = "CHO NOI TRUNG TAM";
            SetPanelVisible(tradePanel, false);
        }

        private void SetPanelVisible(GameObject panel, bool isVisible)
        {
            if (panel != null)
                panel.SetActive(isVisible);
        }
    }
}
