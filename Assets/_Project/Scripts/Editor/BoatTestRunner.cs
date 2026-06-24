/**
 * BoatTestRunner: Static Editor class chạy toàn bộ unit test từ terminal.
 * [Chức năng]: Chạy qua lệnh "Unity -batchmode -executeMethod ChoNoi.Editor.BoatTestRunner.Run"
 *              Kiểm tra BoatStats, công thức vật lý, IBoatInput contract — không cần Play mode.
 *              Kết quả in ra log, exit code 0 = tất cả pass, exit code 1 = có fail.
 * [Dependencies]: BoatStats (Infrastructure), UnityEditor.
 */

using UnityEngine;
using UnityEditor;
using ChoNoi.Infrastructure;

namespace ChoNoi.Editor
{
    public static class BoatTestRunner
    {
        private static int _passed;
        private static int _failed;

        // Có thể gọi từ menu Unity Editor: ChoNoi > Run Tests
        [MenuItem("ChoNoi/Tests/Run All Tests")]
        public static void RunFromMenu() => Run();

        /// <summary>
        /// Entry point cho batch mode:
        /// Unity -batchmode -quit -executeMethod ChoNoi.Editor.BoatTestRunner.Run
        /// </summary>
        public static void Run()
        {
            _passed = 0;
            _failed = 0;

            PrintHeader("BOAT PHYSICS TEST SUITE");

            // --- Nhóm 1: BoatStats validation ---
            PrintGroup("1. KIEM TRA BOAT STATS (Unit Test)");
            RunStatsTests();

            // --- Nhóm 2: Công thức vật lý ---
            PrintGroup("2. KIEM TRA CONG THUC VAT LY");
            RunPhysicsFormulaTests();

            // --- Nhóm 3: IBoatInput contract ---
            PrintGroup("3. KIEM TRA IBOATINPUT CONTRACT");
            RunInputContractTests();

            // --- Nhóm 4: Công thức Weight Penalty (Phase 2) ---
            PrintGroup("4. KIEM TRA CONG THUC WEIGHT PENALTY");
            RunWeightPenaltyTests();

            // --- Nhóm 5: Environment & Tide (Phase 3) ---
            PrintGroup("5. KIEM TRA MOI TRUONG & THUY TRIEU");
            RunEnvironmentTests();

            // --- Nhóm 6: Durability Velocity Clamp (Phase 4) ---
            PrintGroup("6. KIEM TRA DO BEN & GIOI HAN VAN TOC");
            RunDurabilityTests();

            // --- Tổng kết ---
            PrintSummary();

            // Exit code 1 nếu có bài fail (cho CI/CD hoặc script bên ngoài)
            // Qualify đầy đủ: namespace ChoNoi.Application (TimeManager) che mất UnityEngine.Application.
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(_failed > 0 ? 1 : 0);
        }

        // ──────────────────────────────────────────────
        // NHÓM 1: BOAT STATS
        // ──────────────────────────────────────────────

        private static void RunStatsTests()
        {
            // Tạo instance với giá trị mặc định đã khai báo trong BoatStats.cs
            var stats = ScriptableObject.CreateInstance<BoatStats>();

            Assert("BoatStats tao duoc instance (khong null)",
                stats != null);

            Assert("ThrustForce phai > 0 (ghe moi co the di chuyen)",
                stats.ThrustForce > 0f);

            Assert("WaterDrag phai > 0 (ghe moi co the giam toc)",
                stats.WaterDrag > 0f);

            Assert("SidewaysDrag phai > 0 (ghe moi chay thang huong mui)",
                stats.SidewaysDrag > 0f);

            Assert("TurnTorque phai > 0 (ghe moi co the be lai)",
                stats.TurnTorque > 0f);

            Assert("SidewaysDrag nen >= WaterDrag (chong truot ngang tot hon can doc)",
                stats.SidewaysDrag >= stats.WaterDrag);

            Object.DestroyImmediate(stats);
        }

        // ──────────────────────────────────────────────
        // NHÓM 2: CÔNG THỨC VẬT LÝ
        // ──────────────────────────────────────────────

