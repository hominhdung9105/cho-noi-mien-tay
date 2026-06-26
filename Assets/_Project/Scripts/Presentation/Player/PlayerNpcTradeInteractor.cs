using ChoNoi.Presentation.NPC;
using ChoNoiMienTay.UI;
using ChoNoi.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoNoi.Presentation.Player
{
    public class PlayerNpcTradeInteractor : MonoBehaviour
    {
        [SerializeField] private RiverMarketHUD hud;
        [SerializeField] private ShorePlayerController playerController;
        [SerializeField] private BargainingPrototypeUI bargainingUI;

        private NpcTradeTarget currentTarget;
        private NpcTradeTarget activeTradeTarget;

        public bool HasTradeTargetInRange => currentTarget != null;
        public NpcTradeTarget CurrentTarget => currentTarget;

        private void Start()
        {
            if (hud == null)
                hud = FindAnyObjectByType<RiverMarketHUD>();

            if (playerController == null)
                playerController = GetComponent<ShorePlayerController>();

            if (bargainingUI == null)
                bargainingUI = FindAnyObjectByType<BargainingPrototypeUI>();

            // Dynamically add NpcCustomerBehavior to all NPC trade targets in the scene (excluding Upgrade targets)
            NpcTradeTarget[] targets = FindObjectsByType<NpcTradeTarget>(FindObjectsSortMode.None);
            foreach (var target in targets)
            {
                if (target.TargetType != InteractionTargetType.Upgrade)
                {
                    if (target.GetComponent<NpcCustomerBehavior>() == null)
                    {
                        target.gameObject.AddComponent<NpcCustomerBehavior>();
                    }
                }
            }
        }

        private bool CheckIsAnyUIOpen(FullSimulatorUI fullUI)
        {
            if (fullUI != null)
            {
                if (fullUI.IsDialogueOpen || fullUI.IsMarketingOpen || fullUI.IsPauseOpen || 
                    fullUI.IsSettingsOpen || fullUI.IsTutorialOpen || fullUI.IsYardOpen || 
                    fullUI.IsTradeQtyOpen)
                {
                    return true;
                }
            }
            if (hud != null && (hud.IsUpgradeOpen || hud.IsNpcTradeOpen))
            {
                return true;
            }
            if (bargainingUI != null && !bargainingUI.IsHidden)
            {
                return true;
            }
            return false;
        }

        private void Update()
        {
            var fullUI = FindAnyObjectByType<FullSimulatorUI>();
            bool isAnyUIOpen = CheckIsAnyUIOpen(fullUI);

            if (playerController != null && !playerController.CanMove && !isAnyUIOpen)
            {
                // Safety release if movement was somehow locked but no UI is active
                playerController.CanMove = true;
            }

            if (playerController != null && !playerController.CanMove && isAnyUIOpen)
            {
                // If UI is open, check if we need to close it due to escape key
                if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                {
                    CloseTrade();
                }
                return;
            }

            currentTarget = FindClosestTarget();

            if (isAnyUIOpen && currentTarget == null)
                CloseTrade();
        }

        public bool TryHandleInteract()
        {
            if (currentTarget == null)
                return false;

            var fullUI = FindAnyObjectByType<FullSimulatorUI>();
            bool isAnyUIOpen = CheckIsAnyUIOpen(fullUI);

            if (isAnyUIOpen)
                CloseTrade();
            else
                OpenTrade(currentTarget);

            return true;
        }

        public NpcTradeTarget ActiveTradeTarget => activeTradeTarget;

        public void OpenTrade(NpcTradeTarget target)
        {
            if (target == null)
                return;

            activeTradeTarget = target;
            SetNpcPaused(activeTradeTarget, true);

            // Freeze player controller movement
            if (playerController != null)
                playerController.CanMove = false;

            // Freeze boat controls if on boat
            var boarding = GetComponent<BoatBoardingController>();
            if (boarding != null && boarding.IsBoarded)
            {
                boarding.SetBoatControlActive(false);
            }

            var fullUI = FindAnyObjectByType<FullSimulatorUI>();

            switch (target.TargetType)
            {
                case InteractionTargetType.Bargain:
                    if (fullUI != null)
                        fullUI.OpenBargainDialogue(target);
                    else if (bargainingUI != null)
                        bargainingUI.ToggleVisibility(true);
                    break;
                case InteractionTargetType.Upgrade:
                    if (fullUI != null)
                        fullUI.OpenUpgradeCampDialogue(target);
                    else if (hud != null)
                        hud.OpenUpgradePanel();
                    break;
                case InteractionTargetType.News:
                case InteractionTargetType.Trade:
                    if (fullUI != null)
                        fullUI.OpenTradeDialogue(target);
                    else if (hud != null)
                        hud.OpenNpcTrade(activeTradeTarget.NpcDisplayName);
                    break;
            }
        }

        public void CloseTrade()
        {
            SetNpcPaused(activeTradeTarget, false);
            activeTradeTarget = null;

            var boarding = GetComponent<BoatBoardingController>();
            bool onBoat = boarding != null && boarding.IsBoarded;

            // Unfreeze player movement only if NOT on the boat
            if (playerController != null)
                playerController.CanMove = !onBoat;

            // Unfreeze boat controls if on the boat
            if (boarding != null && onBoat)
            {
                boarding.SetBoatControlActive(true);
            }

            if (hud != null)
                hud.CloseAllPanels();
                
            if (bargainingUI != null)
                bargainingUI.ToggleVisibility(false);

            var fullUI = FindAnyObjectByType<FullSimulatorUI>();
            if (fullUI != null)
                fullUI.CloseAllDialogueAndPanels();
        }

        private NpcTradeTarget FindClosestTarget()
        {
            NpcTradeTarget[] targets = FindObjectsByType<NpcTradeTarget>(FindObjectsSortMode.None);
            NpcTradeTarget closest = null;
            float closestDistance = float.MaxValue;

            foreach (NpcTradeTarget target in targets)
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance > target.InteractionRadius || distance >= closestDistance)
                    continue;

                closest = target;
                closestDistance = distance;
            }

            return closest;
        }

        // Commented out OnGUI to avoid overlap with Canvas side prompts
        /*
        private void OnGUI()
        {
            if (currentTarget == null)
                return;

            Rect rect = new Rect((Screen.width - 420f) * 0.5f, Screen.height - 146f, 420f, 42f);
            GUI.Box(rect, $"E: Giao dich voi {currentTarget.NpcDisplayName}");
        }
        */

        private static void SetNpcPaused(NpcTradeTarget target, bool paused)
        {
            if (target == null)
                return;

            SimpleNpcWander wander = target.GetComponent<SimpleNpcWander>();
            if (wander != null)
                wander.SetPaused(paused);
        }
    }
}
