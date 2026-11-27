using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// [v1.8] 3단계 장비 '원기옥 압축 빔포' 연출.
    ///
    /// 흐름: LMB 를 누르는 동안 앞의 쓰레기를 한 점으로 끌어모아 구슬이 점점 커지고,
    ///       놓거나 최대 크기에 닿으면 트럭/수거통 방향으로 날아가 '우르르' 정산된다.
    /// 내구도 있는 고가치 쓰레기는 구슬 표면에서 버티며 압축 데미지로 깎인 뒤 삼켜진다.
    /// 파티클 에셋 없이 절차 메시 + 기본 프리미티브만 사용한다(WebGL 동일).
    /// </summary>
    public class GarbageOrbProjectile : MonoBehaviour
    {
        public GamePrototypeManager manager;
        private InstancedTrashSwarm _swarm;
        private Transform _visual;
        private Material _coreMat;
        private Transform[] _debris;
        private Mesh _ringMesh;
        private Material _ringMat;

        private int _absorbed;
        private float _age;
        private float _popCooldown;
        private bool _launched;
        private Vector3 _launchTarget;
        private float _launchSpeed = 12f;

        public int Absorbed => _absorbed;

        public static GarbageOrbProjectile Spawn(GamePrototypeManager mgr, InstancedTrashSwarm swarm, Vector3 pos)
        {
            GameObject go = new GameObject("FX_GarbageOrb");
            go.transform.position = pos;
            var orb = go.AddComponent<GarbageOrbProjectile>();
            orb.manager = mgr;
            orb._swarm = swarm;
            orb.Build();
            return orb;
        }

        private void Build()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _coreMat = new Material(sh);
            if (_coreMat.HasProperty("_BaseColor")) _coreMat.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.26f));
            _coreMat.color = new Color(0.42f, 0.34f, 0.26f);
            if (_coreMat.HasProperty("_Smoothness")) _coreMat.SetFloat("_Smoothness", 0.38f);
            if (_coreMat.HasProperty("_EmissionColor"))
            {
                _coreMat.SetColor("_EmissionColor", new Color(1f, 0.72f, 0.22f) * 0.55f);
                _coreMat.EnableKeyword("_EMISSION");
            }

            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.name = "OrbCore";
            core.transform.SetParent(transform, false);
            core.transform.localScale = Vector3.one * 0.42f;
            core.GetComponent<Renderer>().sharedMaterial = _coreMat;
            _visual = core.transform;

            // 구슬 표면에 붙은 파편 4개 — 돌아가며 '쓰레기 덩어리' 느낌을 준다.
            _debris = new Transform[4];
            Material debrisMat = new Material(_coreMat);
            debrisMat.color = new Color(0.72f, 0.60f, 0.40f);
            if (debrisMat.HasProperty("_BaseColor")) debrisMat.SetColor("_BaseColor", new Color(0.72f, 0.60f, 0.40f));
            for (int i = 0; i < _debris.Length; i++)
            {
                GameObject d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(d.GetComponent<Collider>());
                d.name = "OrbDebris" + i;
                d.transform.SetParent(transform, false);
                d.transform.localScale = Vector3.one * Random.Range(0.12f, 0.20f);
                d.GetComponent<Renderer>().sharedMaterial = debrisMat;
                _debris[i] = d.transform;
            }

            // 구슬을 감싸는 회전 링 2개(가산 발광)
            _ringMesh = BuildRingMesh(40, 0.86f, 1.0f);
            _ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? sh);
            if (_ringMat.HasProperty("_Surface")) _ringMat.SetFloat("_Surface", 1f);
            if (_ringMat.HasProperty("_SrcBlend")) _ringMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_ringMat.HasProperty("_DstBlend")) _ringMat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (_ringMat.HasProperty("_ZWrite")) _ringMat.SetFloat("_ZWrite", 0f);
            _ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ringMat.renderQueue = (int)RenderQueue.Transparent + 20;
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.34f, 0.55f));
            _ringMat.color = new Color(1f, 0.82f, 0.34f, 0.55f);
        }

        private static Mesh BuildRingMesh(int segments, float innerR, float outerR)
        {
            Mesh m = new Mesh();
            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);
                int ni = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }

        /// <summary>LMB 를 쥐고 있는 동안 매 프레임 호출 — 중심을 옮기고 쓰레기를 빨아들인다.</summary>
        public void AbsorbTick(Vector3 center, float dt)
        {
            if (_launched || manager == null) return;
            transform.position = Vector3.Lerp(transform.position, center, 12f * dt);
            _age += dt;

            if (_swarm != null)
            {
                _swarm.SetOrbTarget(center, manager.OrbPullRadius, manager.OrbDps);
                _swarm.PullToOrb(48);
                int got = _swarm.ConsumeOrbAbsorbed(out int value);
                if (got > 0)
                {
                    _absorbed += got;
                    manager.OnBulkTrashCollected(got, value);
                    _popCooldown -= dt;
                    if (_popCooldown <= 0f && manager.sfx != null)
                    {
                        _popCooldown = 0.09f;
                        manager.sfx.PlayPop(0.8f + Mathf.Min(0.5f, _absorbed * 0.004f));
                    }
                }
            }

            float size = 0.42f + Mathf.Sqrt(_absorbed) * 0.075f;
            size = Mathf.Min(size, 1.35f);
            if (_visual != null)
            {
                _visual.localScale = Vector3.one * (size * (1f + Mathf.Sin(_age * 12f) * 0.03f));
                _visual.localRotation = Quaternion.Euler(_age * 70f, _age * 110f, _age * 40f);
            }
            if (_debris != null)
            {
                for (int i = 0; i < _debris.Length; i++)
                {
                    float a = _age * (2.2f + i * 0.35f) + i * 1.9f;
                    float r = size * 0.62f;
                    _debris[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f) * r * 0.55f, Mathf.Sin(a) * r);
                    _debris[i].localRotation = Quaternion.Euler(a * 90f, a * 130f, 0f);
                }
            }
            if (_ringMesh != null && _ringMat != null)
            {
                Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(transform.position, Quaternion.Euler(24f, _age * 160f, 12f), Vector3.one * (size * 1.15f)), _ringMat, 0);
                Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(transform.position, Quaternion.Euler(-58f, -_age * 220f, 30f), Vector3.one * (size * 1.35f)), _ringMat, 0);
            }

            // 풀충이거나 '즉시 발사' 노드가 있으면 자동으로 날린다.
            if (manager.OrbCapacity > 0 && _absorbed >= manager.OrbCapacity) Launch();
            else if (manager.OrbAutoLaunch && _absorbed >= manager.OrbCapacity * 0.5f) Launch();
        }

        /// <summary>구슬을 트럭 쪽으로 날려 정산한다.</summary>
        public void Launch()
        {
            if (_launched) return;
            _launched = true;
            if (_swarm != null) _swarm.ClearOrbTarget();
            Transform goal = manager != null ? manager.truckBedGoal : null;
            _launchTarget = goal != null ? goal.position + Vector3.up * 1.1f : transform.position + Vector3.forward * 3f;
            _launchSpeed = manager != null ? manager.OrbFlySpeed : 12f;
            if (manager != null && manager.sfx != null) manager.sfx.PlayPour();
            if (manager != null && manager.player != null) manager.player.AddCameraShake(0.02f, 0.12f);
        }

        private void Update()
        {
            if (!_launched) return;
            transform.position = Vector3.MoveTowards(transform.position, _launchTarget, _launchSpeed * Time.deltaTime);
            _age += Time.deltaTime;
            if (transform.position == _launchTarget || _age > 12f)
            {
                if (manager != null)
                {
                    manager.SettleBinAtTruck(transform.position, true, true, 1f + manager.OrbCashBonus);
                    BurstImpactFx.Spawn(transform.position, new Color(1f, 0.82f, 0.32f), manager.OrbBurstRadius);
                    SparkleBurst.Spawn(transform.position, 26, 1.6f, new Color(1f, 0.92f, 0.55f));
                    if (manager.sfx != null) manager.sfx.PlayBigBurst();
                    if (manager.player != null) manager.player.AddCameraShake(0.055f, 0.18f);
                }
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_swarm != null) _swarm.ClearOrbTarget();
        }
    }

    /// <summary>
    /// [v1.8] 4단계 장비 '미니 블랙홀 건'.
    /// LMB 를 쥐면 전방에 검은 구체 1~3개가 전개되어 주변 쓰레기를 소용돌이 궤도로 빨아들인다.
    /// '화이트홀 정산' 노드가 열려 있으면 빨아들인 쓰레기를 트럭 위로 토해내 자동 정산한다.
    /// </summary>
    public class BlackHoleRig : MonoBehaviour
    {
        public GamePrototypeManager manager;
        private InstancedTrashSwarm _swarm;
        private Transform[] _holes;
        private Material _holeMat;
        private Material _ringMat;
        private Mesh _ringMesh;
        private Vector3[] _centers = new Vector3[3];
        private int _count;
        private float _radius;
        private float _age;
        private float _settleTimer;

        public static BlackHoleRig Spawn(GamePrototypeManager mgr, InstancedTrashSwarm swarm, Vector3 forward, Vector3 origin)
        {
            GameObject go = new GameObject("FX_BlackHoleRig");
            go.transform.position = origin;
            var rig = go.AddComponent<BlackHoleRig>();
            rig.manager = mgr;
            rig._swarm = swarm;
            rig.Deploy(forward, origin);
            return rig;
        }

        private void Deploy(Vector3 forward, Vector3 origin)
        {
            _count = Mathf.Clamp(manager.BlackHoleCount, 1, 3);
            _radius = manager.BlackHoleRadius;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _holeMat = new Material(sh);
            Color dark = new Color(0.02f, 0.015f, 0.03f);
            if (_holeMat.HasProperty("_BaseColor")) _holeMat.SetColor("_BaseColor", dark);
            _holeMat.color = dark;
            if (_holeMat.HasProperty("_Smoothness")) _holeMat.SetFloat("_Smoothness", 0.95f);

            _ringMesh = BuildRing(44, 0.84f, 1.0f);
            _ringMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? sh);
            if (_ringMat.HasProperty("_Surface")) _ringMat.SetFloat("_Surface", 1f);
            if (_ringMat.HasProperty("_SrcBlend")) _ringMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_ringMat.HasProperty("_DstBlend")) _ringMat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (_ringMat.HasProperty("_ZWrite")) _ringMat.SetFloat("_ZWrite", 0f);
            _ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ringMat.renderQueue = (int)RenderQueue.Transparent + 25;
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", new Color(0.45f, 0.30f, 1f, 0.60f));
            _ringMat.color = new Color(0.45f, 0.30f, 1f, 0.60f);

            _holes = new Transform[_count];
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);
            for (int i = 0; i < _count; i++)
            {
                float spread = (_count == 1) ? 0f : (i - (_count - 1) * 0.5f) * 1.35f;
                Vector3 p = origin + flat * 3.0f + right * spread + Vector3.up * 0.95f;
                _centers[i] = p;

                GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(hole.GetComponent<Collider>());
                hole.name = "BlackHole" + i;
                hole.transform.SetParent(transform, true);
                hole.transform.position = p;
                hole.transform.localScale = Vector3.one * 0.34f;
                hole.GetComponent<Renderer>().sharedMaterial = _holeMat;
                _holes[i] = hole.transform;
            }
        }

        private static Mesh BuildRing(int segments, float innerR, float outerR)
        {
            Mesh m = new Mesh();
            Vector3[] verts = new Vector3[segments * 2];
            int[] tris = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, 0f, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, 0f, s * outerR);
                int ni = (i + 1) % segments;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
            }
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }

        private void Update()
        {
            if (manager == null) return;
            float dt = Time.deltaTime;
            _age += dt;

            if (_swarm != null)
            {
                _swarm.SetBlackHoles(_centers, _count, _radius, manager.BlackHoleSpin, manager.BlackHoleDps);
                _swarm.BlackHoleTick(72);
                int got = _swarm.ConsumeVortexAbsorbed(out int value);
                if (got > 0) manager.OnBulkTrashCollected(got, value);
            }

            for (int i = 0; i < _count; i++)
            {
                if (_holes[i] == null) continue;
                float s = 0.34f + Mathf.Sin(_age * 6f + i) * 0.02f;
                _holes[i].localScale = Vector3.one * s;
                _holes[i].Rotate(Vector3.up, 220f * dt * manager.BlackHoleSpin, Space.World);
                float rr = _radius * (0.55f + 0.06f * Mathf.Sin(_age * 3f + i));
                Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(_centers[i], Quaternion.Euler(64f, _age * 180f * manager.BlackHoleSpin, 0f), Vector3.one * rr), _ringMat, 0);
                Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(_centers[i], Quaternion.Euler(-20f, -_age * 240f * manager.BlackHoleSpin, 40f), Vector3.one * (rr * 0.7f)), _ringMat, 0);
            }

            // 화이트홀: 빨아들인 쓰레기를 일정 주기로 트럭 위에 토해 자동 정산.
            if (manager.HasWhiteHole)
            {
                _settleTimer -= dt;
                if (_settleTimer <= 0f && manager.CollectingCount > 0)
                {
                    _settleTimer = manager.WhiteHoleAuto ? 0.45f : 0.8f;
                    manager.SettleBinAtTruck(_centers[0], true, true, 1f + manager.WhiteHoleCash);
                }
            }

            if (_age >= manager.BlackHoleDuration) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_swarm != null) _swarm.ClearBlackHoles();
        }
    }
}
