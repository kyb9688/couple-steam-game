using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// [v2.0] 4티어 장비 '산업용 사이클론 청소기' (기존 원기옥 빔포 전면 대체!).
    ///
    /// - 전방 노즐에 다중 회전 사이클론 기류 링과 흡입 원뿔을 펼쳐 주변 쓰레기를 '화아악!' 빨아들인다.
    /// - 수거통 용량(BinCapacity)을 철저히 준수해 용량 초과 버그가 절대 발생하지 않는다.
    /// </summary>
    public class GarbageOrbProjectile : MonoBehaviour
    {
        public GamePrototypeManager manager;
        private InstancedTrashSwarm _swarm;
        private Mesh _ringMesh;
        private Material _ringMat;
        private Material _coreMat;
        private Transform _nozzleCone;

        private int _absorbed;
        private float _age;
        private float _popCooldown;

        public int Absorbed => _absorbed;

        public static GarbageOrbProjectile Spawn(GamePrototypeManager mgr, InstancedTrashSwarm swarm, Vector3 pos)
        {
            GameObject go = new GameObject("FX_CycloneVacuum");
            go.transform.position = pos;
            var fx = go.AddComponent<GarbageOrbProjectile>();
            fx.manager = mgr;
            fx._swarm = swarm;
            fx.Build();
            return fx;
        }

        private void Build()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            _ringMesh = BuildRingMesh(40, 0.80f, 1.0f);

            _ringMat = new Material(unlit);
            if (_ringMat.HasProperty("_Surface")) _ringMat.SetFloat("_Surface", 1f);
            if (_ringMat.HasProperty("_SrcBlend")) _ringMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_ringMat.HasProperty("_DstBlend")) _ringMat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (_ringMat.HasProperty("_ZWrite")) _ringMat.SetFloat("_ZWrite", 0f);
            _ringMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _ringMat.renderQueue = (int)RenderQueue.Transparent + 20;
            Color cyan = new Color(0.28f, 0.88f, 1.0f, 0.52f);
            if (_ringMat.HasProperty("_BaseColor")) _ringMat.SetColor("_BaseColor", cyan);
            _ringMat.color = cyan;

            _coreMat = new Material(_ringMat);
            Color soft = new Color(0.55f, 0.94f, 1.0f, 0.24f);
            if (_coreMat.HasProperty("_BaseColor")) _coreMat.SetColor("_BaseColor", soft);
            _coreMat.color = soft;

            // 바닥 흡입 중심 회오리 코어
            GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(cone.GetComponent<Collider>());
            cone.name = "CycloneCore";
            cone.transform.SetParent(transform, false);
            cone.transform.localScale = new Vector3(0.45f, 0.42f, 0.45f);
            var r = cone.GetComponent<Renderer>();
            r.sharedMaterial = _coreMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            _nozzleCone = cone.transform;
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

        /// <summary>LMB 홀드 중 매 프레임 호출 — 사이클론 기류로 전방 쓰레기를 수거통 용량 한도 내에서 빨아들인다.</summary>
        public void AbsorbTick(Vector3 center, float dt)
        {
            if (manager == null) return;
            transform.position = Vector3.Lerp(transform.position, center, 16f * dt);
            _age += dt;

            if (_swarm != null && !manager.IsBinFull)
            {
                float rad = Mathf.Clamp(manager.OrbPullRadius, 1.8f, 4.6f);
                _swarm.SetOrbTarget(transform.position, rad, manager.OrbDps);
                // 프레임당 적정 흡입량 (수거통 여유 공간 엄수!)
                int pullBudget = Mathf.Clamp(Mathf.RoundToInt(18f * manager.OrbGrowRate), 14, 52);
                _swarm.PullToOrb(pullBudget);
                int got = _swarm.ConsumeOrbAbsorbed(out int value);
                if (got > 0)
                {
                    _absorbed += got;
                    manager.OnBulkTrashCollected(got, value);
                    _popCooldown -= dt;
                    if (_popCooldown <= 0f && manager.sfx != null)
                    {
                        _popCooldown = 0.085f;
                        manager.sfx.PlayPop(0.95f + Mathf.Min(0.45f, _absorbed * 0.002f));
                    }
                }
            }

            float radVis = Mathf.Clamp(manager.OrbPullRadius * 0.55f, 0.9f, 2.5f);
            if (_nozzleCone != null)
            {
                float pulse = 1f + Mathf.Sin(_age * 18f) * 0.08f;
                _nozzleCone.localScale = new Vector3(radVis * 0.36f * pulse, 0.48f, radVis * 0.36f * pulse);
                _nozzleCone.localRotation = Quaternion.Euler(0f, _age * 380f, 0f);
            }
            if (_ringMesh != null && _ringMat != null)
            {
                // 3단 회전 사이클론 기류 링 (아래에서 위로 말려 올라가는 바람 연출)
                for (int k = 0; k < 3; k++)
                {
                    float phase = Mathf.Repeat(_age * 1.8f + k / 3f, 1f);
                    float yOff = Mathf.Lerp(-0.22f, 0.65f, phase);
                    float rScale = Mathf.Lerp(radVis, radVis * 0.38f, phase);
                    Graphics.DrawMesh(
                        _ringMesh,
                        Matrix4x4.TRS(transform.position + Vector3.up * yOff, Quaternion.Euler(0f, _age * (260f + k * 90f), 0f), Vector3.one * rScale),
                        _ringMat, 0);
                }
            }
        }

        public void Launch()
        {
            if (_swarm != null) _swarm.ClearOrbTarget();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_swarm != null) _swarm.ClearOrbTarget();
        }
    }

    /// <summary>
    /// [v2.0] 5티어 최종 장비 '자율 청소 드론 편대 + 트럭 직결 호스' (기존 미니 블랙홀 전면 대체!).
    /// - 프로펠러가 회전하는 자율 청소 드론 1~3대가 플레이어 전방·좌우를 선회하며 바닥/가구 밑 쓰레기를 흡입한다.
    /// - 수거통 용량(BinCapacity)을 철저히 준수하며, '직결 메가 호스(구 화이트홀)' 해금 시 수거통이 차면 트럭으로 직결 정산한다.
    /// </summary>
    public class BlackHoleRig : MonoBehaviour
    {
        public GamePrototypeManager manager;
        private InstancedTrashSwarm _swarm;
        private Transform[] _drones;
        private Transform[] _rotors;
        private Material _bodyMat;
        private Material _accentMat;
        private Material _beamMat;
        private Mesh _ringMesh;
        private Vector3[] _centers = new Vector3[3];
        private int _count;
        private float _radius;
        private float _age;
        private float _settleTimer;
        private float _popCooldown;

        public static BlackHoleRig Spawn(GamePrototypeManager mgr, InstancedTrashSwarm swarm, Vector3 forward, Vector3 origin)
        {
            GameObject go = new GameObject("FX_CleanerDroneSquad");
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
            _radius = Mathf.Clamp(manager.BlackHoleRadius, 2.0f, 5.2f);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? lit;

            _bodyMat = new Material(lit);
            Color bodyCol = new Color(0.16f, 0.22f, 0.30f);
            if (_bodyMat.HasProperty("_BaseColor")) _bodyMat.SetColor("_BaseColor", bodyCol);
            _bodyMat.color = bodyCol;
            if (_bodyMat.HasProperty("_Metallic")) _bodyMat.SetFloat("_Metallic", 0.55f);
            if (_bodyMat.HasProperty("_Smoothness")) _bodyMat.SetFloat("_Smoothness", 0.72f);

            _accentMat = new Material(lit);
            Color neon = new Color(0.16f, 0.92f, 0.68f);
            if (_accentMat.HasProperty("_BaseColor")) _accentMat.SetColor("_BaseColor", neon);
            _accentMat.color = neon;
            if (_accentMat.HasProperty("_EmissionColor"))
            {
                _accentMat.SetColor("_EmissionColor", neon * 0.95f);
                _accentMat.EnableKeyword("_EMISSION");
            }

            _ringMesh = BuildRing(36, 0.84f, 1.0f);
            _beamMat = new Material(unlit);
            if (_beamMat.HasProperty("_Surface")) _beamMat.SetFloat("_Surface", 1f);
            if (_beamMat.HasProperty("_SrcBlend")) _beamMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (_beamMat.HasProperty("_DstBlend")) _beamMat.SetFloat("_DstBlend", (float)BlendMode.One);
            if (_beamMat.HasProperty("_ZWrite")) _beamMat.SetFloat("_ZWrite", 0f);
            _beamMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _beamMat.renderQueue = (int)RenderQueue.Transparent + 22;
            Color beamCol = new Color(0.20f, 0.95f, 0.72f, 0.42f);
            if (_beamMat.HasProperty("_BaseColor")) _beamMat.SetColor("_BaseColor", beamCol);
            _beamMat.color = beamCol;

            _drones = new Transform[_count];
            _rotors = new Transform[_count];

            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);

            for (int i = 0; i < _count; i++)
            {
                float spread = (_count == 1) ? 0f : (i - (_count - 1) * 0.5f) * 1.45f;
                Vector3 p = origin + flat * 2.5f + right * spread + Vector3.up * 1.05f;
                _centers[i] = p;

                GameObject root = new GameObject("CleanerDrone_" + i);
                root.transform.SetParent(transform, true);
                root.transform.position = p;

                // 드론 본체 (원반형 산업 드론)
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = new Vector3(0.38f, 0.065f, 0.38f);
                body.GetComponent<Renderer>().sharedMaterial = _bodyMat;

                // 네온 상태 LED 링
                GameObject led = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(led.GetComponent<Collider>());
                led.transform.SetParent(root.transform, false);
                led.transform.localPosition = new Vector3(0f, 0.045f, 0f);
                led.transform.localScale = new Vector3(0.20f, 0.025f, 0.20f);
                led.GetComponent<Renderer>().sharedMaterial = _accentMat;

                // 하단 흡입 빔 원뿔
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(beam.GetComponent<Collider>());
                beam.transform.SetParent(root.transform, false);
                beam.transform.localPosition = new Vector3(0f, -0.45f, 0f);
                beam.transform.localScale = new Vector3(0.24f, 0.42f, 0.24f);
                var br = beam.GetComponent<Renderer>();
                br.sharedMaterial = _beamMat;
                br.shadowCastingMode = ShadowCastingMode.Off;

                // 상단 회전 로터 바
                GameObject rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(rotor.GetComponent<Collider>());
                rotor.transform.SetParent(root.transform, false);
                rotor.transform.localPosition = new Vector3(0f, 0.095f, 0f);
                rotor.transform.localScale = new Vector3(0.56f, 0.015f, 0.055f);
                rotor.GetComponent<Renderer>().sharedMaterial = _accentMat;

                _drones[i] = root.transform;
                _rotors[i] = rotor.transform;
            }
        }

        public void FollowPlayer(Vector3 playerPos, Vector3 forward, float floorY, float dt)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
            flat.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, flat);

            for (int i = 0; i < _count; i++)
            {
                float spread = (_count == 1) ? 0f : (i - (_count - 1) * 0.5f) * 1.55f;
                float orbit = Mathf.Sin(_age * 2.2f + i * 2.1f) * 0.35f;
                float bob = Mathf.Sin(_age * 4.5f + i) * 0.09f;
                Vector3 target = playerPos + flat * (2.4f + orbit) + right * spread;
                target.y = floorY + 1.15f + bob;
                _centers[i] = Vector3.Lerp(_centers[i], target, 7.5f * dt);
                if (_drones[i] != null) _drones[i].position = _centers[i];
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

            // 수거통 용량 엄수! (직결 호스가 없으면 수거통 가득 차면 흡입 중단)
            if (_swarm != null && !manager.IsBinFull)
            {
                _swarm.SetBlackHoles(_centers, _count, _radius, manager.BlackHoleSpin, manager.BlackHoleDps);
                int pullBudget = Mathf.Clamp(22 * _count, 20, 72);
                _swarm.BlackHoleTick(pullBudget);
                int got = _swarm.ConsumeVortexAbsorbed(out int value);
                if (got > 0)
                {
                    manager.OnBulkTrashCollected(got, value);
                    _popCooldown -= dt;
                    if (_popCooldown <= 0f && manager.sfx != null)
                    {
                        _popCooldown = 0.11f;
                        manager.sfx.PlayPop(1.12f);
                    }
                }
            }

            for (int i = 0; i < _count; i++)
            {
                if (_rotors[i] != null) _rotors[i].Rotate(Vector3.up, 980f * dt, Space.Self);
                float rr = _radius * 0.42f;
                Vector3 groundPos = new Vector3(_centers[i].x, _centers[i].y - 0.85f, _centers[i].z);
                Graphics.DrawMesh(_ringMesh, Matrix4x4.TRS(groundPos, Quaternion.Euler(0f, _age * 200f, 0f), Vector3.one * rr), _beamMat, 0);
            }

            // 트럭 직결 호스(구 화이트홀): 수거통이 75% 이상 차면 깔끔하게 트럭으로 직결 정산!
            if (manager.HasWhiteHole && manager.BinFill01 >= 0.75f)
            {
                _settleTimer -= dt;
                if (_settleTimer <= 0f && manager.CollectingCount > 0)
                {
                    _settleTimer = manager.WhiteHoleAuto ? 0.90f : 1.45f;
                    manager.SettleBinAtTruck(_centers[0], true, true, 1f + manager.WhiteHoleCash);
                }
            }
        }

        private void OnDestroy()
        {
            if (_swarm != null) _swarm.ClearBlackHoles();
        }
    }
}
