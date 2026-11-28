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
        /// <summary>
        /// [v2.2] Poppy Playtime 그랩팩(GrabPack) 스타일 기계식 집게손 조형 (로컬 +Z = 손끝 방향).
        /// 파란 대형 그랩 손바닥 + 노란 손목 커프 + 벌려진 집게형 5손가락 + 중앙 검정 그립 패드!
        /// </summary>
        public static void BuildFist(Transform parent, Material gloveMat, Material cuffMat, Material skinMat)
        {
            if (parent == null) return;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material grabBlue = new Material(lit);
            Color blueCol = new Color(0.14f, 0.44f, 0.92f);
            if (grabBlue.HasProperty("_BaseColor")) grabBlue.SetColor("_BaseColor", blueCol);
            grabBlue.color = blueCol;
            if (grabBlue.HasProperty("_Smoothness")) grabBlue.SetFloat("_Smoothness", 0.48f);

            Material padDark = new Material(lit);
            Color darkCol = new Color(0.14f, 0.16f, 0.20f);
            if (padDark.HasProperty("_BaseColor")) padDark.SetColor("_BaseColor", darkCol);
            padDark.color = darkCol;
            if (padDark.HasProperty("_Smoothness")) padDark.SetFloat("_Smoothness", 0.25f);

            Material cuffYellow = cuffMat != null ? cuffMat : grabBlue;

            // [v2.3] 발사통(노란 캐논)은 카메라 하단에 고정 분리! 여기서는 쉭! 발사되는 '그랩팩 손바닥 + 5손가락'만 조형한다.
            // ── 넓적한 그랩팩 기계 손바닥 (Poppy Playtime 특유의 크고 넓적한 실루엣)
            AddSphere(parent, grabBlue, "Palm_Back",
                new Vector3(0f, 0.005f, 0.045f),
                new Vector3(0.185f, 0.072f, 0.175f));

            // ── 손바닥 안쪽 검정 그립 패드
            AddSphere(parent, padDark, "Palm_GripPad",
                new Vector3(0f, -0.022f, 0.048f),
                new Vector3(0.115f, 0.032f, 0.110f));

            // ── 뒷면 와이어 연결 소켓(작은 검정 링)
            AddCylinder(parent, padDark, "WireAnchor",
                new Vector3(0f, 0f, -0.048f),
                new Vector3(0f, 0f, -0.015f),
                0.032f);

            // ── 집게형 4손가락: 앞으로 길게 뻗어 살짝 아래로 오므려 집는 실루엣!
            float[] fingerX = { -0.068f, -0.022f, 0.024f, 0.068f };
            float[] fingerLen = { 0.94f, 1.04f, 0.98f, 0.84f };
            for (int i = 0; i < fingerX.Length; i++)
            {
                float x = fingerX[i];
                float len = fingerLen[i];

                // 관절 링
                AddSphere(parent, padDark, $"Knuckle_{i + 1}",
                    new Vector3(x, 0.015f, 0.140f),
                    new Vector3(0.038f, 0.038f, 0.038f));

                // 첫마디 (앞으로 시원하게 뻗음)
                Vector3 p0 = new Vector3(x, 0.014f, 0.146f);
                Vector3 p1 = new Vector3(x * 1.05f, 0.012f, 0.146f + 0.095f * len);
                AddCapsule(parent, grabBlue, $"Finger_A_{i + 1}", p0, p1, 0.019f);

                // 끝마디 (집게처럼 아래·안쪽으로 탁 꺾임)
                Vector3 p2 = new Vector3(x * 0.92f, -0.042f, 0.146f + 0.155f * len);
                AddCapsule(parent, grabBlue, $"Finger_B_{i + 1}", p1, p2, 0.017f);

                // 끝 흡착 패드
                AddSphere(parent, padDark, $"FingerTip_{i + 1}", p2, new Vector3(0.034f, 0.028f, 0.034f));
            }

            // ── 마주보는 집게 엄지 (아래·옆에서 앞으로 뻗어 물건을 집는 구조)
            Vector3 t0 = new Vector3(-0.088f, -0.018f, 0.045f);
            Vector3 t1 = new Vector3(-0.118f, -0.022f, 0.120f);
            Vector3 t2 = new Vector3(-0.072f, -0.042f, 0.175f);
            AddCapsule(parent, grabBlue, "Thumb_A", t0, t1, 0.022f);
            AddCapsule(parent, grabBlue, "Thumb_B", t1, t2, 0.019f);
            AddSphere(parent, padDark, "ThumbTip", t2, new Vector3(0.038f, 0.032f, 0.038f));
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
