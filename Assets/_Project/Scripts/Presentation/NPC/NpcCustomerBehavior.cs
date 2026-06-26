using UnityEngine;
using ChoNoiMienTay.Presentation;
using ChoNoi.Presentation.Player;

namespace ChoNoi.Presentation.NPC
{
    public class NpcCustomerBehavior : MonoBehaviour
    {
        private enum BehaviorState
        {
            Wandering,
            Approaching,
            WaitingForPlayer,
            Trading,
            Returning
        }

        [SerializeField] private float approachSpeed = 2f;
        [SerializeField] private float cooldownTime = 15f;
        [SerializeField] private float minPlayerDistanceToTrigger = 120f;

        private NpcTradeTarget npcTarget;
        private SimpleNpcWander wander;
        private Transform moveTarget;
        private AmbientBob bobComponent;

        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private BehaviorState state = BehaviorState.Wandering;
        private float cooldownTimer = 0f;
        private bool didTriggerTrade = false;

        private void Start()
        {
            npcTarget = GetComponent<NpcTradeTarget>();
            wander = GetComponent<SimpleNpcWander>();

            // Determine if this NPC is on a boat and we should move the whole boat
            if (transform.parent != null && (transform.parent.name.Contains("Boat") || transform.parent.name.Contains("Ghe") || transform.parent.name.Contains("Tau")))
            {
                moveTarget = transform.parent;
            }
            else
            {
                moveTarget = transform;
            }

            bobComponent = moveTarget.GetComponent<AmbientBob>();
            originalLocalPosition = moveTarget.localPosition;
            originalLocalRotation = moveTarget.localRotation;

            // Adjust interaction radius based on entity type to ensure E-interaction works when stopped
            if (npcTarget != null)
            {
                bool isBoat = moveTarget != transform;
                float desiredRadius = isBoat ? 4.5f : 2.5f;
                npcTarget.Configure(npcTarget.NpcDisplayName, desiredRadius);
            }
        }

