using UnityEngine;

namespace ChoNoi.Presentation.NPC
{
    public static class NpcTradeCoordinator
    {
        private static NpcTradeTarget activeBuyer;

        public static NpcTradeTarget ActiveBuyer
        {
            get => activeBuyer;
            set
            {
                if (activeBuyer != value)
                {
                    activeBuyer = value;
                    Debug.Log($"[NpcTradeCoordinator] ActiveBuyer set to: {(activeBuyer != null ? activeBuyer.NpcDisplayName : "None")}");
                }
            }
        }

        public static bool IsAnyNpcInteracting()
        {
            var fullUI = Object.FindAnyObjectByType<ChoNoi.UI.FullSimulatorUI>();
            if (fullUI != null && fullUI.IsDialogueOpen)
            {
                return true;
            }

            var interactor = Object.FindAnyObjectByType<ChoNoi.Presentation.Player.PlayerNpcTradeInteractor>();
            if (interactor != null && interactor.ActiveTradeTarget != null)
            {
                return true;
            }

            return false;
        }
    }
}
