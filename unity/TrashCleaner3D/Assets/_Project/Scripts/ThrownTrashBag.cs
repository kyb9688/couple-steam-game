using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 꽉 찬 쓰레기봉투를 열린 현관문 밖 쓰레기 트럭 짐칸으로 농구 슛처럼 던질 때 쓰는 투사체.
    /// 던져진 봉투 1개만 물리(Rigidbody) ON이라 전체 최적화(쓰레기 500개 물리 OFF)를 깨지 않습니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ThrownTrashBag : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public Transform truckGoal;
        public Vector3 throwOrigin;
        public int bagItemCount = 6;
        public bool hasMagnetUpgrade;

        private Rigidbody _rb;
        private bool _scored;
        private float _lifeTimer;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void Launch(Vector3 velocity, Vector3 origin, Transform goal, GamePrototypeManager mgr, int count, bool magnet)
        {
            throwOrigin = origin;
            truckGoal = goal;
            manager = mgr;
            bagItemCount = count;
            hasMagnetUpgrade = magnet;

            _rb.linearVelocity = velocity;
            _rb.angularVelocity = Random.insideUnitSphere * 6f;
        }

        private void FixedUpdate()
        {
            if (_scored) return;
            _lifeTimer += Time.fixedDeltaTime;

            // 트럭 자석 압축기 업그레이드 시 문 밖 트럭 쪽으로 쫙 빨려 들어감!
            if (hasMagnetUpgrade && truckGoal != null)
            {
                Vector3 toGoal = truckGoal.position - transform.position;
                if (toGoal.magnitude < 5.2f)
                {
                    _rb.AddForce(toGoal.normalized * 26f, ForceMode.Acceleration);
                }
            }

            // 골인 거리 판정 (트럭 크기에 따라 판정 반경 확대)
            if (truckGoal != null && manager != null)
            {
                float goalRadius = 1.25f + manager.LvTruckSize * 0.38f;
                Vector3 flat = transform.position - truckGoal.position;
                flat.y *= 0.65f;
                if (flat.magnitude <= goalRadius)
                {
                    ScoreGoal();
                    return;
                }
            }

            // 4.5초 지나도 못 들어갔으면 자동 수거 처리(바닥에 영원히 굴러다니는 스트레스 방지)
            if (_lifeTimer >= 4.2f)
            {
                if (manager != null) manager.OnBagScored(bagItemCount, 0f, false);
                Destroy(gameObject);
            }
        }

        private void ScoreGoal()
        {
            _scored = true;
            float dist = Vector3.Distance(throwOrigin, truckGoal.position);
            if (manager != null)
            {
                manager.OnBagScored(bagItemCount, dist, true);
            }
            Destroy(gameObject);
        }
    }
}
