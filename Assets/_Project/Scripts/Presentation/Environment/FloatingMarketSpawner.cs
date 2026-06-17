/**
 * FloatingMarketSpawner: Rải ghe xuồng NPC làm nền + dựng "Cây Bẹo" treo nông sản mẫu.
 * [Chức năng]: Sinh ngẫu nhiên (seed cố định) một số ghe trong vùng chỉ định trên mặt sông.
 *              Với mỗi ghe: tìm điểm neo "Bow_Anchor" trước mũi (fallback: offset theo hướng mũi),
 *              dựng một Cây Bẹo (prefab hoặc cọc tre primitive) rồi treo 1 nông sản mẫu ngẫu nhiên
 *              (Khóm/Dưa hấu/Bí đao...) lên đỉnh — tái hiện linh hồn chợ nổi miền Tây.
 *              Sinh 1 lần lúc Start, gom dưới 1 container; KHÔNG dùng Update (tối ưu hiệu năng).
 *              KHÔNG tham chiếu UI (đúng dev1-systems-rules).
 * [Dependencies]: Không bắt buộc — prefab gán qua Inspector; có fallback primitive nếu thiếu.
 */

using UnityEngine;

namespace ChoNoi.Presentation.Environment
{
    public class FloatingMarketSpawner : MonoBehaviour
    {
        [Header("Prefab ghe xuồng NPC (nền)")]
        [SerializeField] private GameObject[] boatPrefabs;

        [Header("Cây Bẹo & nông sản mẫu")]
        // Prefab Cây Bẹo (cọc tre). Bỏ trống -> tự dựng cọc tre bằng primitive Cylinder.
        [SerializeField] private GameObject bambooPolePrefab;
        // Các prefab nông sản mẫu (Khóm/Dưa hấu/Bí đao...) — chọn ngẫu nhiên treo lên đỉnh cây.
        [SerializeField] private GameObject[] fruitPrefabs;
        // Tên điểm neo trước mũi ghe để cắm Cây Bẹo.
        [SerializeField] private string bowAnchorName = "Bow_Anchor";
        // Chiều cao Cây Bẹo (primitive fallback) tính từ sàn ghe.
        [SerializeField] private float poleHeight = 2.6f;

        [Header("Vùng rải ghe")]
        [SerializeField] private int boatCount = 8;
        // Kích thước vùng rải (X,Z) quanh tâm GameObject này; Y = mặt nước.
        [SerializeField] private Vector3 areaSize = new Vector3(40f, 0f, 40f);
        [SerializeField] private float waterY = 0f;
        // Khoảng cách tối thiểu giữa các ghe để không chồng lên nhau.
        [SerializeField] private float minSpacing = 5f;
        // Seed cố định -> bố cục ổn định giữa các lần chạy/build.
        [SerializeField] private int randomSeed = 1307;

        [Header("Tự sinh")]
        [SerializeField] private bool spawnOnStart = true;

        private Transform container;

        private void Start()
        {
            if (spawnOnStart)
                Spawn();
        }

        /// <summary>
        /// Rải ghe + Cây Bẹo + nông sản trong vùng. Gọi lại sẽ xoá lứa cũ trước.
        /// </summary>
        public void Spawn()
        {
            ClearSpawned();

            container = new GameObject("SpawnedBoats").transform;
            container.SetParent(transform, false);

            // State random cục bộ -> không ảnh hưởng Random toàn cục của game.
            Random.State previous = Random.state;
            Random.InitState(randomSeed);

            var placed = new System.Collections.Generic.List<Vector3>();
            int attempts = 0;
            int maxAttempts = boatCount * 12;

            while (placed.Count < boatCount && attempts < maxAttempts)
            {
                attempts++;
                Vector3 pos = transform.position + new Vector3(
                    Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                    0f,
                    Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f));
                pos.y = waterY;

                if (!IsFarEnough(pos, placed)) continue;
                placed.Add(pos);

                SpawnBoat(pos, Random.Range(0f, 360f));
            }

            Random.state = previous; // khôi phục Random toàn cục
        }

