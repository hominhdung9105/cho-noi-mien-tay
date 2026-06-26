using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChoNoiMienTay.Data;
using ChoNoiMienTay.Infrastructure;
using ChoNoiMienTay.Presentation;
using ChoNoiMienTay.Systems;
using ChoNoi.UI;

namespace ChoNoiMienTay.UI
{
    public class BargainingPrototypeUI : MonoBehaviour
    {
        private enum PrototypeScreen
        {
            Inventory,
            Shop,
            Bargain
        }

        private BargainingSystem bargainingSystem;
        private PlayerStats playerStats;
        private InventoryManager inventoryManager;

        private Canvas canvas;
        private Text hudText;
        private Text inventoryListText;
        private Text inventoryDetailText;
        private Text shopDetailText;
        private Text bargainNpcNameText;
        private Text bargainOfferText;
        private Text bargainMessageText;
        private Image bargainNpcAvatar;
        private Button openShopButton;
        private Button acceptButton;
        private Button offerUpButton;
        private Button offerDownButton;
        private GameObject inventoryScreen;
        private GameObject shopScreen;
        private GameObject bargainScreen;
        private readonly List<Button> inventoryButtons = new List<Button>();
        private readonly List<GameObject> npcCards = new List<GameObject>();

        [Header("Casual GUI Sprites")]
        public Sprite panelBgSprite;
        public Sprite buttonSpriteNormal;
        public Sprite buttonSpriteHover;
        public Sprite buttonSpritePressed;

        private PrototypeScreen currentScreen = PrototypeScreen.Inventory;
        private bool isHidden = true;

        public bool IsHidden => isHidden;

        public void ToggleVisibility(bool visible)
        {
            isHidden = !visible;
            if (canvas != null)
            {
                // Just toggle the background/parent completely
                canvas.gameObject.SetActive(visible);
            }
            if (visible)
            {
                RefreshUI();
            }
        }

        public void Configure(BargainingSystem system, PlayerStats player, InventoryManager inventory)
        {
            bargainingSystem = system;
            playerStats = player;
            inventoryManager = inventory;
        }

        private void OnEnable()
        {
            if (bargainingSystem != null)
            {
                bargainingSystem.OnStateChanged += RefreshUI;
            }
        }

        private void OnDisable()
        {
            if (bargainingSystem != null)
            {
                bargainingSystem.OnStateChanged -= RefreshUI;
            }
        }

        private void Start()
        {
            BuildUIIfNeeded();

            if (bargainingSystem != null)
            {
                bargainingSystem.OnStateChanged -= RefreshUI;
                bargainingSystem.OnStateChanged += RefreshUI;
            }

            RefreshUI();
        }

        private void BuildUIIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            EnsureEventSystem();

            GameObject canvasObject = new GameObject("BargainingPrototypeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            // Set canvas disabled by default if hidden
            canvasObject.SetActive(!isHidden);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject background = CreatePanel("Background", canvasObject.transform, new Color(0.07f, 0.10f, 0.12f, 0.96f));
            Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            GameObject hudPanel = CreatePanel("HUD", background.transform, new Color(0.12f, 0.17f, 0.16f, 0.96f));
            Stretch(hudPanel.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.98f, 0.98f), Vector2.zero, Vector2.zero);
            hudText = CreateText("HudText", hudPanel.transform, 28, TextAnchor.MiddleLeft);
            Stretch(hudText.rectTransform, new Vector2(0.03f, 0.15f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);

            inventoryScreen = BuildInventoryScreen(background.transform);
            shopScreen = BuildShopScreen(background.transform);
            bargainScreen = BuildBargainScreen(background.transform);
        }

        private GameObject BuildInventoryScreen(Transform parent)
        {
            GameObject screen = CreatePanel("InventoryScreen", parent, new Color(0.13f, 0.19f, 0.17f, 0.95f));
            Stretch(screen.GetComponent<RectTransform>(), new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);

            CreateTitle(screen.transform, "1. Inventory Screen");
            CreateSubtitle(screen.transform, "Chọn nông sản đang có trên ghe rồi mang sang khu chợ để mặc cả.");

            GameObject leftPanel = CreatePanel("InventoryListPanel", screen.transform, new Color(0.17f, 0.24f, 0.22f, 0.92f));
            Stretch(leftPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.08f), new Vector2(0.55f, 0.82f), Vector2.zero, Vector2.zero);

