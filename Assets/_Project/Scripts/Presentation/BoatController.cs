/**
 * BoatController: MonoBehaviour điều khiển vật lý ghe chính.
 * [Chức năng]: Nhận đầu vào từ IBoatInput, áp dụng 4 lực vật lý lên Rigidbody
 *              trong FixedUpdate: lực đẩy, cản nước, cản ngang, mô-men lái.
 *              Lấy tỷ lệ tải trọng từ IWeightProvider và áp dụng Performance
 *              Multiplier: ghe càng nặng → tăng tốc chậm, bẻ lái lỳ, cản nước tăng.
 *              Phase 3: phát hiện mắc cạn (chạm đáy sông theo riverbedLayer) → giảm
 *              mạnh lực đẩy/lái và thêm ma sát, mô phỏng ghe bị kẹt khi nước hạ.
 *              Phase 4: khóa trần vận tốc theo độ bền (IDurabilityProvider) và cấu hình
 *              Rigidbody (Continuous + freeze rotation X/Z) chống xuyên tường, chống lật.
 * [Dependencies]: IBoatInput, IWeightProvider, IDurabilityProvider (Domain),
 *                 BoatStats (Infrastructure), Rigidbody.
 */

using UnityEngine;
using ChoNoi.Domain;
using ChoNoi.Infrastructure;
using ChoNoiMienTay.Presentation;

