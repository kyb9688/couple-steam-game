using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.6 통합 강화소 키오스크 — 트럭 옆 작업대 1대.
    /// 앞에서 [E]를 누르면 56노드 나뭇가지 강화 트리(기존 28 + 마이크로 28)가 열린다.
    /// 트리 창은 마우스 휠 확대/축소 · 드래그 이동 · [R] 초기 위치를 지원한다.
    /// </summary>
    public class FieldUpgradeStation : MonoBehaviour
    {
        public GamePrototypeManager manager;
        public const float InteractRadius = 2.65f;

        private bool _built;
        private Material _screenMat;
        private Material _signMat;
        private Light _lamp;
        private float _pulseTime;
        private float _buyPulse;

        public void Init(GamePrototypeManager mgr, float facingYawDeg = 0f)
        {
            manager = mgr;
            if (_built) return;
            _built = true;
            transform.rotation = Quaternion.Euler(0f, facingYawDeg, 0f);
            BuildVisuals();
        }

        private void BuildVisuals()
        {
            Material baseMat = MakeSimpleMat(new Color(0.20f, 0.22f, 0.26f), 0.35f);
            Material metalMat = MakeSimpleMat(new Color(0.34f, 0.37f, 0.42f), 0.62f);
            Material counterMat = MakeSimpleMat(new Color(0.30f, 0.26f, 0.22f), 0.28f);
            _signMat = MakeEmissiveMat(new Color(0.98f, 0.86f, 0.36f), 1.35f);
            _screenMat = MakeEmissiveMat(new Color(0.36f, 0.88f, 0.78f), 1.05f);

            CreateBox("Kiosk_Base", new Vector3(0f, 0.05f, 0f), new Vector3(2.40f, 0.10f, 1.60f), baseMat, true);
            CreateBox("Kiosk_Counter", new Vector3(0f, 0.50f, 0.30f), new Vector3(2.05f, 0.92f, 0.52f), counterMat, true);
            CreateBox("Kiosk_CounterTop", new Vector3(0f, 0.98f, 0.30f), new Vector3(2.16f, 0.06f, 0.60f), metalMat, true);
            CreateBox("Kiosk_Post_L", new Vector3(-0.98f, 1.02f, -0.62f), new Vector3(0.09f, 2.04f, 0.09f), metalMat, true);
            CreateBox("Kiosk_Post_R", new Vector3(0.98f, 1.02f, -0.62f), new Vector3(0.09f, 2.04f, 0.09f), metalMat, true);
            GameObject sign = CreateBox("Kiosk_Sign", new Vector3(0f, 2.10f, -0.62f), new Vector3(2.20f, 0.42f, 0.08f), _signMat, false);
            sign.name = "Kiosk_Sign";

            GameObject screen = CreateBox("Kiosk_Screen", new Vector3(0f, 1.30f, -0.10f), new Vector3(0.98f, 0.56f, 0.04f), _screenMat, false);
            screen.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            GameObject frame = CreateBox("Kiosk_ScreenFrame", new Vector3(0f, 1.30f, -0.09f), new Vector3(1.08f, 0.66f, 0.03f), baseMat, false);
            frame.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);

            GameObject lampObj = new GameObject("Kiosk_Lamp");
            lampObj.transform.SetParent(transform, false);
            lampObj.transform.localPosition = new Vector3(0f, 2.55f, 0.05f);
            _lamp = lampObj.AddComponent<Light>();
            _lamp.type = LightType.Point;
            _lamp.color = new Color(1f, 0.90f, 0.62f);
            _lamp.intensity = 8.5f;
            _lamp.range = 7.5f;
            _lamp.shadows = LightShadows.None;
        }

        public bool IsPlayerInRange(Vector3 playerPos)
        {
            if (manager == null) return false;
            Vector3 d = playerPos - transform.position;
            d.y = 0f;
            return d.magnitude <= InteractRadius;
        }

        public bool TryBuy(string nodeId)
        {
            if (manager == null) return false;
            bool ok = manager.BuyNode(nodeId);
            if (ok) _buyPulse = 1f;
            return ok;
        }

        private void Update()
        {
            _pulseTime += Time.deltaTime;
            if (_buyPulse > 0f) _buyPulse = Mathf.MoveTowards(_buyPulse, 0f, Time.deltaTime * 2.5f);
            if (_lamp != null) _lamp.intensity = 8.5f + Mathf.Sin(_pulseTime * 3.0f) * 1.2f + _buyPulse * 6.0f;
        }

        private GameObject CreateBox(string n, Vector3 localPos, Vector3 size, Material mat, bool keepCol)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = n;
            g.transform.SetParent(transform, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = size;
            if (!keepCol) Destroy(g.GetComponent<Collider>());
            if (mat != null) g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        private static Material MakeSimpleMat(Color c, float smoothness)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private static Material MakeEmissiveMat(Color c, float emMult)
        {
            Material m = MakeSimpleMat(c, 0.4f);
            if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", c * emMult); m.EnableKeyword("_EMISSION"); }
            return m;
        }
    }
}