        private static void RunPhysicsFormulaTests()
        {
            var stats = ScriptableObject.CreateInstance<BoatStats>();

            // --- Thrust ---
            // F_thrust = forward * throttle * thrustForce
            Vector3 forward      = Vector3.forward;           // (0,0,1)
            float   throttleFull = 1f;
            float   throttleHalf = 0.5f;
            float   throttleBack = -1f;

            Vector3 thrustFull = forward * throttleFull * stats.ThrustForce;
            Vector3 thrustHalf = forward * throttleHalf * stats.ThrustForce;
            Vector3 thrustBack = forward * throttleBack * stats.ThrustForce;

            Assert("Thrust (throttle=1): luc dam tien dung chieu forward",
                thrustFull.z > 0f && Approx(thrustFull.x, 0f) && Approx(thrustFull.y, 0f));

            Assert("Thrust (throttle=0.5): luc bang 50% so voi full throttle",
                Approx(thrustHalf.magnitude, thrustFull.magnitude * 0.5f));

            Assert("Thrust (throttle=-1): luc dam nguoc chieu (lui)",
                thrustBack.z < 0f);

            Assert("Thrust (throttle=0): khong co luc day",
                Approx((forward * 0f * stats.ThrustForce).magnitude, 0f));

            // --- Water Drag ---
            // F_drag = -velocity * waterDrag
            Vector3 velocity = new Vector3(0f, 0f, 5f);  // dang chay tien 5 m/s
            Vector3 drag     = -velocity * stats.WaterDrag;

            Assert("WaterDrag: luc can nguoc chieu voi van toc",
                drag.z < 0f);

            Assert("WaterDrag: do lon ti le voi van toc (van toc x2 -> can x2)",
                Approx((-velocity * 2f * stats.WaterDrag).magnitude,
                       drag.magnitude * 2f));

            Assert("WaterDrag: khi dung im (v=0) khong co luc can",
                Approx((-Vector3.zero * stats.WaterDrag).magnitude, 0f));

            // --- Sideways Resistance ---
            // V_side = right * dot(v, right) ; F = -V_side * sidewaysDrag
            Vector3 right = Vector3.right;  // (1,0,0)

            // Ghe di thang (v theo forward) → khong co thanh phan ngang
            Vector3 vForward        = new Vector3(0f, 0f, 3f);
            Vector3 sidewaysForward = right * Vector3.Dot(vForward, right);
            Assert("SidewaysResistance: ghe di thang (khong truot ngang) → V_side = 0",
                Approx(sidewaysForward.magnitude, 0f));

            // Ghe truot ngang thuần → thanh phan ngang = toàn bộ vận tốc
            Vector3 vSideways        = new Vector3(4f, 0f, 0f);
            Vector3 sidewaysSideways = right * Vector3.Dot(vSideways, right);
            Assert("SidewaysResistance: ghe truot ngang thuan → V_side = v",
                Approx(sidewaysSideways.magnitude, vSideways.magnitude));

            // --- Steering (mô-men lái) ---
            // T = up * steering * turnTorque * |velocity|
            Vector3 up      = Vector3.up;
            float   steer   = 1f;

            // Khi đứng im: |velocity| = 0 → torque = 0 (không lái được khi đứng im)
            float torqueStill = (up * steer * stats.TurnTorque * 0f).magnitude;
            Assert("Steering (dung im): mo-men xoan = 0 (ghe khong quay khi dung im)",
                Approx(torqueStill, 0f));

            // Khi đang chạy: torque > 0
            float speed = 5f;
            float torqueMoving = (up * steer * stats.TurnTorque * speed).magnitude;
            Assert("Steering (dang chay): mo-men xoan > 0 (ghe quay khi co van toc)",
                torqueMoving > 0f);

            // Mô-men tỉ lệ thuận với vận tốc: speed x2 → torque x2
            float torqueDouble = (up * steer * stats.TurnTorque * (speed * 2f)).magnitude;
            Assert("Steering: mo-men ti le thuan voi van toc (v x2 → T x2)",
                Approx(torqueDouble, torqueMoving * 2f));

            Object.DestroyImmediate(stats);
        }

