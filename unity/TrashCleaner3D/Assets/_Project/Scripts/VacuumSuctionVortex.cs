using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.2 진공청소기 흡입 소용돌이 연출:
    /// 노즐 입구에서 바닥 쪽으로 퍼지는 반투명 원뿔 3단 + 서로 반대로 도는 나선 링 2개를 그린다.
    /// 파티클/프리팹 에셋 없이 절차 메쉬와 Graphics.DrawMesh(가산 블렌딩)만 쓰고,
    /// 흡입 중일 때만 서서히 나타났다가 LMB를 떼면 서서히 사라진다.
    /// </summary>
    public class VacuumSuctionVortex : MonoBehaviour
    {
        private struct ConeBand
        {
            public Mesh mesh;
            public Material mat;
            public Vector3 offset;
            public Color color;
            public float alpha;
        }

        private struct SwirlRing
        {
            public Mesh mesh;
            public Material mat;
            public Color color;
            public float alpha;
        }

        private ConeBand[] _cones;
        private SwirlRing[] _rings;
        private Mesh _ringMesh;
        private float _intensity;
        private float _target;

        /// <summary>진공 노즐의 자식으로 소용돌이를 만든다. (노즐 로컬 +Z = 흡입 방향)</summary>
        public static VacuumSuctionVortex Attach(Transform parent, Vector3 localPos, Quaternion localRot)
        {
            if (parent == null) return null;
            GameObject go = new GameObject("FX_VacuumSuctionVortex");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var vortex = go.AddComponent<VacuumSuctionVortex>();
            vortex.Build();
            return vortex;
        }

        /// <summary>0 = 완전히 사라짐, 1 = 최대로 흡입 중 (컨트롤러가 매 프레임 호출)</summary>
        public void SetIntensity(float value)
        {
            _target = Mathf.Clamp01(value);
        }

        private void Build()
        {
            // 1) 원뿔 3단: 노즐 입구(좁고 진함) → 앞쪽(넓고 옅음)
            _cones = new ConeBand[3];
            Color coneCol = new Color(0.66f, 0.95f, 1.0f);
            _cones[0] = MakeCone(BuildConeSegment(0.02f, 0.035f, 0.20f, 0.105f, 20), Vector3.zero, coneCol, 0.20f);
            _cones[1] = MakeCone(BuildConeSegment(0.16f, 0.090f, 0.40f, 0.215f, 24), Vector3.zero, coneCol, 0.13f);
            _cones[2] = MakeCone(BuildConeSegment(0.34f, 0.190f, 0.62f, 0.350f, 28), Vector3.zero, coneCol, 0.075f);

            // 2) 나선 링: 소용돌이가 앞으로 흘러가며 커지는 느낌
            _ringMesh = BuildRingMesh(0.86f, 1.0f, 40);
            _rings = new SwirlRing[2];
            _rings[0] = MakeRing(new Color(0.70f, 0.98f, 1.0f), 0.40f);
            _rings[1] = MakeRing(new Color(0.52f, 0.88f, 0.82f), 0.30f);
        }

        private ConeBand MakeCone(Mesh mesh, Vector3 offset, Color color, float alpha)
        {
            return new ConeBand { mesh = mesh, mat = MakeVortexMaterial(), offset = offset, color = color, alpha = alpha };
        }

        private SwirlRing MakeRing(Color color, float alpha)
        {
            return new SwirlRing { mesh = _ringMesh, mat = MakeVortexMaterial(), color = color, alpha = alpha };
        }

        private static Material MakeVortexMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (sh == null) return null;

            Material mat = new Material(sh);
            mat.name = "Mat_VacuumVortex";
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);   // 가산(더하기) 블렌딩
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);            // 안쪽 면도 보이게
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent + 25;
            return mat;
        }

        private void Update()
        {
            // 켜질 때는 빠르게, 꺼질 때는 부드럽게
            float speed = _target > _intensity ? 10f : 5f;
            _intensity = Mathf.MoveTowards(_intensity, _target, speed * Time.deltaTime);

            if (_intensity <= 0.004f || _cones == null) return;

            float t = Time.time;
            Matrix4x4 root = transform.localToWorldMatrix;

            // 1) 원뿔: 서로 반대 방향으로 천천히 돌며 미세하게 맥동
            for (int i = 0; i < _cones.Length; i++)
            {
                ConeBand band = _cones[i];
                if (band.mesh == null || band.mat == null) continue;

                float spin = t * (i % 2 == 0 ? 150f : -120f) + i * 55f;
                float pulse = 0.97f + 0.03f * Mathf.Sin(t * 9f + i * 1.7f);
                Matrix4x4 m = root * Matrix4x4.TRS(band.offset, Quaternion.Euler(0f, 0f, spin), Vector3.one * pulse);
                ApplyColor(band.mat, band.color, band.alpha * _intensity);
                Graphics.DrawMesh(band.mesh, m, band.mat, 0);
            }

            // 2) 나선 링: 앞으로 흘러가며 커지고 옅어짐 (끝에서 자연스럽게 사라짐)
            for (int i = 0; i < _rings.Length; i++)
            {
                SwirlRing ring = _rings[i];
                if (ring.mesh == null || ring.mat == null) continue;

                float phase = Mathf.Repeat(t * 0.90f + i * 0.5f, 1f);
                float z = Mathf.Lerp(0.10f, 0.60f, phase);
                float scale = Mathf.Lerp(0.42f, 1.15f, phase);
                float fade = Mathf.Sin(phase * Mathf.PI);          // 시작·끝에서 0
                float a = ring.alpha * fade * _intensity;
                if (a <= 0.004f) continue;

                Matrix4x4 m = root * Matrix4x4.TRS(
                    new Vector3(0f, 0f, z),
                    Quaternion.Euler(0f, 0f, t * (i == 0 ? 430f : -380f)),
                    new Vector3(scale, scale, 1f));
                ApplyColor(ring.mat, ring.color, a);
                Graphics.DrawMesh(ring.mesh, m, ring.mat, 0);
            }
        }

        private static void ApplyColor(Material mat, Color color, float alpha)
        {
            Color c = color;
            c.a = Mathf.Clamp01(alpha);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            mat.color = c;
        }

        private void OnDestroy()
        {
            if (_cones != null)
            {
                for (int i = 0; i < _cones.Length; i++)
                {
                    if (_cones[i].mesh != null) Destroy(_cones[i].mesh);
                    if (_cones[i].mat != null) Destroy(_cones[i].mat);
                }
            }
            if (_rings != null)
            {
                for (int i = 0; i < _rings.Length; i++)
                {
                    if (_rings[i].mat != null) Destroy(_rings[i].mat);
                }
            }
            if (_ringMesh != null) Destroy(_ringMesh);
        }

        /// <summary>+Z 축으로 벌어지는 원뿔대(열린 원뿔) 메쉬.</summary>
        private static Mesh BuildConeSegment(float z0, float r0, float z1, float r1, int seg)
        {
            var verts = new List<Vector3>((seg + 1) * 2);
            var tris = new List<int>(seg * 6);
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts.Add(new Vector3(c * r0, s * r0, z0));
                verts.Add(new Vector3(c * r1, s * r1, z1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = (i + 1) * 2, b1 = (i + 1) * 2 + 1;
                tris.Add(a0); tris.Add(a1); tris.Add(b1);
                tris.Add(a0); tris.Add(b1); tris.Add(b0);
            }
            Mesh m = new Mesh();
            m.name = "Mesh_VortexCone";
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>XY 평면(법선 +Z)에 놓인 얇은 고리 메쉬 — 원뿔 축을 따라 돌려 쓴다.</summary>
        private static Mesh BuildRingMesh(float innerR, float outerR, int seg)
        {
            var verts = new Vector3[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                verts[i * 2] = new Vector3(c * innerR, s * innerR, 0f);
                verts[i * 2 + 1] = new Vector3(c * outerR, s * outerR, 0f);

                int ni = (i + 1) % seg;
                int t = i * 6;
                tris[t] = i * 2; tris[t + 1] = i * 2 + 1; tris[t + 2] = ni * 2 + 1;
                tris[t + 3] = i * 2; tris[t + 4] = ni * 2 + 1; tris[t + 5] = ni * 2;
            }
            Mesh m = new Mesh();
            m.name = "Mesh_VortexRing";
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