        private void Update()
        {
            // Do not update when game is paused (timeScale = 0)
            if (Mathf.Approximately(Time.timeScale, 0f))
                return;

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            GameObject playerBoat = GameObject.Find("PlayerBoat");
            if (playerBoat == null) return;

            var interactor = Object.FindAnyObjectByType<PlayerNpcTradeInteractor>();
            var bambooPoleManager = Object.FindAnyObjectByType<BambooPoleManager>();

            switch (state)
            {
                case BehaviorState.Wandering:
                    // Check if we can start approaching to buy
                    if (NpcTradeCoordinator.ActiveBuyer == null && !NpcTradeCoordinator.IsAnyNpcInteracting())
                    {
                        if (cooldownTimer <= 0f && npcTarget != null && !npcTarget.HasTraded)
                        {
                            // Player must have items displayed on the bamboo pole
                            if (bambooPoleManager != null && bambooPoleManager.DisplayedItems.Count > 0)
                            {
                                float distToPlayer = Vector3.Distance(moveTarget.position, playerBoat.transform.position);
                                if (distToPlayer <= minPlayerDistanceToTrigger)
                                {
                                    // Set as active buyer
                                    NpcTradeCoordinator.ActiveBuyer = npcTarget;
                                    state = BehaviorState.Approaching;
                                    didTriggerTrade = false;

                                    if (wander != null) wander.SetPaused(true);
                                    if (bobComponent != null) bobComponent.enabled = false;
                                }
                            }
                        }
                    }
                    break;

                case BehaviorState.Approaching:
                    if (NpcTradeCoordinator.ActiveBuyer != npcTarget)
                    {
                        // Someone else took over or reset
                        state = BehaviorState.Returning;
                        break;
                    }

                    // Move moveTarget towards player boat
                    Vector3 playerPos = playerBoat.transform.position;
                    // Keep the same Y to avoid flying or drowning
                    playerPos.y = moveTarget.position.y;

                    float dist = Vector3.Distance(moveTarget.position, playerPos);
                    bool isBoat = moveTarget != transform;
                    float targetDist = isBoat ? 3.0f : 1.8f;

                    if (dist > targetDist)
                    {
                        Vector3 dir = (playerPos - moveTarget.position).normalized;
                        moveTarget.position += dir * approachSpeed * Time.deltaTime;
                        
                        // Rotate towards player boat smoothly
                        if (dir != Vector3.zero)
                        {
                            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                            moveTarget.rotation = Quaternion.Slerp(moveTarget.rotation, targetRot, 5f * Time.deltaTime);
                        }
                    }
                    else
                    {
                        // Arrived! Transition to WaitingForPlayer instead of auto-triggering trade
                        state = BehaviorState.WaitingForPlayer;
                    }
                    break;

                case BehaviorState.WaitingForPlayer:
                    if (NpcTradeCoordinator.ActiveBuyer != npcTarget)
                    {
                        state = BehaviorState.Returning;
                        break;
                    }

                    // Rotate towards player boat smoothly while waiting
                    Vector3 pPos = playerBoat.transform.position;
                    pPos.y = moveTarget.position.y;
                    Vector3 lookDir = (pPos - moveTarget.position).normalized;
                    if (lookDir != Vector3.zero)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                        moveTarget.rotation = Quaternion.Slerp(moveTarget.rotation, targetRot, 5f * Time.deltaTime);
                    }

                    // Check if player starts trading with this NPC (interactor has opened trade with us)
                    if (interactor != null && interactor.ActiveTradeTarget == npcTarget)
                    {
                        state = BehaviorState.Trading;
                        didTriggerTrade = true;
                    }
                    else
                    {
                        // If player sails/walks too far away, transition back to Returning
                        float currentDist = Vector3.Distance(moveTarget.position, pPos);
                        float maxWaitDist = npcTarget != null ? npcTarget.InteractionRadius * 1.2f : 6.0f;
                        if (currentDist > maxWaitDist)
                        {
                            if (NpcTradeCoordinator.ActiveBuyer == npcTarget)
                            {
                                NpcTradeCoordinator.ActiveBuyer = null;
                            }
                            state = BehaviorState.Returning;
                        }
                    }
                    break;

                case BehaviorState.Trading:
                    // Check if dialog is closed
                    bool isDialogueOpen = interactor != null && interactor.ActiveTradeTarget == npcTarget;
                    if (!isDialogueOpen)
                    {
                        // Dialogue has finished! NPC goes back
                        state = BehaviorState.Returning;
                    }
                    break;

                case BehaviorState.Returning:
                    // Move moveTarget back to its original position
                    Vector3 targetLocalPos = originalLocalPosition;
                    float localDist = Vector3.Distance(moveTarget.localPosition, targetLocalPos);

                    if (localDist > 0.1f)
                    {
                        Vector3 dir = (originalLocalPosition - moveTarget.localPosition).normalized;
                        moveTarget.localPosition += dir * approachSpeed * Time.deltaTime;

                        // Rotate back to original rotation
                        moveTarget.localRotation = Quaternion.Slerp(moveTarget.localRotation, originalLocalRotation, 5f * Time.deltaTime);
                    }
                    else
                    {
                        // Fully returned
                        moveTarget.localPosition = originalLocalPosition;
                        moveTarget.localRotation = originalLocalRotation;

                        if (wander != null) wander.SetPaused(false);
                        if (bobComponent != null)
                        {
                            bobComponent.StartPosition = originalLocalPosition;
                            bobComponent.enabled = true;
                        }

                        // Release active buyer role
                        if (NpcTradeCoordinator.ActiveBuyer == npcTarget)
                        {
                            NpcTradeCoordinator.ActiveBuyer = null;
                        }

                        cooldownTimer = cooldownTime;
                        state = BehaviorState.Wandering;
                    }
                    break;
            }
        }
    }
}