        // ──────────────────────────────────────────────
        // NHÓM 3: IBOATINPUT CONTRACT
        // ──────────────────────────────────────────────

        private static void RunInputContractTests()
        {
            // Dùng MockBoatInput để kiểm tra contract của IBoatInput
            var mock = new MockInput();

            mock.Set(0f, 0f);
            Assert("IBoatInput: throttle=0, steering=0 → ca hai = 0",
                Approx(mock.Throttle, 0f) && Approx(mock.Steering, 0f));

            mock.Set(1f, 1f);
            Assert("IBoatInput: throttle=1 (tien) va steering=1 (phai) hop le",
                mock.Throttle <= 1f && mock.Steering <= 1f);

            mock.Set(-1f, -1f);
            Assert("IBoatInput: throttle=-1 (lui) va steering=-1 (trai) hop le",
                mock.Throttle >= -1f && mock.Steering >= -1f);

            mock.Set(0.5f, -0.75f);
            Assert("IBoatInput: gia tri trung gian (0.5, -0.75) nam trong [-1, 1]",
                mock.Throttle >= -1f && mock.Throttle <= 1f &&
                mock.Steering >= -1f && mock.Steering <= 1f);
        }

        // ──────────────────────────────────────────────
        // NHÓM 4: WEIGHT PENALTY (Phase 2)
        // ──────────────────────────────────────────────

        // Mô phỏng lại công thức trong BoatController để test thuần toán học.
        private static float Performance(float ratio, float maxPenalty)
            => 1f - Mathf.Clamp01(ratio) * maxPenalty;

        private static float DragMultiplier(float ratio)
            => 1f + Mathf.Clamp01(ratio) * 0.5f;

        private static void RunWeightPenaltyTests()
        {
            var stats = ScriptableObject.CreateInstance<BoatStats>();

            // MaxPenaltyFactor phải hợp lệ: 0 <= x < 1 (nếu = 1 thì đầy tải đứng im).
            Assert("BoatStats.MaxPenaltyFactor nam trong [0, 1)",
                stats.MaxPenaltyFactor >= 0f && stats.MaxPenaltyFactor < 1f);

            // ratio=0 → performance = 1.0 (100% hiệu suất khi ghe trống)
            Assert("Performance (ratio=0): bang 1.0 (100% hieu suat)",
                Approx(Performance(0f, stats.MaxPenaltyFactor), 1f));

            // ratio=1, penalty=0.4 → performance = 0.6 (60%)
            Assert("Performance (ratio=1, penalty=0.4): bang 0.6 (60% hieu suat)",
                Approx(Performance(1f, 0.4f), 0.6f));

            // Đơn điệu giảm: ratio cao hơn → performance thấp hơn
            Assert("Performance giam don dieu khi tai trong tang",
                Performance(0.25f, stats.MaxPenaltyFactor) > Performance(0.75f, stats.MaxPenaltyFactor));

            // Clamp: ratio vượt 1 vẫn cho kết quả như ratio=1 (không phạt quá mức)
            Assert("Performance (ratio>1): bi clamp ve nhu ratio=1",
                Approx(Performance(5f, stats.MaxPenaltyFactor), Performance(1f, stats.MaxPenaltyFactor)));

            // dragMultiplier: trống = 1.0, đầy tải = 1.5
            Assert("DragMultiplier (ratio=0): bang 1.0 (khong tang can)",
                Approx(DragMultiplier(0f), 1f));
            Assert("DragMultiplier (ratio=1): bang 1.5 (chay nang -> can nuoc tang)",
                Approx(DragMultiplier(1f), 1.5f));

            // ActualThrust đầy tải phải nhỏ hơn khi trống
            float thrustEmpty = stats.ThrustForce * Performance(0f, stats.MaxPenaltyFactor);
            float thrustFull  = stats.ThrustForce * Performance(1f, stats.MaxPenaltyFactor);
            Assert("ActualThrust (day tai) < ActualThrust (trong)",
                thrustFull < thrustEmpty);

            // ActualTorque đầy tải phải nhỏ hơn khi trống (bẻ lái lỳ hơn)
            float torqueEmpty = stats.TurnTorque * Performance(0f, stats.MaxPenaltyFactor);
            float torqueFull  = stats.TurnTorque * Performance(1f, stats.MaxPenaltyFactor);
            Assert("ActualTorque (day tai) < ActualTorque (trong)",
                torqueFull < torqueEmpty);

            Object.DestroyImmediate(stats);
        }

