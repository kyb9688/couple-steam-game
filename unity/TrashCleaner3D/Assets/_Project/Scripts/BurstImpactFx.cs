using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.6 큰 봉투가 터지는 순간의 '팡!' 충격 링.
    /// 바닥에 밀착된 링이 빠르게 퍼지며 사라지고, 살짝 기울어진 두 번째 링이 위로 튀어 오른다.
    /// 프리팹/텍스처 없이 링 메쉬 + 가산 머티리얼만 사용하고 수명이 끝나면 스스로 제거된다.
    /// </summary>
    public class BurstImpactFx : MonoBehaviour
    {
        private static Mesh _ringMesh;
        private static Material _mat;

        private const float Duration = 0.34f;
        private float _age;
        private Color _color;
        private float _radius;
        private Camera _cam;

        public static void Spawn(Vector3 worldPos, Color color, float radius = 2.0f)
        {
            GameObject go = new GameObject("BurstImpactFx");
            go.transform.position = worldPos;
            BurstImpactFx fx = go.AddComponent<BurstImpactFx>();
            fx._color = color;
            fx._radius = Mathf.Max(0.6f, radius);
            fx._cam = Camera.main;
            fx.EnsureAssets();
        }

        private void EnsureAssets()
        {
            if (_ringMesh != null && _mat != null) return;
            if (_ringMesh == null)
            {
                int seg = 40;
                Mesh m = new Mesh();
                Vector3[] verts = new Vector3[seg * 2];
                int[] tris = new int[seg * 6];
                for (int i = 0; i < seg; i++)
                {
                    float a = (i / (float)seg) * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    verts[i * 2] = new Vector3(c * 0.72f, 0f, s * 0.72f);
                    verts[i * 2 + 1] = new Vector3(c, 0f, s);
                    int ni = (i + 1) % seg;
                    int t = i * 6;
                    tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                    tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
                }
                m.vertices = verts;
                m.triangles = tris;
                m.RecalculateNormals();
                m.RecalculateBounds();
                _ringMesh = m;
            }
            if (_mat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (sh == null) return;
                _mat = new Material(sh);
                _mat.name = "Mat_BurstImpactRing";
                if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 1f);
                if (_mat.HasProperty("_SrcBlend")) _mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (_mat.HasProperty("_DstBlend")) _mat.SetFloat("_DstBlend", (float)BlendMode.One);
                if (_mat.HasProperty("_ZWrite")) _mat.SetFloat("_ZWrite", 0f);
                if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", (float)CullMode.Off);
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _mat.renderQueue = (int)RenderQueue.Transparent + 25;
            }
        }

        private void Update()
        {
            if (_ringMesh == null || _mat == null) { Destroy(gameObject); return; }

            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Duration);
            float ease = 1f - (1f - t) * (1f - t);
            float alpha = (1f - t) * (1f - t);

            Color c = _color; c.a = alpha * 0.85f;
            _mat.SetColor("_BaseColor", c);
            _mat.color = c;

            float r = Mathf.Lerp(0.35f, _radius, ease);

            // 바닥에 밀착된 주 링
            Matrix4x4 flat = Matrix4x4.TRS(
                transform.position + Vector3.up * 0.035f,
                Quaternion.identity,
                new Vector3(r, 1f, r));
            Graphics.DrawMesh(_ringMesh, flat, _mat, 0);

            // 살짝 기울어 위로 퍼지는 보조 링 (높이만큼 시야에서도 보인다)
            Matrix4x4 tilt = Matrix4x4.TRS(
                transform.position + Vector3.up * (0.25f + 0.55f * ease),
                Quaternion.Euler(72f, 0f, 0f),
                new Vector3(r * 0.72f, 1f, r * 0.72f));
            Color c2 = c; c2.a = alpha * 0.45f;
            _mat.SetColor("_BaseColor", c2);
            Graphics.DrawMesh(_ringMesh, tilt, _mat, 0);

            if (_age >= Duration) Destroy(gameObject);
        }
    }
}
