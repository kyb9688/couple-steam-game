using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.3 서브 자동화 펫: 바닥을 돌며 쓰레기를 자동 수거하는 미니 로봇청소기.
    /// 요청 #14 반영 — 예전에는 Lv.1·3·5에서 1대씩 추가되고 한 번에 1+LvRoomba개까지 먹어
    /// 플레이어 주력을 넘어버렸다. 지금은 '시작 0대'에서 강화소 첫 구매로 1대가 등장하고(최대 3대),
    /// 속도는 로봇을 보유한 뒤에만 올린다. 언제나 '보조'.
    /// </summary>
    public class AutoRoombaCleaner : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public float suckInterval = 1.0f;   // 매니저가 매 프레임 실제 값으로 덮어쓴다
        public float moveSpeed = 1.85f;

        private Vector3 _targetPos;
        private float _retargetTimer;
        private float _suckTimer;
        private bool _hasTarget;

        public static AutoRoombaCleaner Spawn(GamePrototypeManager mgr, Vector3 startPos, int index)
        {
            GameObject root = new GameObject($"AutoRoomba_{index + 1:00}");
            root.transform.position = new Vector3(startPos.x, 0.05f, startPos.z);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.36f, 0.055f, 0.36f);
            Destroy(body.GetComponent<Collider>());
            Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMat.SetColor("_BaseColor", new Color(0.14f, 0.16f, 0.20f));
            bodyMat.SetFloat("_Smoothness", 0.72f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            GameObject led = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            led.transform.SetParent(root.transform, false);
            led.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            led.transform.localScale = new Vector3(0.16f, 0.012f, 0.16f);
            Destroy(led.GetComponent<Collider>());
            Material ledMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            ledMat.SetColor("_BaseColor", new Color(0.25f, 0.92f, 1.0f));
            led.GetComponent<Renderer>().sharedMaterial = ledMat;

            var bot = root.AddComponent<AutoRoombaCleaner>();
            bot.manager = mgr;
            return bot;
        }

        private void Update()
        {
            if (manager == null || manager.trashSwarm == null) return;
            if (manager.Phase != GamePhase.Phase1_TrashRemoval && manager.Phase != GamePhase.Phase2_FloorMopping) return;

            float dt = Time.deltaTime;
            _retargetTimer -= dt;
            _suckTimer -= dt;

            if (_retargetTimer <= 0f || !_hasTarget)
            {
                _retargetTimer = 0.65f;
                _hasTarget = manager.trashSwarm.TryFindNearestIdlePos(transform.position, out _targetPos);
                if (!_hasTarget)
                {
                    // 쓰레기가 없으면 방 안을 순찰
                    _targetPos = new Vector3(Random.Range(-2.8f, 2.8f), 0.05f, Random.Range(-2.3f, 2.2f));
                }
            }

            Vector3 flatTarget = new Vector3(_targetPos.x, 0.05f, _targetPos.z);
            Vector3 to = flatTarget - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.02f)
            {
                float spd = manager.RoombaMoveSpeed;
                Vector3 next = transform.position + to.normalized * (spd * dt);
                bool st2 = manager.CurrentStage >= 2;
                next.x = Mathf.Clamp(next.x, st2 ? -7.2f : -4.2f, st2 ? 7.2f : 4.2f);
                next.z = Mathf.Clamp(next.z, st2 ? -8.4f : -3.5f, 3.4f);
                next.y = 0.05f;
                transform.position = next;
                transform.Rotate(Vector3.up * (180f * dt));
            }

            if (_suckTimer <= 0f && manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                // v1.3: 한 번에 1개, 간격은 1.00초 → 최대 강화해도 0.55초 (플레이어보다 항상 느린 보조)
                _suckTimer = manager.RoombaEatInterval;
                int eaten = manager.trashSwarm.SuckNearestForRobot(transform.position, 0.85f, transform, 1);
                if (eaten > 0)
                {
                    manager.OnRobotAutoCleaned(eaten, transform.position);
                }
            }
        }
    }
}