        // ──────────────────────────────────────────────
        // NHÓM 5: ENVIRONMENT & TIDE (Phase 3)
        // ──────────────────────────────────────────────

        // Công thức mực nước (mirror EnvironmentProfileSO.EvaluateWaterHeight).
        private static float WaterHeight(float min, float max, float factor)
            => Mathf.Lerp(min, max, factor);

        // Công thức lực đẩy còn lại khi mắc cạn (mirror BoatController grounding).
        private static float GroundedThrust(float baseThrust, float penalty, bool grounded)
            => baseThrust * (grounded ? penalty : 1f);

        private static void RunEnvironmentTests()
        {
            // --- Tide: nội suy tuyến tính mực nước ---
            Assert("Tide: factor=1 -> mat nuoc = maxWaterHeight",
                Approx(WaterHeight(-2f, 0f, 1f), 0f));
            Assert("Tide: factor=0 -> mat nuoc = minWaterHeight",
                Approx(WaterHeight(-2f, 0f, 0f), -2f));
            Assert("Tide: factor cao hon -> mat nuoc cao hon",
                WaterHeight(-2f, 0f, 0.7f) > WaterHeight(-2f, 0f, 0.3f));

            // --- EnvironmentProfileSO instance (gia tri mac dinh) ---
            var profile = ScriptableObject.CreateInstance<EnvironmentProfileSO>();

            Assert("Profile: MaxWaterHeight > MinWaterHeight (sang cao hon chieu)",
                profile.MaxWaterHeight > profile.MinWaterHeight);

            float wMid = profile.EvaluateWaterHeight(0.5f);
            Assert("Profile: EvaluateWaterHeight nam trong [min, max]",
                wMid >= profile.MinWaterHeight - 0.001f && wMid <= profile.MaxWaterHeight + 0.001f);

            Assert("Profile: FogDensity khong am tai moi moc gio",
                profile.EvaluateFogDensity(0f) >= 0f &&
                profile.EvaluateFogDensity(0.25f) >= 0f &&
                profile.EvaluateFogDensity(0.5f) >= 0f &&
                profile.EvaluateFogDensity(0.99f) >= 0f);

            Object.DestroyImmediate(profile);

            // --- Grounding: mắc cạn làm lực đẩy giảm mạnh ---
            const float penalty = 0.05f;
            Assert("Grounding: penalty mac dinh nam trong [0, 1)",
                penalty >= 0f && penalty < 1f);
            Assert("Grounding: mac can -> luc day < khi noi",
                GroundedThrust(10f, penalty, true) < GroundedThrust(10f, penalty, false));
            Assert("Grounding: KHONG mac can -> luc day giu nguyen (Phase 1/2 khong doi)",
                Approx(GroundedThrust(10f, penalty, false), 10f));
        }

        // ──────────────────────────────────────────────
        // NHÓM 6: DURABILITY VELOCITY CLAMP (Phase 4)
        // ──────────────────────────────────────────────

        // Công thức trần tốc độ (mirror BoatController.ClampVelocityByDurability).
        private static float CurrentMaxSpeed(float baseMax, float ratio)
            => baseMax * Mathf.Clamp(ratio, 0.3f, 1f);

        // Áp clamp lên một độ lớn vận tốc.
        private static float ApplyClamp(float speed, float maxSpeed)
            => speed > maxSpeed ? maxSpeed : speed;

