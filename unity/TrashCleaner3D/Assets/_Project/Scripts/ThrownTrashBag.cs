using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.4 던지는 쓰레기봉투:
    /// - 좁고 짜릿한 트럭 짐칸 투입구 판정 (강화할수록 넓어지고 자석 흡입!)
    /// - 실패해서 바닥에 떨어져도 사라지지 않고 유지 → 마우스 올리면 하얀 테두리 → [좌클릭]/[F]로 다시 주워 재투하 가능!
    /// - 골인 시 착지 3D 위치에 돈 팝업 + 트럭 짐칸 안에 실제 3D 봉투 쌓기!
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrownTrashBag : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform truckGoal;
        public Vector3 throwOrigin;
        public int bagItemCount = 15;
        public int bagPendingValue = 45;
        public bool hasMagnetUpgrade;

        public bool IsRetrievable { get; private set; }

        private Rigidbody _rb;
        private bool _scored;
        private float _lifeTimer;
        private GameObject _outlineRoot;
        private bool _highlighted;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            BuildWhiteOutline();
        }

        private void BuildWhiteOutline()
        {
            var mfs = GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return;

            Shader sh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material outlineMat = new Material(sh);
            outlineMat.SetColor("_OutlineColor", Color.white);
            outlineMat.SetFloat("_OutlineWidth", 0.018f);

            _outlineRoot = new GameObject("_WhiteOutline");
            _outlineRoot.transform.SetParent(transform, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_Outline");
                copy.transform.SetParent(_outlineRoot.transform, false);
                copy.transform.position = mf.transform.position;
                copy.transform.rotation = mf.transform.rotation;
                copy.transform.localScale = mf.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = copy.AddComponent<MeshRenderer>();
                mr.sharedMaterial = outlineMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _outlineRoot.SetActive(false);
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;
            if (_outlineRoot != null) _outlineRoot.SetActive(on);
        }

        public void Launch(Vector3 velocity, Vector3 origin, Transform goal, GamePrototypeManager mgr, int count, int pendingValue, bool magnet)
        {
            throwOrigin = origin;
            truckGoal = goal;
            manager = mgr;
            bagItemCount = count;
            bagPendingValue = pendingValue;
            hasMagnetUpgrade = magnet;
            IsRetrievable = false;

            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * 5.5f;
        }

        private void FixedUpdate()
        {
            if (_scored) return;
            _lifeTimer += Time.fixedDeltaTime;

            // 트럭 자석 압축기 업그레이드 시 근처를 지나는 봉투만 부드럽게 끌어당김
            if (hasMagnetUpgrade && truckGoal != null && !IsRetrievable)
            {
                Vector3 toGoal = truckGoal.position - transform.position;
                if (toGoal.magnitude < 3.4f)
                {
                    _rb.AddForce(toGoal.normalized * 24f, ForceMode.Acceleration);
                }
            }

            // 타이트한 트럭 짐칸 골인 판정 (초반엔 정확히 짐칸 안으로 넣어야 골인! 트럭 확장 강화 시 넓어짐)
            if (truckGoal != null && manager != null)
            {
                float halfW = 0.56f + manager.LvTruckSize * 0.24f;
                float halfD = 0.52f + manager.LvTruckSize * 0.22f;
                Vector3 diff = transform.position - truckGoal.position;

                bool insideXZ = Mathf.Abs(diff.x) <= halfW && Mathf.Abs(diff.z) <= halfD;
                bool insideY = diff.y >= -0.45f && diff.y <= 0.55f;

                if (insideXZ && insideY)
                {
                    ScoreGoal();
                    return;
                }
            }

            // 0.6초 이후 땅에 굴러 멈췄거나 속도가 느려지면 다시 주울 수 있는 상태로 전환! (절대 자동 삭제 안 함!)
            if (!IsRetrievable && _lifeTimer >= 0.65f && (_rb.linearVelocity.magnitude < 0.75f || transform.position.y < 0.35f))
            {
                IsRetrievable = true;
            }

            // 맵 밖으로 떨어졌을 때만 현관문 앞 마당으로 안전 복귀
            if (transform.position.y < -2.0f)
            {
                transform.position = new Vector3(1.5f, 0.5f, 3.2f);
                _rb.linearVelocity = Vector3.zero;
                IsRetrievable = true;
            }
        }

        public void RetrieveIntoHand()
        {
            if (_scored) return;
            _scored = true;
            if (manager != null)
            {
                manager.OnDroppedBagRetrieved(bagItemCount, bagPendingValue);
            }
            Destroy(gameObject);
        }

        private void ScoreGoal()
        {
            _scored = true;
            float dist = Vector3.Distance(throwOrigin, truckGoal.position);
            if (manager != null)
            {
                manager.OnBagScored(bagItemCount, bagPendingValue, dist, transform.position);
            }
            Destroy(gameObject);
        }
    }
}
