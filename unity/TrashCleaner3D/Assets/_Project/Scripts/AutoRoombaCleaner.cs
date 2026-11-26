using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 스팀 인크리멘탈 핵심 자동화 펫: 방 바닥을 직접 돌아다니며 쓰레기를 자동 흡입하는 3D 미니 로봇청소기!
    /// 스킬트리에서 해금/강화(Lv.1~3)할 때마다 대수와 작업 속도가 증가합니다.
    /// </summary>
    public class AutoRoombaCleaner : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public float moveSpeed = 2.1f;
        public float suckInterval = 0.55f;

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
                float spd = moveSpeed * (1f + manager.LvRoomba * 0.22f);
                Vector3 next = transform.position + to.normalized * (spd * dt);
                next.x = Mathf.Clamp(next.x, -3.15f, 3.15f);
                next.z = Mathf.Clamp(next.z, -2.55f, 2.45f);
                next.y = (next.x < -1.55f && next.z < -0.55f) ? 0.36f : 0.05f;
                transform.position = next;
                transform.Rotate(Vector3.up * (180f * dt));
            }

            if (_suckTimer <= 0f && manager.Phase == GamePhase.Phase1_TrashRemoval)
            {
                _suckTimer = Mathf.Max(0.18f, suckInterval - manager.LvRoomba * 0.11f);
                int eaten = manager.trashSwarm.SuckNearestForRobot(transform.position, 0.85f, transform, 1 + manager.LvRoomba);
                if (eaten > 0)
                {
                    manager.OnRobotAutoCleaned(eaten, transform.position);
                }
            }
        }
    }
}