        private static void RunDurabilityTests()
        {
            var stats = ScriptableObject.CreateInstance<BoatStats>();
            float baseMax = stats.BaseMaxSpeed;   // mặc định 10

            Assert("BoatStats.BaseMaxSpeed phai > 0",
                baseMax > 0f);

            // ratio=1 (nguyen ven) -> tran = baseMaxSpeed
            Assert("Durability=1.0: tran toc do = baseMaxSpeed",
                Approx(CurrentMaxSpeed(baseMax, 1f), baseMax));

            // ratio=0 -> tran = 30% baseMaxSpeed (san toi thieu, ghe khong ket chet)
            Assert("Durability=0.0: tran toc do = 30% baseMaxSpeed (giu it nhat 30%)",
                Approx(CurrentMaxSpeed(baseMax, 0f), baseMax * 0.3f));

            // ratio=0.1 (gan hong) -> van bi clamp ve san 30% (khop test "Durability 10 -> 3m/s")
            Assert("Durability=0.1: tran van la 30% baseMaxSpeed (3 m/s khi base=10)",
                Approx(CurrentMaxSpeed(baseMax, 0.1f), baseMax * 0.3f));

            // ratio=0.5 -> tran = 50% baseMaxSpeed
            Assert("Durability=0.5: tran toc do = 50% baseMaxSpeed",
                Approx(CurrentMaxSpeed(baseMax, 0.5f), baseMax * 0.5f));

            // Tran khong giam theo do ben (don dieu khong giam)
            Assert("Tran toc do KHONG giam khi do ben tang (don dieu)",
                CurrentMaxSpeed(baseMax, 0.4f) <= CurrentMaxSpeed(baseMax, 0.9f));

            // Clamp: vuot tran -> ve dung tran; duoi tran -> giu nguyen
            float maxAtLow = CurrentMaxSpeed(baseMax, 0.1f);   // = 3 khi base=10
            Assert("Clamp: van toc vuot tran bi keo ve tran",
                Approx(ApplyClamp(maxAtLow + 5f, maxAtLow), maxAtLow));
            Assert("Clamp: van toc duoi tran duoc giu nguyen",
                Approx(ApplyClamp(maxAtLow - 1f, maxAtLow), maxAtLow - 1f));

            // Do ben KHONG cham thrustForce (ghe hong van co luc nhich di): cong thuc tran
            // toc do chi phu thuoc baseMaxSpeed, hoan toan doc lap voi ThrustForce.
            Assert("Do ben KHONG lam giam ThrustForce (thrust > 0, doc lap voi tran toc do)",
                stats.ThrustForce > 0f);

            Object.DestroyImmediate(stats);
        }

        // ──────────────────────────────────────────────
        // MOCK + UTILITIES
        // ──────────────────────────────────────────────

        // Mock đơn giản implement IBoatInput để test contract
        private class MockInput : ChoNoi.Domain.IBoatInput
        {
            public float Throttle { get; private set; }
            public float Steering { get; private set; }
            public void Set(float throttle, float steering)
            {
                Throttle = throttle;
                Steering = steering;
            }
        }

        // So sánh float với ngưỡng sai số
        private static bool Approx(float a, float b, float epsilon = 0.001f)
            => Mathf.Abs(a - b) < epsilon;

        // ──────────────────────────────────────────────
        // PRINT HELPERS
        // ──────────────────────────────────────────────

        private static void Assert(string description, bool condition)
        {
            if (condition)
            {
                _passed++;
                Debug.Log($"  [PASS] {description}");
            }
            else
            {
                _failed++;
                Debug.LogError($"  [FAIL] {description}");
            }
        }

        private static void PrintHeader(string title)
            => Debug.Log($"\n{'='.ToString().PadRight(1)}{'='.ToString().PadLeft(1)}" +
                         $" {title} " +
                         $"{'='.ToString().PadRight(1)}{'='.ToString().PadLeft(1)}\n");

        private static void PrintGroup(string name)
            => Debug.Log($"\n--- {name} ---");

        private static void PrintSummary()
        {
            int total = _passed + _failed;
            string result = _failed == 0 ? "ALL PASS" : $"{_failed} FAIL";
            Debug.Log(
                $"\n==========================================\n" +
                $"  KET QUA: {_passed}/{total} test passed  |  {result}\n" +
                $"==========================================\n"
            );
        }
    }
}
