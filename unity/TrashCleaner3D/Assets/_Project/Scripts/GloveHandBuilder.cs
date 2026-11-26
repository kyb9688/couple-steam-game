using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 1인칭 1티어 '작업 장갑 낀 맨손' 조형 빌더.
    ///
    /// v1.3 (요청 #2): 예전에는 손이 회색 직육면체 하나 + 엄지 상자 하나뿐이라
    /// 카메라 앞에서 '손'으로 읽히지 않았다. 이제 손바닥(둥근 배면) · 손등 헤일로 ·
    /// 손가락 4개(너클 → 첫마디 → 접힌 둘째마디 → 손끝) · 엄지(두 마디) · 소매 커프를
    /// 실제 손 실루엣 순서대로 쌓아 올린다. 전부 기본 프리미티브라
    /// 씬에 그대로 직렬화되고, WebGL 빌드에서도 추가 에셋 없이 동일하게 보인다.
    /// </summary>
    public static class GloveHandBuilder
    {
        /// <summary>장갑 낀 오른손을 parent 아래에 조형한다. (로컬 +Z = 손끝 방향)</summary>
        public static void BuildFist(Transform parent, Material gloveMat, Material cuffMat, Material skinMat)
        {
            if (parent == null) return;

            // ── 소매(팔뚝) : 손목에서 화면 밖(-Z, 아래)으로 빠진다
            AddCapsule(parent, skinMat, "Forearm",
                new Vector3(0.020f, -0.070f, -0.235f),
                new Vector3(0.010f, -0.010f, -0.055f),
                0.052f);

            // ── 주황 커프 밴드 : 손목 경계를 확실히 그어 '장갑'임을 각인
            GameObject cuff = AddCylinder(parent, cuffMat, "GloveCuff",
                new Vector3(0.012f, -0.012f, -0.030f),
                new Vector3(0.012f, 0.014f, -0.062f),
                0.062f);
            if (cuff != null) cuff.transform.localScale = new Vector3(0.120f, 0.038f, 0.120f);

            // ── 손바닥 배면(손등) : 살짝 넓적한 타원체
            AddSphere(parent, gloveMat, "Palm_Back",
                new Vector3(0.000f, 0.010f, 0.055f),
                new Vector3(0.150f, 0.108f, 0.185f));

            // ── 손바닥 아래쪽(손날/엄지 볼록) : 손등 타원 아래를 받쳐 입체감을 준다
            AddSphere(parent, gloveMat, "Palm_Heel",
                new Vector3(-0.010f, -0.028f, 0.030f),
                new Vector3(0.132f, 0.080f, 0.140f));

            // ── 손가락 4개 : 너클 → 첫마디(앞으로) → 접힌 둘째마디(아래로 말림) → 손끝
            float[] fingerX = { -0.062f, -0.021f, 0.020f, 0.060f };
            float[] fingerLen = { 0.92f, 1.00f, 0.96f, 0.84f };   // 검지~새끼 길이 차이
            for (int i = 0; i < fingerX.Length; i++)
            {
                float x = fingerX[i];
                float len = fingerLen[i];

                // 너클(관절 봉우리)
                AddSphere(parent, gloveMat, $"Knuckle_{i + 1}",
                    new Vector3(x, 0.042f, 0.138f),
                    new Vector3(0.044f, 0.040f, 0.048f));

                // 첫마디 : 너클에서 손끝 방향(+Z)으로 뻗고 살짝 내려간다
                Vector3 p0 = new Vector3(x, 0.040f, 0.150f);
                Vector3 p1 = new Vector3(x, 0.014f, 0.150f + 0.070f * len);
                AddCapsule(parent, gloveMat, $"Finger_A_{i + 1}", p0, p1, 0.0195f);

                // 접힌 둘째마디 : 손끝이 손바닥 안쪽(아래·뒤)으로 말려 들어간다 = 주먹 실루엣
                Vector3 p2 = new Vector3(x, -0.044f, 0.176f + 0.048f * len);
                AddCapsule(parent, gloveMat, $"Finger_B_{i + 1}", p1, p2, 0.0185f);

                // 손끝 마디
                AddSphere(parent, gloveMat, $"FingerTip_{i + 1}",
                    p2,
                    new Vector3(0.038f, 0.034f, 0.040f));
            }

            // ── 엄지 : 손날 쪽에서 비스듬히 올라와 손가락 앞을 가로지른다 (두 마디)
            Vector3 t0 = new Vector3(-0.086f, -0.026f, 0.020f);
            Vector3 t1 = new Vector3(-0.062f, 0.006f, 0.090f);
            Vector3 t2 = new Vector3(-0.012f, 0.030f, 0.140f);
            AddCapsule(parent, gloveMat, "Thumb_A", t0, t1, 0.026f);
            AddCapsule(parent, gloveMat, "Thumb_B", t1, t2, 0.023f);
            AddSphere(parent, gloveMat, "ThumbTip", t2, new Vector3(0.044f, 0.040f, 0.044f));

            // ── 손목 장갑 마감(커프 위 얇은 테) : 팔뚝 피부와 장갑 경계를 정리
            AddCylinder(parent, gloveMat, "GloveWrist",
                new Vector3(0.014f, -0.014f, -0.048f),
                new Vector3(0.014f, 0.010f, -0.072f),
                0.056f);
        }

        // ===== 프리미티브 헬퍼 =====

        private static GameObject AddPrimitive(PrimitiveType type, Transform parent, Material mat, string name)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            KillCollider(go);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        private static GameObject AddSphere(Transform parent, Material mat, string name, Vector3 localPos, Vector3 localScale)
        {
            GameObject go = AddPrimitive(PrimitiveType.Sphere, parent, mat, name);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            return go;
        }

        /// <summary>from → to 를 잇는 캡슐 마디. (캡슐의 긴 축은 로컬 Y)</summary>
        private static GameObject AddCapsule(Transform parent, Material mat, string name, Vector3 from, Vector3 to, float radius)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.0001f) len = 0.0001f;

            GameObject go = AddPrimitive(PrimitiveType.Capsule, parent, mat, name);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            go.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            return go;
        }

        /// <summary>from → to 를 잇는 원기둥(커프·관절 밴드).</summary>
        private static GameObject AddCylinder(Transform parent, Material mat, string name, Vector3 from, Vector3 to, float radius)
        {
            Vector3 dir = to - from;
            float len = dir.magnitude;
            if (len < 0.0001f) len = 0.0001f;

            GameObject go = AddPrimitive(PrimitiveType.Cylinder, parent, mat, name);
            go.transform.localPosition = (from + to) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            go.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            return go;
        }

        private static void KillCollider(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col == null) return;
            if (Application.isPlaying) Object.Destroy(col);
            else Object.DestroyImmediate(col);
        }
    }
}