            inventoryListText = CreateText("InventoryListText", leftPanel.transform, 24, TextAnchor.UpperLeft);
            Stretch(inventoryListText.rectTransform, new Vector2(0.50f, 0.08f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero);

            GameObject buttonColumn = new GameObject("InventoryButtons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            buttonColumn.transform.SetParent(leftPanel.transform, false);
            RectTransform buttonColumnRect = buttonColumn.GetComponent<RectTransform>();
            Stretch(buttonColumnRect, new Vector2(0.04f, 0.08f), new Vector2(0.45f, 0.92f), Vector2.zero, Vector2.zero);
            VerticalLayoutGroup buttonLayout = buttonColumn.GetComponent<VerticalLayoutGroup>();
            buttonLayout.spacing = 12f;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = true;
            buttonLayout.childForceExpandHeight = false;
            ContentSizeFitter buttonFitter = buttonColumn.GetComponent<ContentSizeFitter>();
            buttonFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject detailPanel = CreatePanel("InventoryDetailPanel", screen.transform, new Color(0.22f, 0.30f, 0.27f, 0.92f));
            Stretch(detailPanel.GetComponent<RectTransform>(), new Vector2(0.60f, 0.22f), new Vector2(0.97f, 0.82f), Vector2.zero, Vector2.zero);
            inventoryDetailText = CreateText("InventoryDetailText", detailPanel.transform, 24, TextAnchor.UpperLeft);
            Stretch(inventoryDetailText.rectTransform, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.88f), Vector2.zero, Vector2.zero);

            openShopButton = CreateButton("OpenShopButton", detailPanel.transform, "Mang Ra Chợ");
            Stretch(openShopButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.04f), new Vector2(0.90f, 0.18f), Vector2.zero, Vector2.zero);
            openShopButton.onClick.AddListener(() =>
            {
                if (bargainingSystem != null && bargainingSystem.SelectedItem != null)
                {
                    ShowScreen(PrototypeScreen.Shop);
                }
            });

            return screen;
        }

        private GameObject BuildShopScreen(Transform parent)
        {
            GameObject screen = CreatePanel("ShopScreen", parent, new Color(0.16f, 0.18f, 0.22f, 0.95f));
            Stretch(screen.GetComponent<RectTransform>(), new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);

            CreateTitle(screen.transform, "2. Shop Menu Screen");
            CreateSubtitle(screen.transform, "Chọn NPC đang chờ giao dịch. Avatar sẽ đổi theo người mua.");

            Button backButton = CreateButton("BackToInventoryButton", screen.transform, "Quay Lại Kho");
            Stretch(backButton.GetComponent<RectTransform>(), new Vector2(0.03f, 0.86f), new Vector2(0.18f, 0.94f), Vector2.zero, Vector2.zero);
            backButton.onClick.AddListener(() => ShowScreen(PrototypeScreen.Inventory));

            GameObject detailPanel = CreatePanel("ShopDetailPanel", screen.transform, new Color(0.20f, 0.23f, 0.28f, 0.92f));
            Stretch(detailPanel.GetComponent<RectTransform>(), new Vector2(0.03f, 0.12f), new Vector2(0.38f, 0.78f), Vector2.zero, Vector2.zero);
            shopDetailText = CreateText("ShopDetailText", detailPanel.transform, 24, TextAnchor.UpperLeft);
            Stretch(shopDetailText.rectTransform, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.90f), Vector2.zero, Vector2.zero);