namespace ChoNoi.Presentation
{
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour
    {
        [SerializeField] private BoatStats boatStats;

        public BoatStats Stats
        {
            get => boatStats;
            set => boatStats = value;
        }

        [Header("Mắc cạn (Grounding) — Phase 3")]
        // Layer của đáy sông / Terrain. Khi ghe chạm collider thuộc layer này -> mắc cạn.
        [SerializeField] private LayerMask riverbedLayer;
        // Hệ số lực đẩy/lái còn lại khi mắc cạn (0.05 = chỉ còn 5%, ghe gần như đứng yên).
        [SerializeField, Range(0f, 1f)] private float groundedThrustPenalty = 0.05f;
        // Lực cản phụ rất lớn khi mắc cạn để mô phỏng ma sát với đáy sông.
        [SerializeField] private float groundedExtraDrag = 8f;
        [Header("Do Ben & On Dinh")]
        [SerializeField] private float durabilityWearPerSpeed = 0.012f;
        [SerializeField] private float collisionDamageMultiplier = 1.4f;
        [SerializeField] private float maxStableVerticalSpeed = 3.5f;
        [SerializeField] private float maxAngularSpeed = 3.2f;

        private Rigidbody rb;
        private IBoatInput boatInput;
        // Nguồn cấp tỷ lệ tải trọng (tùy chọn). Null → ghe coi như trống, hiệu suất 100%.
        private IWeightProvider weightProvider;
        private IDurabilityProvider durabilityProvider;
        // True khi ghe đang chạm đáy sông (mắc cạn).
        private bool isGrounded;
        private bool wasGrounded;

        public bool IsGrounded => wasGrounded;
        public float EngineThrustMultiplier { get; set; } = 1f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            boatInput = GetComponent<IBoatInput>();
            // Decouple: chỉ cần một component bất kỳ trên ghe implement các interface này.
            weightProvider = GetComponent<IWeightProvider>();
            durabilityProvider = GetComponent<IDurabilityProvider>();

            // Fallback cho scene hiện tại: nếu provider không nằm trên cùng ghe thì lấy manager dùng chung trong scene.
            if (weightProvider == null)
                weightProvider = FindAnyObjectByType<InventoryManager>();

            if (durabilityProvider == null)
                durabilityProvider = FindAnyObjectByType<DurabilityManager>();

            // Chống xuyên tường (tunneling) khi ghe chạy nhanh — bắt buộc theo physics-tuning-rules.
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            // Chỉ cho xoay quanh trục Y → va chạm không làm ghe lật úp / văng lên trời.
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        private void FixedUpdate()
        {
            if (rb == null || boatStats == null)
                return;

            if (boatInput == null)
            {
                boatInput = GetComponent<IBoatInput>();
                if (boatInput == null)
                    return;
            }

            // Cập nhật trạng thái mắc cạn thực tế từ frame vật lý trước
            wasGrounded = isGrounded;
            isGrounded = false;

            // Bước 0: Tính hệ số suy giảm hiệu suất theo tải trọng (1 lần / FixedUpdate).
            // WeightRatio ∈ [0,1]; Performance = 1 - ratio*MaxPenaltyFactor (đầy tải → tối thiểu).
            // dragMul: chở nặng làm lực cản nước tăng nhẹ (tối đa +50% khi đầy tải).
            float weightRatio = 0f;
            if (weightProvider != null)
            {
                try
                {
                    weightRatio = Mathf.Clamp01(weightProvider.GetCurrentWeightRatio());
                }
                catch
                {
                    weightProvider = FindAnyObjectByType<InventoryManager>();
                    weightRatio = weightProvider != null ? Mathf.Clamp01(weightProvider.GetCurrentWeightRatio()) : 0f;
                }
            }
            float basePerformance = 1f - weightRatio * boatStats.MaxPenaltyFactor;
            float dragMultiplier = 1f + weightRatio * 0.5f;

            float throttle = 0f;
            float steering = 0f;
            try
            {
                throttle = boatInput.Throttle;
                steering = boatInput.Steering;
            }
            catch
            {
                boatInput = GetComponent<IBoatInput>();
                if (boatInput == null)
                    return;

                throttle = boatInput.Throttle;
                steering = boatInput.Steering;
            }

            // Tách hiệu suất đẩy và lái để hỗ trợ người chơi thoát mắc cạn khi đi lùi hoặc bẻ lái
            float thrustPerformance = basePerformance;
            float steeringPerformance = basePerformance;

            if (wasGrounded)
            {
                // Nếu đi lùi (lùi ra khỏi vùng cạn), cho phép sử dụng ít nhất 50% công suất động cơ
                if (throttle < 0f)
                    thrustPerformance *= Mathf.Max(groundedThrustPenalty, 0.5f);
                else
                    thrustPerformance *= groundedThrustPenalty;

                // Cho phép bẻ lái với ít nhất 40% công suất để có thể hướng đầu ghe ra vùng nước sâu
                steeringPerformance *= Mathf.Max(groundedThrustPenalty, 0.4f);
            }

            ApplyThrust(throttle, thrustPerformance);
            ApplyWaterDrag(dragMultiplier);
            ApplyRiverCurrent();
            ApplyLateralDrift(throttle, steering, steeringPerformance);
            ApplySidewaysResistance();
            ApplySteering(steering, steeringPerformance);

            // Ma sát đáy sông: lực cản phụ ngược chiều vận tốc khi mắc cạn.
            if (wasGrounded)
                rb.AddForce(-rb.linearVelocity * groundedExtraDrag, ForceMode.Acceleration);

            // Bước 5 (Phase 4) - Khóa trần vận tốc theo độ bền (SAU mọi lực).
            ClampVelocityByDurability();
            ApplyDurabilityWear();
            StabilizePhysics();
        }

        /// <summary>
        /// Giới hạn vận tốc tối đa theo độ bền. Độ bền KHÔNG giảm thrustForce (ghe hỏng
        /// vẫn nhích đi được) mà chỉ khóa trần tốc độ, giữ tối thiểu 30% để ghe không
        /// kẹt chết giữa sông khi độ bền = 0.
        /// </summary>
        private void ClampVelocityByDurability()
        {
            float durabilityRatio = durabilityProvider != null ? durabilityProvider.GetDurabilityRatio() : 1f;
            // currentMaxSpeed = baseMaxSpeed * clamp(ratio, 0.3, 1.0)
            float currentMaxSpeed = boatStats.BaseMaxSpeed * Mathf.Clamp(durabilityRatio, 0.3f, 1f);

            if (rb.linearVelocity.magnitude > currentMaxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * currentMaxSpeed;
        }

        // Phát hiện mắc cạn: chạm liên tục với collider thuộc riverbedLayer.
        private void OnCollisionStay(Collision collision)
        {
            if (IsRiverbed(collision.gameObject.layer))
            {
                // Chỉ coi là mắc cạn nếu pháp tuyến tiếp xúc hướng lên trên (đáy sông nâng ghe)
                foreach (ContactPoint contact in collision.contacts)
                {
                    if (contact.normal.y > 0.5f)
                    {
                        isGrounded = true;
                        break;
                    }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            DurabilityManager durabilityManager = durabilityProvider as DurabilityManager;
            if (durabilityManager == null)
                return;

            float impactStrength = collision.relativeVelocity.magnitude;
            if (impactStrength < 1.5f)
                return;

            durabilityManager.ReduceDurability(impactStrength * collisionDamageMultiplier);
        }

        // Rời khỏi đáy sông (nước dâng lại) -> hết mắc cạn.
        private void OnCollisionExit(Collision collision)
        {
            if (IsRiverbed(collision.gameObject.layer))
                isGrounded = false;
        }

        // Kiểm tra một layer có nằm trong riverbedLayer mask không.
        private bool IsRiverbed(int layer) => (riverbedLayer.value & (1 << layer)) != 0;

        /// <summary>
        /// Bước 1 - Lực đẩy: Áp dụng lực tiến/lùi theo hướng mũi ghe (transform.forward).
        /// ForceMode.Acceleration bỏ qua khối lượng để cảm giác điều khiển đồng nhất.
        /// </summary>
        /// <param name="throttle">Giá trị từ -1 (lùi) đến 1 (tiến).</param>
        /// <param name="performance">Hệ số hiệu suất [0,1] theo tải trọng (1 = đầy lực).</param>
        private void ApplyThrust(float throttle, float performance)
        {
            // F_thrust = forward * throttle * thrustForce * performance * engineThrustMultiplier
            rb.AddForce(transform.forward * throttle * boatStats.ThrustForce * performance * EngineThrustMultiplier, ForceMode.Acceleration);
        }

        /// <summary>
        /// Bước 2 - Lực cản nước: Tạo lực ngược chiều vận tốc tổng để ghe không trôi mãi.
        /// Mô phỏng lực ma sát của nước; tăng nhẹ theo dragMultiplier khi ghe chở nặng.
        /// </summary>
        /// <param name="dragMultiplier">Hệ số nhân lực cản theo tải trọng (≥ 1).</param>
        private void ApplyWaterDrag(float dragMultiplier)
        {
            float throttleMagnitude = boatInput != null ? Mathf.Abs(boatInput.Throttle) : 0f;
            float steeringMagnitude = boatInput != null ? Mathf.Abs(boatInput.Steering) : 0f;
            float inputMagnitude = Mathf.Clamp01(Mathf.Max(throttleMagnitude, steeringMagnitude));
            float coastFactor = Mathf.Lerp(boatStats.CoastDragFactor, boatStats.ActiveDragFactor, inputMagnitude);

            // F_resistance = -velocity * waterDrag * dragMultiplier
            Vector3 resistance = -rb.linearVelocity * boatStats.WaterDrag * dragMultiplier * coastFactor;
            rb.AddForce(resistance, ForceMode.Acceleration);
        }

        /// <summary>
        /// Bước 3 - Lực cản ngang: Triệt tiêu vận tốc trượt sang hai bên (transform.right).
        /// Quan trọng nhất để ghe chạy đúng hướng mũi, không bị drift.
        /// </summary>
        private void ApplySidewaysResistance()
        {
            // Chiếu vận tốc lên trục ngang của ghe để tách phần trượt ngang
            // V_sideways = right * dot(velocity, right)
            Vector3 sidewaysVelocity = transform.right * Vector3.Dot(rb.linearVelocity, transform.right);

            // Áp lực ngược để triệt tiêu phần trượt ngang đó
            rb.AddForce(-sidewaysVelocity * boatStats.SidewaysDrag, ForceMode.Acceleration);
        }

        /// <summary>
        /// Bước 4 - Mô-men lái: Xoay ghe quanh trục Y.
        /// Nhân thêm vận tốc hiện tại để ghe chỉ bẻ lái khi đang chạy — thực tế hơn.
        /// Nhân performance để ghe chở nặng bẻ lái lỳ (trễ) hơn.
        /// </summary>
        /// <param name="steering">Giá trị từ -1 (trái) đến 1 (phải).</param>
        /// <param name="performance">Hệ số hiệu suất [0,1] theo tải trọng (1 = đầy lực).</param>
        private void ApplySteering(float steering, float performance)
        {
            // T_steer = up * steering * turnTorque * |velocity| * performance
            rb.AddTorque(
                transform.up * steering * boatStats.TurnTorque * Mathf.Max(rb.linearVelocity.magnitude, 2.8f) * performance,
                ForceMode.Acceleration
            );
        }

        private void ApplyRiverCurrent()
        {
            rb.AddForce(boatStats.RiverCurrent, ForceMode.Acceleration);
        }

        private void ApplyLateralDrift(float throttle, float steering, float performance)
        {
            float lateralInput = steering;
            if (Mathf.Abs(lateralInput) < 0.01f)
                return;

            rb.AddForce(transform.right * lateralInput * boatStats.LateralThrust * performance, ForceMode.Acceleration);
        }

        private void ApplyDurabilityWear()
        {
            DurabilityManager durabilityManager = durabilityProvider as DurabilityManager;
            if (durabilityManager == null)
                return;

            float speedWear = rb.linearVelocity.magnitude * durabilityWearPerSpeed * Time.fixedDeltaTime;
            if (wasGrounded)
                speedWear *= 1.8f;

            durabilityManager.ReduceDurability(speedWear);
        }

        private void StabilizePhysics()
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = Mathf.Clamp(velocity.y, -maxStableVerticalSpeed, maxStableVerticalSpeed);
            rb.linearVelocity = velocity;

            if (rb.angularVelocity.magnitude > maxAngularSpeed)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * maxAngularSpeed;
            }
        }
    }
}
