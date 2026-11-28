using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.4 GPU-Instanced 트럭 낱개 투입 & 적재 시스템:
    /// - 쓰레기 봉지 대신 실제 3D 쓰레기 메쉬(캔·담뱃갑·통조림·물통·종이)가
    ///   플레이어 허리 아래 오른손 위치에서 낮은 포물선으로 날아가(시야 가림 0%!)
    ///   수거함/리어카/다마스/1톤 트럭 적재함 바닥부터 실제 쓰레기 산으로 차곡차곡 쌓인다!
    /// - 50개 들고 있으면 정확히 50개 1:1 투입 (GPU Instancing으로 수백 개도 드로우콜 5개 미만·렉 0%!)
    /// </summary>
    public class InstancedTruckTrash : MonoBehaviour
    {
        private struct FlyingPiece
        {
            public int typeIdx;
            public Vector3 from;
            public Vector3 ctrl;
            public Vector3 landWorld;
            public Vector3 landLocal;
            public Quaternion rot;
            public float scale;
            public float delay;
            public float dur;
            public float spin;
            public bool landed;
        }

        private struct StackedPiece
        {
            public int typeIdx;
            public Vector3 localPos;
            public Quaternion localRot;
            public float scale;
        }

        private const int MaxStackedVisuals = 600;

        private InstancedTrashSwarm _swarm;
        private Transform _stackRoot;
        private readonly List<FlyingPiece> _flying = new List<FlyingPiece>(256);
        private readonly List<StackedPiece> _stacked = new List<StackedPiece>(MaxStackedVisuals);

        private List<Matrix4x4[]>[] _typeChunks;
        private int[] _typeCounts;
        private RenderParams[] _renderParams;
        private int _meshTypeCount;

        public void Init(InstancedTrashSwarm swarm, Transform stackRoot)
        {
            _swarm = swarm;
            _stackRoot = stackRoot;
            EnsureBuffers();
        }

        private void EnsureBuffers()
        {
            if (_swarm == null || _swarm.meshTypes == null || _swarm.meshTypes.Count == 0) return;
            if (_typeChunks != null && _meshTypeCount == _swarm.meshTypes.Count) return;

            _meshTypeCount = _swarm.meshTypes.Count;
            _typeChunks = new List<Matrix4x4[]>[_meshTypeCount];
            _typeCounts = new int[_meshTypeCount];
            _renderParams = new RenderParams[_meshTypeCount];

            for (int t = 0; t < _meshTypeCount; t++)
            {
                _typeChunks[t] = new List<Matrix4x4[]> { new Matrix4x4[1023] };
                Material mat = _swarm.meshTypes[t].material;
                if (mat != null)
                {
                    mat.enableInstancing = true;
                    _renderParams[t] = new RenderParams(mat)
                    {
                        worldBounds = new Bounds(Vector3.zero, Vector3.one * 80f),
                        shadowCastingMode = ShadowCastingMode.Off,
                        receiveShadows = true
                    };
                }
            }
        }

        /// <summary>
        /// 보유한 exactCount 개수 그대로(50개면 50개!) 허리 아래에서 트럭 적재함으로 낮은 호를 그리며 쏟아 적재한다.
        /// (극단적 후반 300개 초과 시에도 1.4초 안에 시원하게 끝나도록 간격 자동 압축)
        /// </summary>
        public void PourExactCount(Vector3 lowOrigin, Transform goal, Vector3 halfExtents, int exactCount, int vehicleStage)
        {
            EnsureBuffers();
            if (_meshTypeCount == 0 || goal == null || exactCount <= 0) return;

            int spawnCount = Mathf.Min(exactCount, 450); // 1~450개 100% 1:1 (GPU Instancing 5드로우콜)
            float totalSpread = Mathf.Clamp(spawnCount * 0.016f, 0.25f, 1.15f);
            float stepDelay = spawnCount > 1 ? totalSpread / spawnCount : 0f;

            Vector3 stackOrigin = _stackRoot != null ? _stackRoot.position : (goal.position - Vector3.up * 0.42f);
            float fillW = Mathf.Max(0.24f, halfExtents.x * 0.78f);
            float fillD = Mathf.Max(0.24f, halfExtents.z * 0.78f);

            int baseStackIndex = _stacked.Count + _flying.Count;

            for (int i = 0; i < spawnCount; i++)
            {
                int tIdx = Random.Range(0, _meshTypeCount);
                float baseScale = _swarm.meshTypes[tIdx].normalizedScale * Random.Range(0.82f, 1.12f);

                // 적재함 바닥부터 층층이 자연스럽게 쌓이는 로컬 좌표 계산
                int slotIdx = baseStackIndex + i;
                int perLayer = vehicleStage == 0 ? 14 : vehicleStage == 1 ? 32 : vehicleStage == 2 ? 56 : 90;
                int layer = slotIdx / perLayer;
                float layerY = Mathf.Min(1.35f, 0.04f + layer * 0.095f + Random.Range(0f, 0.04f));

                Vector3 landLocal = new Vector3(
                    Random.Range(-fillW, fillW),
                    layerY,
                    Random.Range(-fillD, fillD)
                );
                Vector3 landWorld = stackOrigin + landLocal;

                // 허리 아래 우측에서 출발 + 낮은 제어점(시야 절대 안 가림!)
                Vector3 from = lowOrigin + new Vector3(Random.Range(-0.14f, 0.14f), Random.Range(-0.08f, 0.06f), Random.Range(-0.10f, 0.10f));
                Vector3 mid = Vector3.Lerp(from, landWorld, 0.52f);
                // 낮은 아치: 출발점과 목표점 중 높은 쪽에서 +0.28~0.48m만 살짝 솟음 (카메라 눈높이 아래 유지!)
                float arcY = Mathf.Max(from.y, landWorld.y) + Random.Range(0.24f, 0.46f);
                Vector3 ctrl = new Vector3(mid.x + Random.Range(-0.16f, 0.16f), arcY, mid.z + Random.Range(-0.12f, 0.12f));

                Quaternion baseOff = Quaternion.Euler(_swarm.meshTypes[tIdx].baseEulerOffset);
                Quaternion rot = Quaternion.Euler(Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f)) * baseOff;

                _flying.Add(new FlyingPiece
                {
                    typeIdx = tIdx,
                    from = from,
                    ctrl = ctrl,
                    landWorld = landWorld,
                    landLocal = landLocal,
                    rot = rot,
                    scale = baseScale,
                    delay = i * stepDelay,
                    dur = Random.Range(0.26f, 0.40f),
                    spin = Random.Range(320f, 720f),
                    landed = false
                });
            }
        }

        public void ClearStackedVisuals()
        {
            _flying.Clear();
            _stacked.Clear();
        }

        private void PushMatrix(int tIdx, Matrix4x4 mtx)
        {
            int idx = _typeCounts[tIdx]++;
            int chunkIdx = idx / 1023;
            int localIdx = idx % 1023;
            while (_typeChunks[tIdx].Count <= chunkIdx)
            {
                _typeChunks[tIdx].Add(new Matrix4x4[1023]);
            }
            _typeChunks[tIdx][chunkIdx][localIdx] = mtx;
        }

        private void Update()
        {
            if (_meshTypeCount == 0 || _typeChunks == null) return;
            if (_flying.Count == 0 && _stacked.Count == 0) return;

            float dt = Time.deltaTime;
            for (int t = 0; t < _meshTypeCount; t++) _typeCounts[t] = 0;

            Vector3 stackOrigin = _stackRoot != null ? _stackRoot.position : Vector3.zero;
            Quaternion stackRot = _stackRoot != null ? _stackRoot.rotation : Quaternion.identity;

            // 1) 날아가는 중인 낱개 쓰레기 업데이트 & 렌더
            for (int i = _flying.Count - 1; i >= 0; i--)
            {
                FlyingPiece fp = _flying[i];
                if (fp.delay > 0f)
                {
                    fp.delay -= dt;
                    _flying[i] = fp;
                    continue;
                }

                // delay를 음수로 누적해 경과 시간으로 재활용
                float elapsed = -fp.delay + dt;
                fp.delay = -elapsed;
                float u = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, fp.dur));

                Vector3 a = Vector3.Lerp(fp.from, fp.ctrl, u);
                Vector3 b = Vector3.Lerp(fp.ctrl, fp.landWorld, u);
                Vector3 curPos = Vector3.Lerp(a, b, u);
                fp.rot = fp.rot * Quaternion.Euler(0f, fp.spin * dt, fp.spin * 0.45f * dt);

                if (u >= 1f)
                {
                    // [v2.2] 쓰레기통 안으로 들어가는 모션만 남기고 쌓임 제거 (밖으로 삐져나옴 원천 차단!)
                    _flying.RemoveAt(i);
                }
                else
                {
                    _flying[i] = fp;
                    PushMatrix(fp.typeIdx, Matrix4x4.TRS(curPos, fp.rot, Vector3.one * fp.scale));
                }
            }

            // 2) 적재함에 쌓인 낱개 쓰레기 산 렌더
            for (int i = 0; i < _stacked.Count; i++)
            {
                StackedPiece sp = _stacked[i];
                Vector3 worldPos = stackOrigin + stackRot * sp.localPos;
                PushMatrix(sp.typeIdx, Matrix4x4.TRS(worldPos, stackRot * sp.localRot, Vector3.one * sp.scale));
            }

            // 3) GPU Instanced 렌더 (드로우콜 5개 미만!)
            for (int t = 0; t < _meshTypeCount; t++)
            {
                int total = _typeCounts[t];
                if (total <= 0 || _swarm.meshTypes[t].mesh == null || _swarm.meshTypes[t].material == null) continue;
                int fullChunks = total / 1023;
                int rem = total % 1023;
                for (int c = 0; c < fullChunks; c++)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], _swarm.meshTypes[t].mesh, 0, _typeChunks[t][c], 1023);
                }
                if (rem > 0)
                {
                    Graphics.RenderMeshInstanced(_renderParams[t], _swarm.meshTypes[t].mesh, 0, _typeChunks[t][fullChunks], rem);
                }
            }
        }
    }
}