            GameObject npcColumn = new GameObject("NpcCards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            npcColumn.transform.SetParent(screen.transform, false);
            RectTransform npcColumnRect = npcColumn.GetComponent<RectTransform>();
            Stretch(npcColumnRect, new Vector2(0.42f, 0.18f), new Vector2(0.96f, 0.72f), Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup npcLayout = npcColumn.GetComponent<HorizontalLayoutGroup>();
            npcLayout.spacing = 18f;
            npcLayout.padding = new RectOffset(0, 0, 0, 0);
            npcLayout.childControlWidth = true;
            npcLayout.childControlHeight = true;
            npcLayout.childForceExpandWidth = true;
            npcLayout.childForceExpandHeight = true;

            return screen;
        }

        private GameObject BuildBargainScreen(Transform parent)
        {
            GameObject screen = CreatePanel("BargainScreen", parent, new Color(0.23f, 0.17f, 0.14f, 0.96f));
            Stretch(screen.GetComponent<RectTransform>(), new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.85f), Vector2.zero, Vector2.zero);

            CreateTitle(screen.transform, "3. Bargaining Minigame");
            CreateSubtitle(screen.transform, "Mỗi lần tăng hoặc giảm giá sẽ tốn thể lực. Chốt khi thấy mức hợp lý.");

            Button backButton = CreateButton("BackToShopButton", screen.transform, "Về Shop");
            Stretch(backButton.GetComponent<RectTransform>(), new Vector2(0.03f, 0.86f), new Vector2(0.15f, 0.94f), Vector2.zero, Vector2.zero);
            backButton.onClick.AddListener(() => ShowScreen(PrototypeScreen.Shop));

            GameObject avatarPanel = CreatePanel("AvatarPanel", screen.transform, new Color(0.30f, 0.21f, 0.17f, 0.92f));
            Stretch(avatarPanel.GetComponent<RectTransform>(), new Vector2(0.04f, 0.20f), new Vector2(0.28f, 0.78f), Vector2.zero, Vector2.zero);
            bargainNpcAvatar = CreateImage("NpcAvatar", avatarPanel.transform, new Color(1f, 1f, 1f, 0.95f));
            Stretch(bargainNpcAvatar.rectTransform, new Vector2(0.12f, 0.18f), new Vector2(0.88f, 0.88f), Vector2.zero, Vector2.zero);
            bargainNpcNameText = CreateText("NpcNameText", avatarPanel.transform, 24, TextAnchor.MiddleCenter);
            Stretch(bargainNpcNameText.rectTransform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.16f), Vector2.zero, Vector2.zero);

            GameObject offerPanel = CreatePanel("OfferPanel", screen.transform, new Color(0.38f, 0.25f, 0.20f, 0.92f));
            Stretch(offerPanel.GetComponent<RectTransform>(), new Vector2(0.33f, 0.20f), new Vector2(0.96f, 0.78f), Vector2.zero, Vector2.zero);

            bargainOfferText = CreateText("OfferText", offerPanel.transform, 28, TextAnchor.UpperLeft);
            Stretch(bargainOfferText.rectTransform, new Vector2(0.06f, 0.56f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);

            bargainMessageText = CreateText("BargainMessageText", offerPanel.transform, 24, TextAnchor.UpperLeft);
            Stretch(bargainMessageText.rectTransform, new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.54f), Vector2.zero, Vector2.zero);

            offerDownButton = CreateButton("OfferDownButton", offerPanel.transform, "Giảm Giá");
            Stretch(offerDownButton.GetComponent<RectTransform>(), new Vector2(0.06f, 0.08f), new Vector2(0.28f, 0.20f), Vector2.zero, Vector2.zero);
            offerDownButton.onClick.AddListener(() => bargainingSystem?.AdjustOffer(-1));

            offerUpButton = CreateButton("OfferUpButton", offerPanel.transform, "Tăng Giá");
            Stretch(offerUpButton.GetComponent<RectTransform>(), new Vector2(0.32f, 0.08f), new Vector2(0.54f, 0.20f), Vector2.zero, Vector2.zero);
            offerUpButton.onClick.AddListener(() => bargainingSystem?.AdjustOffer(1));

            acceptButton = CreateButton("AcceptButton", offerPanel.transform, "Chốt Kèo");
            Stretch(acceptButton.GetComponent<RectTransform>(), new Vector2(0.58f, 0.08f), new Vector2(0.76f, 0.20f), Vector2.zero, Vector2.zero);
            acceptButton.onClick.AddListener(() =>
            {
                if (bargainingSystem != null && bargainingSystem.TryAcceptDeal())
                {
                    ShowScreen(PrototypeScreen.Inventory);
                }
            });

