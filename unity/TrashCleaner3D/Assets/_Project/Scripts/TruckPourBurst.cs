using System.Collections.Generic;
using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 트럭에 봉투가 들어갈 때 '와르르' 쏟아져 들어가는 다발 연출.
    /// 가벼운 큐브 다발이 포물선을 그리며 짐칸(goal)으로 빨려 들어가며 작아져 사라진다.
    /// Physics/오브젝트 풀 없이 1회성으로 최대 18개만 만들어 쓰고 스스로 파괴한다.
    /// </summary>
    public class TruckPourBurst : MonoBehaviour
    {
        private struct PourPiece
        {
            public Transform tr;
            public Vector3 from;
            public float delay;
            public float dur;
            public float spin;
            public Vector3 baseScale;
        }

        private static Material _cachedMat;

        private readonly List<PourPiece> _pieces = new List<PourPiece>();
        private Transform _goal;
        private float _age;
        private float _life = 1.4f;

        /// <summary>
        /// origin에서 goal 방향으로 count/2개(6~26개)의 다발을 뿌린다. goal이 없으면 아무것도 하지 않는다.
        /// </summary>
        public static void Spawn(Vector3 origin, Transform goal, int count, Color tint)
        {
            if (goal == null) return;

            GameObject root = new GameObject("TruckPourBurst");
            root.transform.position = origin;
            var fx = root.AddComponent<TruckPourBurst>();
            // v1.3 (요청 #4·#12): 수거통을 통째로 비우는 정산이라 다발도 더 많이 뿌린다.
            fx.Build(origin, goal, Mathf.Clamp(count / 2, 6, 26), tint);
        }

        private void Build(Vector3 origin, Transform goal, int count, Color tint)
        {
            _goal = goal;
            Material mat = GetPourMaterial(tint);

            float lastEnd = 0f;
            for (int i = 0; i < count; i++)
            {
                GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                piece.name = $"PourPiece_{i + 1:00}";
                piece.transform.SetParent(transform, false);

                float s = Random.Range(0.09f, 0.18f);
                Vector3 sc = new Vector3(s, s * Random.Range(0.85f, 1.45f), s * Random.Range(0.80f, 1.20f));
                piece.transform.localScale = sc;

                Renderer r = piece.GetComponent<Renderer>();
                if (r != null)
                {
                    if (mat != null) r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Collider col = piece.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Vector3 from = origin + Random.insideUnitSphere * 0.32f;
                piece.transform.position = from;
                piece.transform.rotation = Random.rotation;

                float delay = i * 0.032f;
                float dur = Random.Range(0.32f, 0.48f);
                if (delay + dur > lastEnd) lastEnd = delay + dur;

                _pieces.Add(new PourPiece
                {
                    tr = piece.transform,
                    from = from,
                    delay = delay,
                    dur = dur,
                    spin = Random.Range(420f, 980f),
                    baseScale = sc
                });
            }

            _life = lastEnd + 0.18f;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Vector3 dest = _goal != null ? _goal.position : transform.position;

            for (int i = 0; i < _pieces.Count; i++)
            {
                PourPiece p = _pieces[i];
                if (p.tr == null) continue;

                float t = Mathf.Clamp01((_age - p.delay) / Mathf.Max(0.05f, p.dur));
                if (t <= 0f)
                {
                    p.tr.position = p.from;
                    continue;
                }

                // 위로 살짝 솟았다가 짐칸으로 떨어지는 포물선 (베지에)
                Vector3 ctrl = Vector3.Lerp(p.from, dest, 0.5f) + Vector3.up * (0.9f + (i % 5) * 0.10f);
                Vector3 a = Vector3.Lerp(p.from, ctrl, t);
                Vector3 b = Vector3.Lerp(ctrl, dest, t);
                p.tr.position = Vector3.Lerp(a, b, t);
                p.tr.rotation = p.tr.rotation * Quaternion.Euler(0f, p.spin * Time.deltaTime, p.spin * 0.6f * Time.deltaTime);

                float shrink = Mathf.Clamp01(1f - Mathf.Pow(t, 2.4f));
                p.tr.localScale = p.baseScale * shrink;

                if (t >= 1f) p.tr.localScale = Vector3.zero;
            }

            if (_age >= _life) Destroy(gameObject);
        }

        private static Material GetPourMaterial(Color tint)
        {
            if (_cachedMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Standard");
                if (sh == null) return null;

                _cachedMat = new Material(sh);
                if (_cachedMat.HasProperty("_BaseColor")) _cachedMat.SetColor("_BaseColor", tint);
                if (_cachedMat.HasProperty("_Color")) _cachedMat.SetColor("_Color", tint);
                if (_cachedMat.HasProperty("_Smoothness")) _cachedMat.SetFloat("_Smoothness", 0.28f);
            }
            return _cachedMat;
        }
    }
}
