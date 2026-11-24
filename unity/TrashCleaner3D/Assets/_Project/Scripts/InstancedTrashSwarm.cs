using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 1,000개 이상의 3D 쓰레기를 렉 0%(144fps)로 돌리기 위한 GPU Instancing + Zero-GameObject 매니저.
    /// - 개별 GameObject / Collider / Update() 0개!
    /// - 단 4~5번의 Graphics.RenderMeshInstanced 드로우콜로 1,000개 3D 쓰레기를 일괄 렌더링
    /// - 순수 수학 거리 비교(sqrMagnitude)로 동전 줍기처럼 넓은 반경 촤르르륵 연속 흡입!
    /// </summary>
    public class InstancedTrashSwarm : MonoBehaviour
    {
        public struct TrashUnit
        {
            public Vector3 pos;
            public Quaternion rot;
            public float baseScale;
            public float curScale;
            public int typeIdx;
            public byte state; // 0:Idle, 1:Popping, 2:Sucking, 3:Cleaned
            public float timer;
            public Vector3 startPos;
            public Vector3 endPos;
            public int reward;
        }

        [System.Serializable]
        public class TrashMeshType
        {
            public string label;
            public Mesh mesh;
            public Material material;
            public float normalizedScale = 1f;
        }

        public List<TrashMeshType> meshTypes = new List<TrashMeshType>();
        public GamePrototypeManager manager;

        private TrashUnit[] _units = new TrashUnit[0];
        private int _unitCount;
        private int _cleanedCount;
        private Transform _suckDestination;

        // Per-frame instancing buffers (max 1023 per Graphics.RenderMeshInstanced call)
        private Matrix4x4[][] _batchMatrices;
        private int[] _batchCounts;
        private RenderParams[] _renderParams;

        // Beacon mesh for 10% remaining detect upgrade
        private Mesh _beaconMesh;
        private Material _beaconMat;
        private Matrix4x4[] _beaconMatrices = new Matrix4x4[64];

        public int TotalCount => _unitCount;
        public int CleanedCount => _cleanedCount;
        public int RemainingCount => Mathf.Max(0, _unitCount - _cleanedCount);

        public void InitializeCapacity(int capacity)
        {
            _units = new TrashUnit[capacity];
            _unitCount = 0;
            _cleanedCount = 0;
            EnsureRenderBuffers();
        }

        private void EnsureRenderBuffers()
        {
            int types = Mathf.Max(1, meshTypes.Count);
            _batchMatrices = new Matrix4x4[types][];
            _batchCounts = new int[types];
            _renderParams = new RenderParams[types];

            for (int t = 0; t < meshTypes.Count; t++)
            {
                _batchMatrices[t] = new Matrix4x4[1023];
                if (meshTypes[t].material != null)
                {
                    meshTypes[t].material.enableInstancing = true;
                    _renderParams[t] = new RenderParams(meshTypes[t].material)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 50f),
                        shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                        receiveShadows = true
                    };
                }
            }

            if (_beaconMesh == null)
            {
                GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _beaconMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
                _beaconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _beaconMat.enableInstancing = true;
                _beaconMat.SetColor("_BaseColor", new Color(0.42f, 1.0f, 0.45f, 0.65f));
            }
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
            if (_units == null || _units.Length == 0)
            {
                SpawnInitialSwarm(1000);
            }
            else
            {
                EnsureRenderBuffers();
            }
        }

        /// <summary>
        /// 방 바닥 전역에 1,000개의 3D 소형 쓰레기를 자연스러운 더미 분포로 생성 (물리 0, 렉 0!)
        /// </summary>
        public void SpawnInitialSwarm(int count)
        {
            if (meshTypes.Count == 0) return;
            InitializeCapacity(count + 400); // 큰 봉투 터질 때 추가 스폰 여유분 포함

            // 쓰레기 밀집 클러스터 중심점 7개 (한국 원룸 사진처럼 구석·벽·냉장고 옆에 수북하게!)
            Vector3[] hubs =
            {
                new Vector3(1.65f, 0f, 0.45f),
                new Vector3(1.65f, 0f, -0.45f),
                new Vector3(1.45f, 0f, -1.15f),
                new Vector3(0.55f, 0f, 0.75f),
                new Vector3(-0.45f, 0f, 1.25f),
                new Vector3(0.40f, 0f, -0.40f),
                new Vector3(-0.95f, 0f, 0.95f)
            };

            for (int i = 0; i < count; i++)
            {
                Vector3 pos;
                if (i < count * 0.72f)
                {
                    // 72%는 클러스터에 겹겹이 쌓인 느낌으로 배치 (높이도 살짝 얹음!)
                    Vector3 h = hubs[i % hubs.Length];
                    Vector2 off = Random.insideUnitCircle * Random.Range(0.15f, 0.95f);
                    float dist01 = Mathf.Clamp01(off.magnitude / 0.95f);
                    float yStack = (1f - dist01) * Random.Range(0.02f, 0.28f);
                    pos = new Vector3(
                        Mathf.Clamp(h.x + off.x, -2.05f, 2.05f),
                        0.03f + yStack,
                        Mathf.Clamp(h.z + off.y, -1.72f, 1.68f)
                    );
                }
                else
                {
                    // 28%는 방 바닥 전역에 흩뿌림 (동전 줍기 쾌감!)
                    pos = new Vector3(Random.Range(-1.95f, 1.95f), 0.03f, Random.Range(-1.65f, 1.65f));
                }

                // 매트리스 위(-2.1..-0.72, -1.9..0.0)와 플레이어 스폰 앞 최소 공간은 살짝 피하거나 위에 얹음
                if (pos.x < -0.70f && pos.z < 0.02f) pos.y += 0.30f; // 매트리스 위에 흩뿌려진 쓰레기!
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-0.15f, -1.55f)) < 0.55f)
                {
                    pos.z += 0.75f;
                }

                AddUnit(pos, pos, false);
            }
        }

        /// <summary>
        /// 큰 쓰레기봉투가 퍽! 터질 때 작은 쓰레기 25~30개를 분수처럼 쏟아냄!
        /// </summary>
        public void SpawnBurstFountain(Vector3 origin, int count)
        {
            if (meshTypes.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                if (_unitCount >= _units.Length) break;
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.25f, 1.15f);
                Vector3 land = new Vector3(
                    Mathf.Clamp(origin.x + off.x, -1.95f, 1.95f),
                    0.04f,
                    Mathf.Clamp(origin.z + off.y, -1.65f, 1.65f)
                );
                if (land.x < -0.70f && land.z < 0.02f) land.y += 0.30f;
                AddUnit(origin + Vector3.up * 0.35f, land, true);
            }
        }

        private void AddUnit(Vector3 startPos, Vector3 endPos, bool popAnimation)
        {
            if (_unitCount >= _units.Length) return;
            int tIdx = Random.Range(0, meshTypes.Count);
            float baseS = meshTypes[tIdx].normalizedScale * Random.Range(0.82f, 1.22f);

            _units[_unitCount++] = new TrashUnit
            {
                pos = startPos,
                rot = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)),
                baseScale = baseS,
                curScale = baseS,
                typeIdx = tIdx,
                state = popAnimation ? (byte)1 : (byte)0,
                timer = 0f,
                startPos = startPos,
                endPos = endPos,
                reward = 1
            };
        }

        /// <summary>
        /// 동전 줍기식 넓은 반경 훑기! 조준 지점(aimPoint) 반경 안의 쓰레기를 한 번에 촤르르 흡입 시작.
        /// 반환값: 이번에 새로 빨아들이기 시작한 쓰레기 수.
        /// </summary>
        public int SweepAt(Vector3 aimPoint, float radius, int maxGrabThisTick, Transform suckDest)
        {
            if (maxGrabThisTick <= 0 || _unitCount == 0) return 0;
            _suckDestination = suckDest;

            float rSq = radius * radius;
            int grabbed = 0;

            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - aimPoint;
                // Y축 차이는 넉넉히 허용해 바닥·더미·매트리스 위 쓰레기도 스트레스 없이 싹 훑어짐!
                d.y *= 0.45f;
                if (d.sqrMagnitude <= rSq)
                {
                    _units[i].state = 2; // Sucking!
                    _units[i].timer = 0f;
                    grabbed++;
                    if (grabbed >= maxGrabThisTick) break;
                }
            }
            return grabbed;
        }

        public bool HasAnyIdleNear(Vector3 aimPoint, float radius)
        {
            float rSq = radius * radius;
            for (int i = 0; i < _unitCount; i++)
            {
                if (_units[i].state != 0) continue;
                Vector3 d = _units[i].pos - aimPoint;
                d.y *= 0.45f;
                if (d.sqrMagnitude <= rSq) return true;
            }
            return false;
        }

        public void ResetSwarm(int initialCount)
        {
            SpawnInitialSwarm(initialCount);
        }

        private void Update()
        {
            if (_unitCount == 0 || meshTypes.Count == 0 || _batchMatrices == null) return;

            float dt = Time.deltaTime;
            Vector3 destPos = _suckDestination != null ? _suckDestination.position : Vector3.zero;

            for (int t = 0; t < _batchCounts.Length; t++) _batchCounts[t] = 0;

            bool showBeacon = manager != null && manager.HasBeaconDetect &&
                              manager.Phase == GamePhase.Phase1_TrashRemoval &&
                              RemainingCount > 0 && (RemainingCount <= 40 || manager.Phase1Percent >= 90f);
            int beaconCount = 0;

            for (int i = 0; i < _unitCount; i++)
            {
                byte st = _units[i].state;
                if (st == 3) continue; // Cleaned

                if (st == 1) // Popping fountain
                {
                    _units[i].timer += dt;
                    float p = Mathf.Clamp01(_units[i].timer / 0.45f);
                    Vector3 cur = Vector3.Lerp(_units[i].startPos, _units[i].endPos, p);
                    cur.y += Mathf.Sin(p * Mathf.PI) * 0.65f;
                    _units[i].pos = cur;
                    if (p >= 1f)
                    {
                        _units[i].pos = _units[i].endPos;
                        _units[i].state = 0;
                    }
                }
                else if (st == 2) // Sucking into hand bag / vacuum nozzle!
                {
                    _units[i].pos = Vector3.MoveTowards(_units[i].pos, destPos, 13.5f * dt);
                    _units[i].rot = _units[i].rot * Quaternion.Euler(0f, 520f * dt, 260f * dt);
                    _units[i].curScale = Mathf.MoveTowards(_units[i].curScale, 0f, _units[i].baseScale * 3.8f * dt);

                    if ((_units[i].pos - destPos).sqrMagnitude < 0.04f || _units[i].curScale <= 0.002f)
                    {
                        _units[i].state = 3;
                        _cleanedCount++;
                        if (manager != null) manager.OnInstancedTrashCollected(_units[i].reward);
                        continue;
                    }
                }
                else if (showBeacon && beaconCount < _beaconMatrices.Length && (i % Mathf.Max(1, RemainingCount / 16) == 0))
                {
                    _beaconMatrices[beaconCount++] = Matrix4x4.TRS(
                        _units[i].pos + Vector3.up * 0.9f,
                        Quaternion.identity,
                        new Vector3(0.05f, 0.9f, 0.05f)
                    );
                }

                // Append to GPU Instancing batch
                int tIdx = _units[i].typeIdx;
                int c = _batchCounts[tIdx];
                if (c < 1023)
                {
                    _batchMatrices[tIdx][c] = Matrix4x4.TRS(_units[i].pos, _units[i].rot, Vector3.one * _units[i].curScale);
                    _batchCounts[tIdx] = c + 1;
                }
            }

            // Issue ONLY 1 Draw Call per trash mesh type! (1,000+ objects = ~4~5 draw calls total!)
            for (int t = 0; t < meshTypes.Count; t++)
            {
                int cnt = _batchCounts[t];
                if (cnt > 0 && meshTypes[t].mesh != null && meshTypes[t].material != null)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], meshTypes[t].mesh, 0, _batchMatrices[t], cnt);
                }
            }

            if (beaconCount > 0 && _beaconMesh != null && _beaconMat != null)
            {
                var bp = new RenderParams(_beaconMat)
                {
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 50f)
                };
                Graphics.RenderMeshInstanced(bp, _beaconMesh, 0, _beaconMatrices, beaconCount);
            }
        }
    }
}
