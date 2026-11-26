using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 트럭 반납 지점 바로 옆에 서는 '현장 강화' 자판기 존.
    /// 발판 3개(범위 · 흡입 · 용량) 위에 올라서서 [E]를 누르면 그 자리에서 바로 핵심 강화를 산다.
    /// → 방을 쓸고 → 밖으로 뛰어나와 트럭에 와르르 쏟고 → 한 걸음 옆에서 강화 → 다시 방으로.
    /// 씬 프리팹/직렬화에 의존하지 않고 GamePrototypeManager가 런타임에 코드로 스폰한다.
    /// </summary>
    public class FieldUpgradeStation : MonoBehaviour
    {
        public GamePrototypeManager manager;

        public struct PadInfo
        {
            public int padIndex;
            public string nodeId;
            public string label;
            public string title;
            public string stat;
            public int cost;
            public int level;
            public int maxLevel;
            public bool affordable;
            public bool maxed;
            public Color color;
        }

        public const float PadStandRadius = 0.62f;

        private static readonly string[] NodeIds = { "sweep", "tool", "bag" };
        private static readonly string[] Labels = { "범위", "흡입", "용량" };
        private static readonly Color[] PadColors =
        {
            new Color(0.98f, 0.80f, 0.26f),
            new Color(0.42f, 0.94f, 0.62f),
            new Color(0.36f, 0.78f, 0.98f)
        };

        private bool _built;
        private Transform[] _pads = new Transform[0];
        private Transform[] _icons = new Transform[0];
        private Material[] _padMats = new Material[0];
        private Material _signMat;
        private Light _lamp;
        private float _buyPulse;
        private float _pulseTime;
        private float _colorTimer;

        /// <summary>매니저가 스폰 직후 한 번 호출한다.</summary>
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
            _signMat = MakeEmissiveMat(new Color(0.98f, 0.86f, 0.36f), 1.35f);

            // 바닥 슬래브
            CreateBox("Station_Base", new Vector3(0f, 0.06f, 0f), new Vector3(3.90f, 0.12f, 1.45f), baseMat, true);

            // 뒤쪽 표지판 + 기둥 2개 (플레이어가 트럭 쪽에서 다가오면 발판 → 표지판 순서로 보이게)
            CreateBox("Station_SignPost_L", new Vector3(-1.72f, 0.95f, -0.66f), new Vector3(0.10f, 1.90f, 0.10f), baseMat, true);
            CreateBox("Station_SignPost_R", new Vector3(1.72f, 0.95f, -0.66f), new Vector3(0.10f, 1.90f, 0.10f), baseMat, true);
            GameObject sign = CreateBox("Station_Sign", new Vector3(0f, 1.86f, -0.66f), new Vector3(3.70f, 0.52f, 0.10f), _signMat, true);
            Object.Destroy(sign.GetComponent<Collider>());

            // 발판 3개 (범위 · 흡입 · 용량)
            _pads = new Transform[NodeIds.Length];
            _icons = new Transform[NodeIds.Length];
            _padMats = new Material[NodeIds.Length];

            for (int i = 0; i < NodeIds.Length; i++)
            {
                float x = (i - 1) * 1.22f;

                GameObject padRoot = new GameObject($"Pad_{NodeIds[i]}");
                padRoot.transform.SetParent(transform, false);
                padRoot.transform.localPosition = new Vector3(x, 0f, -0.06f);

                Material padMat = MakeEmissiveMat(PadColors[i] * 0.55f, 1.05f);
                _padMats[i] = padMat;

                GameObject padBody = CreatePrimitive(PrimitiveType.Cube, "Pad_Body", padRoot.transform,
                    new Vector3(0f, 0.14f, 0f), new Vector3(1.04f, 0.16f, 1.04f), baseMat);
                padBody.GetComponent<Renderer>().sharedMaterial = baseMat;

                GameObject padTop = CreatePrimitive(PrimitiveType.Cube, "Pad_Top", padRoot.transform,
                    new Vector3(0f, 0.235f, 0f), new Vector3(0.96f, 0.04f, 0.96f), padMat);
                padTop.GetComponent<Renderer>().sharedMaterial = padMat;
                Object.Destroy(padTop.GetComponent<Collider>());

                // 발판 위로 살짝 떠 있는 아이콘(강화 가능 상태를 한눈에)
                GameObject icon = CreatePrimitive(PrimitiveType.Cube, "Pad_Icon", padRoot.transform,
                    new Vector3(0f, 0.95f, 0f), new Vector3(0.26f, 0.26f, 0.26f), padMat);
                icon.GetComponent<Renderer>().sharedMaterial = padMat;
                Object.Destroy(icon.GetComponent<Collider>());

                _pads[i] = padRoot.transform;
                _icons[i] = icon.transform;
            }

            // 야드가 어두워도 강화 존이 바로 눈에 띄도록 작은 스팟 조명
            GameObject lampObj = new GameObject("Station_Lamp");
            lampObj.transform.SetParent(transform, false);
            lampObj.transform.localPosition = new Vector3(0f, 2.55f, -0.10f);
            _lamp = lampObj.AddComponent<Light>();
            _lamp.type = LightType.Point;
            _lamp.color = new Color(1f, 0.90f, 0.62f);
            _lamp.intensity = 8.5f;
            _lamp.range = 7.5f;
            _lamp.shadows = LightShadows.None;
        }

        private void Update()
        {
            _pulseTime += Time.deltaTime;
            if (_buyPulse > 0f) _buyPulse = Mathf.MoveTowards(_buyPulse, 0f, Time.deltaTime * 2.2f);

            float bob = Mathf.Sin(_pulseTime * 3.1f) * 0.06f;
            for (int i = 0; i < _icons.Length; i++)
            {
                if (_icons[i] == null) continue;
                Vector3 lp = _icons[i].localPosition;
                lp.y = 0.95f + bob + (i - 1) * 0.01f;
                _icons[i].localPosition = lp;
                _icons[i].Rotate(Vector3.up, 55f * Time.deltaTime, Space.Self);
            }

            if (_lamp != null)
            {
                _lamp.intensity = 8.5f + Mathf.Sin(_pulseTime * 2.4f) * 1.2f + _buyPulse * 6f;
            }
            if (_signMat != null)
            {
                float e = 1.05f + 0.25f * Mathf.Sin(_pulseTime * 2.0f) + _buyPulse * 0.9f;
                if (_signMat.HasProperty("_EmissionColor"))
                    _signMat.SetColor("_EmissionColor", new Color(0.98f, 0.86f, 0.36f) * e);
            }

            if (manager == null) return;

            _colorTimer -= Time.deltaTime;
            if (_colorTimer <= 0f)
            {
                _colorTimer = 0.10f;
                RefreshPadColors();
            }
        }

        private void RefreshPadColors()
        {
            for (int i = 0; i < _padMats.Length; i++)
            {
                Material m = _padMats[i];
                if (m == null) continue;

                PadInfo info = Describe(i);
                float e = info.maxed ? 0.35f : info.affordable ? (1.10f + 0.30f * Mathf.Sin(_pulseTime * 5.0f)) : 0.55f;
                if (m.HasProperty("_EmissionColor"))
                    m.SetColor("_EmissionColor", info.color * e);
            }
        }

        /// <summary>플레이어가 발판 위에 서 있으면 그 발판 정보를 돌려준다.</summary>
        public bool TryGetStandingPad(Vector3 playerPos, out PadInfo info)
        {
            info = new PadInfo();
            if (manager == null || _pads == null || _pads.Length == 0) return false;

            int best = -1;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _pads.Length; i++)
            {
                if (_pads[i] == null) continue;
                Vector3 d = _pads[i].position - playerPos;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist <= PadStandRadius && dist < bestDist)
                {
                    bestDist = dist;
                    best = i;
                }
            }
            if (best < 0) return false;

            info = Describe(best);
            return true;
        }

        /// <summary>발판 위에서 [E]를 눌렀을 때 실제 구매 처리.</summary>
        public bool TryBuy(PadInfo info)
        {
            if (manager == null) return false;

            if (info.maxed)
            {
                manager.ShowToast($"{info.label} 강화는 이미 MAX");
                return false;
            }
            if (manager.Money < info.cost)
            {
                if (manager.sfx != null) manager.sfx.PlayAlert();
                manager.ShowToast($"현금 부족 · ${info.cost} 필요 (보유 ${manager.Money})");
                return false;
            }
            if (!manager.BuyNode(info.nodeId))
            {
                if (manager.sfx != null) manager.sfx.PlayAlert();
                return false;
            }

            _buyPulse = 1f;
            if (manager.sfx != null) manager.sfx.PlayUpgrade();

            Vector3 fxPos = transform.position + Vector3.up * 2.4f;
            if (info.padIndex >= 0 && info.padIndex < _pads.Length && _pads[info.padIndex] != null)
                fxPos = _pads[info.padIndex].position + Vector3.up * 1.5f;

            manager.SpawnFloatingText(fxPos, $"▲ {info.label} 강화! -${info.cost}", info.color, 1.6f);
            return true;
        }

        private PadInfo Describe(int i)
        {
            PadInfo info = new PadInfo
            {
                padIndex = i,
                nodeId = NodeIds[i],
                label = Labels[i],
                color = PadColors[i],
                level = 0,
                maxLevel = 1,
                cost = 0,
                stat = ""
            };

            switch (info.nodeId)
            {
                case "sweep":
                    info.level = manager.LvSweepRadius;
                    info.maxLevel = 10;
                    info.cost = manager.CostSweep;
                    info.stat = $"원 반경 {manager.SweepRadius:0.00}m · 동시 {manager.AutoGrabPerTick}개";
                    break;
                case "tool":
                    info.level = manager.ToolStep;
                    info.maxLevel = 9;
                    info.cost = manager.CostTool;
                    info.stat = $"홀드 {manager.EffectiveHoldDuration:0.00}초 · {manager.CurrentToolName}";
                    break;
                default:
                    info.level = manager.LvBagSize;
                    info.maxLevel = 10;
                    info.cost = manager.CostBag;
                    info.stat = $"봉투 {manager.HandBagCapacity}개";
                    break;
            }

            info.maxed = info.level >= info.maxLevel;
            info.affordable = !info.maxed && manager.Money >= info.cost;
            info.title = info.maxed
                ? $"{info.label} 강화 · MAX"
                : $"[E] {info.label} 강화  Lv.{info.level} → {info.level + 1}   (${info.cost})";
            return info;
        }

        private GameObject CreateBox(string name, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
        {
            GameObject go = CreatePrimitive(PrimitiveType.Cube, name, transform, localPos, localScale, mat);
            if (!keepCollider)
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) Object.Destroy(c);
            }
            else
            {
                Collider c = go.GetComponent<Collider>();
                if (c != null) c.isTrigger = true; // 강화 존은 플레이어 이동을 막지 않는다
            }
            return go;
        }

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (mat != null) r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return go;
        }

        private static Material MakeSimpleMat(Color color, float smoothness)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) return null;

            Material mat = new Material(sh);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static Material MakeEmissiveMat(Color color, float intensity)
        {
            Material mat = MakeSimpleMat(color, 0.45f);
            if (mat == null) return null;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * intensity);
            return mat;
        }
    }
}