        private bool IsFarEnough(Vector3 pos, System.Collections.Generic.List<Vector3> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if ((placed[i] - pos).sqrMagnitude < minSpacing * minSpacing)
                    return false;
            }
            return true;
        }

        private void SpawnBoat(Vector3 position, float yaw)
        {
            GameObject boat = InstantiateBoat(position, Quaternion.Euler(0f, yaw, 0f));
            if (boat == null) return;
            boat.transform.SetParent(container, true);

            // Tìm điểm neo trước mũi ghe; nếu không có thì offset theo hướng mũi (forward).
            Transform anchor = FindDeepChild(boat.transform, bowAnchorName);
            Vector3 poleBase = anchor != null
                ? anchor.position
                : boat.transform.position + boat.transform.forward * 1.6f + Vector3.up * 0.5f;

            GameObject pole = BuildBambooPole(poleBase, boat.transform.rotation);
            pole.transform.SetParent(boat.transform, true);

            // Treo nông sản mẫu lên đỉnh Cây Bẹo.
            GameObject fruit = InstantiateFruit(poleBase + Vector3.up * poleHeight);
            if (fruit != null)
                fruit.transform.SetParent(pole.transform, true);
        }

        private GameObject InstantiateBoat(Vector3 position, Quaternion rotation)
        {
            if (boatPrefabs != null && boatPrefabs.Length > 0)
            {
                GameObject prefab = boatPrefabs[Random.Range(0, boatPrefabs.Length)];
                if (prefab != null)
                {
                    GameObject instance = Instantiate(prefab, position, rotation);
                    instance.name = prefab.name + "_NPC";
                    return instance;
                }
            }

            // Fallback: thân ghe gỗ bằng primitive Cube.
            GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hull.name = "NpcBoat_Fallback";
            hull.transform.SetPositionAndRotation(position, rotation);
            hull.transform.localScale = new Vector3(2.0f, 0.6f, 4.6f);
            Paint(hull, new Color(0.45f, 0.30f, 0.17f));
            return hull;
        }

        private GameObject BuildBambooPole(Vector3 basePosition, Quaternion rotation)
        {
            if (bambooPolePrefab != null)
            {
                GameObject prefabPole = Instantiate(bambooPolePrefab, basePosition, rotation);
                prefabPole.name = "CayBeo";
                return prefabPole;
            }

            // Fallback: cọc tre cao thẳng đứng (primitive Cylinder, scale Y = nửa chiều cao).
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "CayBeo";
            pole.transform.position = basePosition + Vector3.up * (poleHeight * 0.5f);
            pole.transform.localScale = new Vector3(0.07f, poleHeight * 0.5f, 0.07f);
            DisableCollider(pole);
            Paint(pole, new Color(0.62f, 0.55f, 0.28f));
            return pole;
        }

        private GameObject InstantiateFruit(Vector3 topPosition)
        {
            if (fruitPrefabs != null && fruitPrefabs.Length > 0)
            {
                GameObject prefab = fruitPrefabs[Random.Range(0, fruitPrefabs.Length)];
                if (prefab != null)
                {
                    GameObject fruit = Instantiate(prefab, topPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    fruit.name = prefab.name + "_Sample";
                    fruit.transform.localScale = Vector3.one * 0.6f;
                    return fruit;
                }
            }

            // Fallback: trái cây mẫu bằng primitive Sphere màu vàng khóm.
            GameObject sample = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sample.name = "FruitSample";
            sample.transform.position = topPosition;
            sample.transform.localScale = Vector3.one * 0.45f;
            DisableCollider(sample);
            Paint(sample, new Color(0.85f, 0.68f, 0.18f));
            return sample;
        }

        private void ClearSpawned()
        {
            if (container == null) return;
            if (UnityEngine.Application.isPlaying)
                Destroy(container.gameObject);
            else
                DestroyImmediate(container.gameObject);
            container = null;
        }

        // Tìm child theo tên ở mọi cấp (DFS).
        private static Transform FindDeepChild(Transform root, string childName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName) return child;
                Transform found = FindDeepChild(child, childName);
                if (found != null) return found;
            }
            return null;
        }

        private static void DisableCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        private static void Paint(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader) { color = color };
        }

#if UNITY_EDITOR
        // Vẽ khung vùng rải trong Scene view để dễ căn chỉnh.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * waterY, new Vector3(areaSize.x, 0.2f, areaSize.z));
        }
#endif
    }
}
