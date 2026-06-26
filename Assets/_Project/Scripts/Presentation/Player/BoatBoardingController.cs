using ChoNoi.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoNoi.Presentation.Player
{
    public class BoatBoardingController : MonoBehaviour
    {
        [SerializeField] private ShorePlayerController playerController;
        [SerializeField] private Transform playerVisualRoot;
        [SerializeField] private Transform boat;
        [SerializeField] private Transform standPoint;
        [SerializeField] private Transform dismountPoint;
        [SerializeField] private BoatController boatController;
        [SerializeField] private PCBoatInput boatInput;
        [SerializeField] private BoatFollowCamera followCamera;
        [SerializeField] private float interactDistance = 3.2f;
        [SerializeField] private PlayerNpcTradeInteractor npcTradeInteractor;

        private bool isBoarded;

        public bool IsBoarded => isBoarded;

        public void Configure(
            ShorePlayerController controller,
            Transform visualRoot,
            Transform boatTarget,
            Transform stand,
            Transform dismount,
            BoatController controllerBoat,
            PCBoatInput inputBoat,
            BoatFollowCamera cameraRig)
        {
            playerController = controller;
            playerVisualRoot = visualRoot;
            boat = boatTarget;
            standPoint = stand;
            dismountPoint = dismount;
            boatController = controllerBoat;
            boatInput = inputBoat;
            followCamera = cameraRig;
            if (npcTradeInteractor == null)
                npcTradeInteractor = GetComponent<PlayerNpcTradeInteractor>();
            SetBoatControl(false);
        }

        private void Start()
        {
            SetBoatControl(false);
            if (followCamera != null)
                followCamera.Configure(transform);
        }

        private void Update()
        {
            if (Keyboard.current?.eKey.wasPressedThisFrame != true)
                return;

            // Ưu tiên tương tác NPC trước (dù đang ở trên ghe hay trên bờ)
            if (npcTradeInteractor != null && npcTradeInteractor.TryHandleInteract())
                return;

            if (isBoarded)
            {
                if (CanDismount())
                    DismountBoat();
            }
            else if (CanBoard())
            {
                BoardBoat();
            }
        }

        public bool CanBoardBoat => CanBoard();
        public bool CanDismountBoat => CanDismount();

        public void SetBoatControlActive(bool active)
        {
            SetBoatControl(active);
        }

        private bool CanBoard()
        {
            if (boat == null)
                return false;

            return Vector3.Distance(transform.position, boat.position) <= interactDistance;
        }

        private bool CanDismount()
        {
            if (dismountPoint == null || boat == null)
                return false;

            // Có thể tăng nhẹ khoảng cách cho phép xuống ghe để dễ thao tác hơn
            return Vector3.Distance(boat.position, dismountPoint.position) <= interactDistance * 2f;
        }

        private void BoardBoat()
        {
            isBoarded = true;
            if (playerController != null)
                playerController.CanMove = false;

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            if (standPoint != null)
            {
                transform.SetParent(boat, true);
                transform.position = standPoint.position;
                transform.rotation = standPoint.rotation;
            }

            if (playerVisualRoot != null)
                playerVisualRoot.gameObject.SetActive(false);

            SetBoatControl(true);
            if (followCamera != null && boat != null)
                followCamera.Configure(boat);
        }

        private void DismountBoat()
        {
            isBoarded = false;
            transform.SetParent(null, true);

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            if (dismountPoint != null)
            {
                transform.position = dismountPoint.position;
                transform.rotation = dismountPoint.rotation;
            }

            if (playerVisualRoot != null)
                playerVisualRoot.gameObject.SetActive(true);

            if (playerController != null)
                playerController.CanMove = true;

            SetBoatControl(false);
            if (followCamera != null)
                followCamera.Configure(transform);

            if (cc != null)
                cc.enabled = true;
        }
        public void ResetToStartingState(Vector3 playerStartPos, Quaternion playerStartRot, Vector3 boatStartPos, Quaternion boatStartRot)
        {
            isBoarded = false;
            transform.SetParent(null, true);

            CharacterController cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            transform.position = playerStartPos;
            transform.rotation = playerStartRot;

            if (playerVisualRoot != null)
                playerVisualRoot.gameObject.SetActive(true);

            if (playerController != null)
                playerController.CanMove = true;

            SetBoatControl(false);

            if (boat != null)
            {
                boat.position = boatStartPos;
                boat.rotation = boatStartRot;
                Rigidbody boatRb = boat.GetComponent<Rigidbody>();
                if (boatRb != null)
                {
                    boatRb.linearVelocity = Vector3.zero;
                    boatRb.angularVelocity = Vector3.zero;
                }
            }

            if (followCamera != null)
            {
                followCamera.Configure(transform);
            }

            if (cc != null)
                cc.enabled = true;
        }

        private void SetBoatControl(bool enabled)
        {
            if (boatController != null)
                boatController.enabled = enabled;
            if (boatInput != null)
                boatInput.enabled = enabled;
        }

        // Commented out OnGUI to avoid overlap with Canvas side prompts
        /*
        private void OnGUI()
        {
            const int width = 450;
            const int height = 46;
            Rect rect = new Rect((Screen.width - width) * 0.5f, Screen.height - 92f, width, height);

            if (isBoarded)
            {
                string msg = CanDismount() ? "E: Roi ghe / Giao tiep" : "E: Giao tiep NPC";
                GUI.Box(rect, $"{msg} | WASD: Lai ghe | Alt + Mouse: Xoay camera");
            }
            else if (CanBoard())
            {
                GUI.Box(rect, "E: Len ghe | WASD: Di chuyen tren bo");
            }
        }
        */
    }
}
