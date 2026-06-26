using UnityEngine;
using UnityEngine.InputSystem;

namespace ChoNoi.Presentation
{
    public class BoatFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 10f, -14f);
        [SerializeField] private float followSpeed = 4f;
        [SerializeField] private float lookAheadDistance = 4f;
        [SerializeField] private float mouseSensitivity = 15f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 1.5f;
        [SerializeField] private float minZoomDistance = 5f;
        [SerializeField] private float maxZoomDistance = 35f;
        [SerializeField] private float onFootTargetHeight = 1.4f;

        private float orbitYaw = 180f;
        private float orbitPitch = 35f;
        private float orbitDistance;
        private bool orbitInitialized;
        private float defaultPitch;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            orbitInitialized = false;
            orbitDistance = offset.magnitude;
            UpdateDefaultPitch();
        }

        private void Start()
        {
            orbitDistance = offset.magnitude;
            UpdateDefaultPitch();
        }

        private void UpdateDefaultPitch()
        {
            float planarDistance = new Vector2(offset.x, offset.z).magnitude;
            defaultPitch = Mathf.Atan2(offset.y, Mathf.Max(planarDistance, 0.01f)) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            bool isOnFootTarget = target.GetComponent<ChoNoi.Presentation.Player.ShorePlayerController>() != null;

            // Kiểm tra lăn chuột để Zoom ở mọi chế độ
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                orbitDistance = Mathf.Clamp(orbitDistance - Mathf.Sign(scroll) * zoomSpeed, minZoomDistance, maxZoomDistance);
            }

            bool isFreeLookHeld = isOnFootTarget
                ? (Mouse.current?.rightButton.isPressed ?? false)
                : ((Keyboard.current?.leftAltKey.isPressed ?? false) || (Keyboard.current?.rightAltKey.isPressed ?? false));
            Vector3 desiredPosition;

            if (isOnFootTarget)
            {
                if (!orbitInitialized)
                {
                    Vector3 worldOffset = transform.position - target.position;
                    orbitYaw = Mathf.Atan2(worldOffset.x, worldOffset.z) * Mathf.Rad2Deg;
                    float planarDistance = new Vector2(worldOffset.x, worldOffset.z).magnitude;
                    orbitPitch = Mathf.Atan2(worldOffset.y, Mathf.Max(planarDistance, 0.01f)) * Mathf.Rad2Deg;
                    orbitInitialized = true;
                }

                if (isFreeLookHeld)
                {
                    Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                    float sensitivity = mouseSensitivity * 0.01f;
                    orbitYaw += mouseDelta.x * sensitivity;
                    orbitPitch = Mathf.Clamp(orbitPitch - mouseDelta.y * sensitivity, minPitch, maxPitch);
                }
                else
                {
                    bool pressingMove = false;
                    if (Keyboard.current != null)
                    {
                        pressingMove = Keyboard.current.wKey.isPressed || Keyboard.current.sKey.isPressed || 
                                       Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed ||
                                       Keyboard.current.upArrowKey.isPressed || Keyboard.current.downArrowKey.isPressed ||
                                       Keyboard.current.leftArrowKey.isPressed || Keyboard.current.rightArrowKey.isPressed;
                    }
                    
                    if (pressingMove)
                    {
                        orbitYaw = Mathf.LerpAngle(orbitYaw, target.eulerAngles.y, 3.0f * Time.deltaTime);
                    }
                }

                Quaternion worldRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                Vector3 onFootLookTarget = target.position + Vector3.up * onFootTargetHeight;
                desiredPosition = onFootLookTarget + worldRotation * new Vector3(0f, 0f, -orbitDistance);
                transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSpeed);
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.LookRotation(onFootLookTarget - transform.position, Vector3.up),
                    Time.deltaTime * followSpeed);
                return;
            }

            if (isFreeLookHeld)
            {
                if (!orbitInitialized)
                {
                    // Khởi tạo góc xoay local của camera so với ghe dựa trên vị trí hiện tại trong thế giới thực để tránh bị giật khi bắt đầu bấm Alt
                    Vector3 localOffset = target.InverseTransformPoint(transform.position);
                    orbitYaw = Mathf.Atan2(localOffset.x, localOffset.z) * Mathf.Rad2Deg;
                    float planarDistance = new Vector2(localOffset.x, localOffset.z).magnitude;
                    orbitPitch = Mathf.Atan2(localOffset.y, Mathf.Max(planarDistance, 0.01f)) * Mathf.Rad2Deg;
                    orbitInitialized = true;
                }

                Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                float sensitivity = mouseSensitivity * 0.01f;
                orbitYaw += mouseDelta.x * sensitivity;
                orbitPitch = Mathf.Clamp(orbitPitch - mouseDelta.y * sensitivity, minPitch, maxPitch);

                Quaternion localRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                desiredPosition = target.position + target.rotation * localRotation * new Vector3(0f, 0f, orbitDistance);
            }
            else
            {
                orbitInitialized = false;
                // Quay lại hướng mặc định phía sau ghe nhưng giữ khoảng cách đã zoom
                desiredPosition = target.position + target.TransformDirection(offset.normalized * orbitDistance);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSpeed);

            // Tính toán lookTarget mượt mà không bị giật:
            // - Khi ở chế độ orbit, camera tập trung nhìn vào chiếc ghe (hoặc lệch một chút nếu yaw gần 180).
            // - Để tránh bị giật góc nhìn đột ngột (snap) khi nhấn Alt, ta nội suy lookAheadDistance dựa trên góc lệch yaw so với phía sau ghe (180 độ).
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(orbitYaw, 180f));
            float lookAheadFactor = Mathf.Clamp01(1f - angleDiff / 45f); // Chỉ kích hoạt lookAhead khi yaw lệch không quá 45 độ so với phía sau ghe

            Vector3 lookTarget;
            if (isFreeLookHeld)
            {
                lookTarget = target.position + target.rotation * Quaternion.Euler(0f, orbitYaw - 180f, 0f) * new Vector3(0f, 0f, lookAheadDistance * lookAheadFactor);
            }
            else
            {
                lookTarget = target.position + target.forward * lookAheadDistance;
            }

            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(lookTarget - transform.position, Vector3.up),
                Time.deltaTime * followSpeed);
        }
    }
}
