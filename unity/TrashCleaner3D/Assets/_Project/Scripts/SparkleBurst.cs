using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// 닦인 바닥 위로 톡 터지는 반짝이 (v1.1.2 디자인).
    /// 프리팹·이미지 에셋 없이 작은 사각 메쉬와 가산(Additive) 머티리얼만으로 만들고,
    /// 수명이 끝나면 스스로 사라진다. 걸레로 얼룩을 닦아낸 순간의 시각 보상용.
    /// </summary>
    public class SparkleBurst : MonoBehaviour
    {
        private const int MaxParticles = 24;
        private const float Gravity = 1.35f;

        private struct Bit
        {
            public Vector3 pos;
            public Vector3 vel;
            public float age;
            public float life;
            public float size;
            public float spin;
        }

        private static Mesh _quadMesh;
        private static Material _sparkleMat;

        private readonly Bit[] _bits = new Bit[MaxParticles];
        private int _count;
        private Camera _cam;

        public static void Spawn(Vector3 origin, int count = 14, float radius = 0.55f, Color? tint = null)
        {
            GameObject go = new GameObject("SparkleBurst");
            go.transform.position = origin;
            SparkleBurst burst = go.AddComponent<SparkleBurst>();
            burst.Init(count, radius, tint ?? new Color(1f, 0.965f, 0.80f));
        }

        private void Init(int count, float radius, Color tint)
        {
            EnsureAssets(tint);
            _cam = Camera.main;
            _count = Mathf.Clamp(count, 1, MaxParticles);

            for (int i = 0; i < _count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float side = Random.Range(0.30f, 1.15f);
                _bits[i] = new Bit
                {
                    pos = transform.position + new Vector3(
                        Random.Range(-radius, radius) * 0.55f,
                        Random.Range(0.015f, 0.10f),
                        Random.Range(-radius, radius) * 0.55f),
                    vel = new Vector3(Mathf.Cos(ang) * side, Random.Range(0.55f, 1.30f), Mathf.Sin(ang) * side),
                    age = 0f,
                    life = Random.Range(0.34f, 0.66f),
                    size = Random.Range(0.016f, 0.042f),
                    spin = Random.Range(0f, 180f)
                };
            }
        }

        private static void EnsureAssets(Color tint)
        {
            if (_quadMesh == null) _quadMesh = BuildQuadMesh();

            if (_sparkleMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
                if (sh == null) return;
                _sparkleMat = new Material(sh);
                _sparkleMat.name = "Mat_Sparkle_Additive";
                // 불투명 표면을 가산 블렌딩 반투명으로 전환 (URP Unlit 기준, 다른 셰이더에서는 기본값 사용)
                if (_sparkleMat.HasProperty("_Surface")) _sparkleMat.SetFloat("_Surface", 1f);
                if (_sparkleMat.HasProperty("_SrcBlend")) _sparkleMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                if (_sparkleMat.HasProperty("_DstBlend")) _sparkleMat.SetFloat("_DstBlend", (float)BlendMode.One);
                if (_sparkleMat.HasProperty("_ZWrite")) _sparkleMat.SetFloat("_ZWrite", 0f);
                if (_sparkleMat.HasProperty("_Cull")) _sparkleMat.SetFloat("_Cull", (float)CullMode.Off);
                _sparkleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _sparkleMat.renderQueue = (int)RenderQueue.Transparent + 20;
            }

            if (_sparkleMat != null)
            {
                if (_sparkleMat.HasProperty("_BaseColor")) _sparkleMat.SetColor("_BaseColor", tint);
                _sparkleMat.color = tint;
            }
        }

        private static Mesh BuildQuadMesh()
        {
            Mesh m = new Mesh();
            m.name = "Mesh_SparkleQuad";
            m.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(-1f, 1f, 0f)
            };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private void Update()
        {
            if (_quadMesh == null || _sparkleMat == null)
            {
                Destroy(gameObject);
                return;
            }

            if (_cam == null) _cam = Camera.main;
            float dt = Time.deltaTime;
            float now = Time.time;
            bool anyAlive = false;

            Vector3 camPos = _cam != null ? _cam.transform.position : transform.position - Vector3.forward * 5f;

            for (int i = 0; i < _count; i++)
            {
                if (_bits[i].age >= _bits[i].life) continue;

                _bits[i].age += dt;
                if (_bits[i].age >= _bits[i].life) continue;
                anyAlive = true;

                _bits[i].vel += Vector3.down * (Gravity * dt);
                _bits[i].pos += _bits[i].vel * dt;

                float lifeP = 1f - _bits[i].age / _bits[i].life;
                float twinkle = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_bits[i].age * 16f + _bits[i].spin));
                float s = _bits[i].size * (0.35f + 0.75f * lifeP) * (0.55f + 0.65f * twinkle);

                Quaternion face = Quaternion.LookRotation(_bits[i].pos - camPos);
                Quaternion rot = face * Quaternion.Euler(0f, 0f, _bits[i].spin + now * 120f);
                Matrix4x4 mtx = Matrix4x4.TRS(_bits[i].pos, rot, new Vector3(s, s * 1.8f, s));
                Graphics.DrawMesh(_quadMesh, mtx, _sparkleMat, 0);
            }

            if (!anyAlive) Destroy(gameObject);
        }
    }
}