            Button rejectButton = CreateButton("RejectButton", offerPanel.transform, "Từ Chối");
            Stretch(rejectButton.GetComponent<RectTransform>(), new Vector2(0.80f, 0.08f), new Vector2(0.94f, 0.20f), Vector2.zero, Vector2.zero);
            rejectButton.onClick.AddListener(() =>
            {
                bargainingSystem?.RejectDeal();
                ShowScreen(PrototypeScreen.Shop);
            });

            return screen;
        }

        private void RefreshUI()
        {
            if (bargainingSystem == null || playerStats == null || inventoryManager == null)
            {
                return;
            }

            RebuildInventoryButtons();
            RebuildNpcButtons();
            UpdateHud();
            UpdateInventoryTexts();
            UpdateShopTexts();
            UpdateBargainTexts();
            
            if (!isHidden)
                ShowScreen(currentScreen);
        }

        private void RebuildInventoryButtons()
        {
            if (inventoryButtons.Count > 0)
            {
                foreach (Button button in inventoryButtons)
                {
                    if (button != null)
                    {
                        Destroy(button.gameObject);
                    }
                }

                inventoryButtons.Clear();
            }

            Transform parent = inventoryListText.transform.parent.Find("InventoryButtons");
            if (parent == null || bargainingSystem.EconomyConfig == null)
            {
                return;
            }

            foreach (BargainingItemEconomyEntry entry in bargainingSystem.EconomyConfig.AgriculturalItems)
            {
                if (entry == null || entry.item == null)
                {
                    continue;
                }

                ItemData capturedItem = entry.item;
                int count = bargainingSystem.GetInventoryCount(capturedItem);
                Button itemButton = CreateButton($"Item_{capturedItem.itemID}", parent, $"{capturedItem.itemName} x{count}");
                itemButton.interactable = count > 0;
                itemButton.onClick.AddListener(() => bargainingSystem.SelectInventoryItem(capturedItem));
                inventoryButtons.Add(itemButton);
            }
        }

        private void RebuildNpcButtons()
        {
            if (npcCards.Count > 0)
            {
                foreach (GameObject card in npcCards)
                {
                    if (card != null)
                    {
                        Destroy(card);
                    }
                }

                npcCards.Clear();
            }

            Transform parent = shopScreen.transform.Find("NpcCards");
            if (parent == null || bargainingSystem.EconomyConfig == null)
            {
                return;
            }

            foreach (BargainingNpcProfile profile in bargainingSystem.EconomyConfig.NpcProfiles)
            {
                if (profile == null)
                {
                    continue;
                }

                GameObject npcCard = CreateNpcCard(parent, profile);
                npcCards.Add(npcCard);
            }
        }

        private void UpdateHud()
        {
            hudText.text =
                $"Tiền: {playerStats.CurrentMoney:N0} VNĐ    " +
                $"Thể lực: {playerStats.CurrentStamina:0}/{playerStats.MaxStamina:0}    " +
                $"Stamina / lượt mặc cả: {bargainingSystem.EconomyConfig.StaminaCostPerNegotiation}";
        }

        private void UpdateInventoryTexts()
        {
            StringBuilder listBuilder = new StringBuilder();
            listBuilder.AppendLine("Hàng đang có trên ghe");
            listBuilder.AppendLine("----------------------------");

            if (inventoryManager.Inventory.Count == 0)
            {
                listBuilder.AppendLine("Kho đang trống.");
            }
            else
            {
                foreach (KeyValuePair<ItemData, int> itemEntry in inventoryManager.Inventory)
                {
                    listBuilder.AppendLine($"{itemEntry.Key.itemName} x{itemEntry.Value}  |  Giá gốc {itemEntry.Key.basePrice:N0} VNĐ");
                }
            }

            inventoryListText.text = listBuilder.ToString();

            if (bargainingSystem.SelectedItem == null)
            {
                inventoryDetailText.text = "Chọn một mặt hàng ở danh sách bên trái để xem chi tiết và mang sang shop.";
                openShopButton.interactable = false;
                return;
            }

            int count = bargainingSystem.GetInventoryCount(bargainingSystem.SelectedItem);
            inventoryDetailText.text =
                $"Mặt hàng đã chọn: {bargainingSystem.SelectedItem.itemName}\n" +
                $"Số lượng còn lại: {count}\n" +
                $"Khối lượng / món: {bargainingSystem.SelectedItem.weight:0.##} kg\n" +
                $"Giá gốc: {bargainingSystem.SelectedItem.basePrice:N0} VNĐ\n\n" +
                "Nhấn 'Mang Ra Chợ' để chọn người mua.";
            openShopButton.interactable = count > 0;
        }

        private void UpdateShopTexts()
        {
            if (bargainingSystem.SelectedItem == null)
            {
                shopDetailText.text = "Bạn chưa chọn hàng từ Inventory Screen.";
                return;
            }

            BargainingItemEconomyEntry itemEntry = bargainingSystem.EconomyConfig.FindItemEntry(bargainingSystem.SelectedItem);
            if (itemEntry == null)
            {
                shopDetailText.text = $"Chưa có dữ liệu giá cho {bargainingSystem.SelectedItem.itemName}.";
                return;
            }

            shopDetailText.text =
                $"Mặt hàng: {bargainingSystem.SelectedItem.itemName}\n" +
                $"Giá gốc: {bargainingSystem.SelectedItem.basePrice:N0} VNĐ\n" +
                $"Biến động thị trường: {itemEntry.minPriceVariation:N0} đến +{itemEntry.maxPriceVariation:N0} VNĐ\n" +
                $"Số lượng còn lại: {bargainingSystem.GetInventoryCount(bargainingSystem.SelectedItem)}\n\n" +
                "Chọn Merchant hoặc Villager để bước vào màn mặc cả.";
        }

        private void UpdateBargainTexts()
        {
            if (!bargainingSystem.HasActiveSession)
            {
                bargainNpcNameText.text = "Chưa có NPC";
                bargainNpcAvatar.sprite = null;
                bargainNpcAvatar.color = new Color(1f, 1f, 1f, 0.15f);
                bargainOfferText.text = "Hãy chọn NPC trong Shop Menu.";
                bargainMessageText.text = bargainingSystem.CurrentMessage;
                SetBargainButtonsInteractable(false);
                return;
            }

            bargainNpcNameText.text = bargainingSystem.CurrentNpc.displayName;
            bargainNpcAvatar.sprite = bargainingSystem.CurrentNpc.avatar;
            bargainNpcAvatar.color = Color.white;
            bargainOfferText.text =
                $"Item: {bargainingSystem.SelectedItem.itemName}\n" +
                $"NPC mở giá: {bargainingSystem.NpcOpeningPrice:N0} VNĐ\n" +
                $"Giá bạn đang đòi: {bargainingSystem.CurrentAskPrice:N0} VNĐ\n" +
                $"Mốc NPC chịu tối đa: khoảng {bargainingSystem.NpcMaxAcceptPrice:N0} VNĐ";
            bargainMessageText.text = bargainingSystem.CurrentMessage;
            SetBargainButtonsInteractable(true);
        }

        private void ShowScreen(PrototypeScreen screen)
        {
            if (screen == PrototypeScreen.Shop && bargainingSystem.SelectedItem == null)
            {
                screen = PrototypeScreen.Inventory;
            }

            if (screen == PrototypeScreen.Bargain && !bargainingSystem.HasActiveSession)
            {
                screen = bargainingSystem.SelectedItem != null ? PrototypeScreen.Shop : PrototypeScreen.Inventory;
            }

            currentScreen = screen;
            
            if (isHidden)
            {
                inventoryScreen.SetActive(false);
                shopScreen.SetActive(false);
                bargainScreen.SetActive(false);
                return;
            }

            inventoryScreen.SetActive(screen == PrototypeScreen.Inventory);
            shopScreen.SetActive(screen == PrototypeScreen.Shop);
            bargainScreen.SetActive(screen == PrototypeScreen.Bargain);
        }

        private GameObject CreateNpcCard(Transform parent, BargainingNpcProfile profile)
        {
            GameObject card = CreatePanel($"NpcCard_{profile.npcId}", parent, new Color(0.24f, 0.27f, 0.31f, 0.98f));
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            LayoutElement element = card.AddComponent<LayoutElement>();
            element.preferredWidth = 320f;

            Image avatar = CreateImage("Avatar", card.transform, Color.white);
            LayoutElement avatarLayout = avatar.gameObject.AddComponent<LayoutElement>();
            avatarLayout.preferredHeight = 220f;
            avatar.sprite = profile.avatar;
            avatar.preserveAspect = true;

            Text nameText = CreateText("Name", card.transform, 26, TextAnchor.MiddleCenter);
            nameText.text = profile.displayName;
            nameText.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;

            Text infoText = CreateText("Info", card.transform, 22, TextAnchor.MiddleCenter);
            infoText.text =
                $"Mở giá: x{profile.openingPriceMultiplier:0.00}\n" +
                $"Giá tối đa: x{profile.maxAcceptPriceMultiplier:0.00}";
            infoText.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;

            Button button = CreateButton("StartBargainButton", card.transform, "Bắt Đầu Mặc Cả");
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
            button.onClick.AddListener(() =>
            {
                if (bargainingSystem.StartSession(profile, 1))
                {
                    ShowScreen(PrototypeScreen.Bargain);
                }
            });

            return card;
        }

        private void SetBargainButtonsInteractable(bool isInteractable)
        {
            offerDownButton.interactable = isInteractable;
            offerUpButton.interactable = isInteractable;
            acceptButton.interactable = isInteractable;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
        }

        private GameObject CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel;
            if (panelBgSprite != null)
            {
                panel = new GameObject(name, typeof(RectTransform), typeof(Image));
                panel.transform.SetParent(parent, false);
                Image img = panel.GetComponent<Image>();
                img.sprite = panelBgSprite;
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

        private Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            image.preserveAspect = true;
            return image;
        }

        private Button CreateButton(string name, Transform parent, string label)
        {
            GameObject buttonObject;
            if (buttonSpriteNormal != null)
            {
                buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(parent, false);

                Image background = buttonObject.GetComponent<Image>();
                background.sprite = buttonSpriteNormal;
                background.type = Image.Type.Sliced;
                background.color = Color.white;

                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.SpriteSwap;
                SpriteState state = new SpriteState();
                state.highlightedSprite = buttonSpriteHover != null ? buttonSpriteHover : buttonSpriteNormal;
                state.pressedSprite = buttonSpritePressed != null ? buttonSpritePressed : buttonSpriteNormal;
                button.spriteState = state;
            }
            else
            {
                buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(parent, false);

                Image background = buttonObject.GetComponent<Image>();
                background.color = new Color(0.86f, 0.73f, 0.46f, 1f);

                Button button = buttonObject.GetComponent<Button>();
                button.targetGraphic = background;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(0.95f, 0.82f, 0.58f, 1f);
                colors.pressedColor = new Color(0.72f, 0.58f, 0.33f, 1f);
                colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.9f);
                button.colors = colors;
            }

            Button btnComp = buttonObject.GetComponent<Button>();
            Text text = CreateText("Label", buttonObject.transform, 24, TextAnchor.MiddleCenter);
            text.text = label;
            text.color = buttonSpriteNormal != null ? Color.white : new Color(0.16f, 0.12f, 0.07f, 1f);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return btnComp;
        }

        private void CreateTitle(Transform parent, string title)
        {
            Text titleText = CreateText("Title", parent, 34, TextAnchor.MiddleLeft);
            titleText.text = title;
            Stretch(titleText.rectTransform, new Vector2(0.03f, 0.86f), new Vector2(0.60f, 0.95f), Vector2.zero, Vector2.zero);
        }

        private void CreateSubtitle(Transform parent, string subtitle)
        {
            Text subtitleText = CreateText("Subtitle", parent, 22, TextAnchor.MiddleLeft);
            subtitleText.text = subtitle;
            subtitleText.color = new Color(0.86f, 0.91f, 0.89f, 0.92f);
            Stretch(subtitleText.rectTransform, new Vector2(0.03f, 0.80f), new Vector2(0.95f, 0.87f), Vector2.zero, Vector2.zero);
        }

        private void Stretch(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
