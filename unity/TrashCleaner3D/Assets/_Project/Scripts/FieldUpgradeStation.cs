using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v1.3 '통합 강화소' — 트럭 반납 지점 바로 옆에 서는 강화 키오스크 1대.
    ///
    /// 요청 #5·#10 반영:
    ///  · 예전의 발판 3개(범위 · 흡입 · 용량)를 **작업대 1대**로 합쳤다.
    ///  · 플레이어가 앞에 서서 [E]를 누르면 강화 창이 열리고, 그 창에서 14개 항목을 골라 산다.
    ///  · [Tab]으로 언제든 열리던 상시 강화 트리는 없앴다 → 강화 경로는 이 한 곳뿐.
    ///
    /// 씬 프리팹/직렬화에 의존하지 않고 GamePrototypeManager가 런타임에 코드로 스폰한다.
    /// </summary>
    public class FieldUpgradeStation : MonoBehaviour
    {
        public GamePrototypeManager manager;

        /// <summary>강화 창 한 칸에 표시할 항목 정보 (HUD가 그대로 그린다).</summary>
        public struct KioskItem
        {
            public string nodeId;
            public string icon;
            public string title;
            public string stat;
            public int level;
            public int maxLevel;
            public int cost;
            public bool maxed;
            public bool affordable;
            public Color color;
        }

        /// <summary>키오스크 앞 판정 반경(m)</summary>
        public const float InteractRadius = 2.35f;

        // 노드 순서 = 강화 창 진열 순서 (요청 #5: 흡입 속도·반경·수거통·정산 단가·이동 속도·로봇 대수/속도 등 세분화)
        private static readonly string[] NodeIds =
        {
            "tool", "sweep", "bag", "price",
            "combo", "speed", "mask", "fist",
            "mop", "scrub", "roomba", "roombaspeed",
            "truck", "fever"
        };
        private static readonly string[] Icons =
        {
            "▲", "◎", "▣", "◆",
            "»", "➤", "☣", "❖",
            "┴", "✧", "◉", "⚡",
            "▶", "✹"
        };
        private static readonly string[] Titles =
        {
            "흡입 속도", "흡입 반경", "수거통 용량", "정산 단가",
            "연속 정산 콤보", "이동 속도", "방독면", "주먹 힘",
            "걸레 성능", "강력 세제", "로봇 대수", "로봇 속도",
            "수거함 진입", "피버 엔진"
        };
        private static readonly Color[] ItemColors =
        {
            new Color(0.95f, 0.78f, 0.24f), new Color(0.95f, 0.78f, 0.24f),
            new Color(0.32f, 0.78f, 0.98f), new Color(0.32f, 0.78f, 0.98f),
            new Color(0.32f, 0.78f, 0.98f), new Color(0.36f, 0.90f, 0.54f),
            new Color(0.36f, 0.90f, 0.54f), new Color(0.98f, 0.56f, 0.24f),
            new Color(0.36f, 0.90f, 0.54f), new Color(0.36f, 0.90f, 0.54f),
            new Color(0.62f, 0.84f, 1.00f), new Color(0.62f, 0.84f, 1.00f),
            new Color(0.98f, 0.72f, 0.28f), new Color(0.95f, 0.60f, 0.26f)
        };

        private bool _built;
        private Material _screenMat;
        private Material _signMat;
        private Light _lamp;
        private float _pulseTime;
        private float _buyPulse;

        public int ItemCount => NodeIds.Length;

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
            Material metalMat = MakeSimpleMat(new Color(0.34f, 0.37f, 0.42f), 0.62f);
            Material counterMat = MakeSimpleMat(new Color(0.30f, 0.26f, 0.22f), 0.28f);
            _signMat = MakeEmissiveMat(new Color(0.98f, 0.86f, 0.36f), 1.35f);
            _screenMat = MakeEmissiveMat(new Color(0.36f, 0.88f, 0.78f), 1.05f);

            // 바닥 슬래브 (2.40 x 1.60)
            CreateBox("Kiosk_Base", new Vector3(0f, 0.05f, 0f), new Vector3(2.40f, 0.10f, 1.60f), baseMat, true);

            // 앞 작업대 + 상판
            CreateBox("Kiosk_Counter", new Vector3(0f, 0.50f, 0.30f), new Vector3(2.05f, 0.92f, 0.52f), counterMat, true);
            CreateBox("Kiosk_CounterTop", new Vector3(0f, 0.98f, 0.30f), new Vector3(2.16f, 0.06f, 0.60f), metalMat, true);

            // 뒤쪽 프레임 기둥 2개 + 상단 간판
            CreateBox("Kiosk_Post_L", new Vector3(-0.98f, 1.02f, -0.62f), new Vector3(0.09f, 2.04f, 0.09f), metalMat, true);
            CreateBox("Kiosk_Post_R", new Vector3(0.98f, 1.02f, -0.62f), new Vector3(0.09f, 2.04f, 0.09f), metalMat, true);
            GameObject sign = CreateBox("Kiosk_Sign", new Vector3(0f, 2.10f, -0.62f), new Vector3(2.20f, 0.42f, 0.08f), _signMat, true);
            Object.Destroy(sign.GetComponent<Collider>());

            // 화면 패널 (작업대 위, 살짝 기울여 서 있는 모니터)
            GameObject screen = CreatePrimitive(PrimitiveType.Cube, "Kiosk_Screen", transform,
                new Vector3(0f, 1.30f, -0.10f), new Vector3(0.98f, 0.56f, 0.04f), _screenMat);
            screen.transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);
            Object.Destroy(screen.GetComponent<Collider>());
            CreateBox("Kiosk_ScreenFrame", new Vector3(0f, 1.30f, -0.09f), new Vector3(1.08f, 0.66f, 0.03f), baseMat, false)
                .transform.localRotation = Quaternion.Euler(-16f, 0f, 0f);

            // 작업대 위 작은 도구함·판넬 — '강화 작업대' 느낌
            CreateBox("Kiosk_ToolBox", new Vector3(-0.70f, 1.10f, 0.34f), new Vector3(0.34f, 0.18f, 0.26f), metalMat, false);
            CreateBox("Kiosk_ToolBox2", new Vector3(0.72f, 1.08f, 0.30f), new Vector3(0.26f, 0.14f, 0.22f), baseMat, false);

            // 야드가 어두워도 강화소가 바로 눈에 띄도록 작은 조명
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

        /// <summary>플레이어가 키오스크 앞에 서 있는지.</summary>
        public bool IsPlayerInRange(Vector3 playerPos)
        {
            if (manager == null) return false;
            Vector3 d = playerPos - transform.position;
            d.y = 0f;
            return d.magnitude <= InteractRadius;
        }

        /// <summary>강화 창 한 칸 정보를 돌려준다. (HUD가 그대로 그린다)</summary>
        public KioskItem GetItem(int i)
        {
            i = Mathf.Clamp(i, 0, NodeIds.Length - 1);
            string id = NodeIds[i];

            KioskItem info = new KioskItem
            {
                nodeId = id,
                icon = Icons[i],
                title = Titles[i],
                color = ItemColors[i],
                level = 0,
                maxLevel = 1,
                cost = 0
            };
            if (manager == null) return info;

            switch (id)
            {
                case "tool":
                    info.level = manager.ToolStep; info.maxLevel = 9; info.cost = manager.CostTool;
                    info.stat = $"홀드 {manager.EffectiveHoldDuration:0.00}초 · {manager.CurrentToolName}";
                    break;
                case "sweep":
                    info.level = manager.LvSweepRadius; info.maxLevel = 10; info.cost = manager.CostSweep;
                    info.stat = $"반경 {manager.SweepRadius:0.00}m · 동시 {manager.AutoGrabPerTick}개";
                    break;
                case "bag":
                    info.level = manager.LvBagSize; info.maxLevel = 10; info.cost = manager.CostBag;
                    info.stat = $"수거통 {manager.BinCapacity}개";
                    break;
                case "price":
                    info.level = manager.LvTrashPrice; info.maxLevel = 10; info.cost = manager.CostTrashPrice;
                    info.stat = $"정산 단가 +{manager.LvTrashPrice * 15}%";
                    break;
                case "combo":
                    info.level = manager.LvComboTime; info.maxLevel = 6; info.cost = manager.CostComboTime;
                    info.stat = $"유지 {manager.MaxComboWindow:0.0}초 · 스택당 ×1.25";
                    break;
                case "speed":
                    info.level = manager.LvMoveSpeed; info.maxLevel = 8; info.cost = manager.CostMoveSpeed;
                    info.stat = $"이동 속도 +{manager.LvMoveSpeed * 8}%";
                    break;
                case "mask":
                    info.level = manager.LvMask; info.maxLevel = 10; info.cost = manager.CostMask;
                    info.stat = $"악취 -{Mathf.Min(82, manager.LvMask * 8)}%";
                    break;
                case "fist":
                    info.level = manager.LvFistPower; info.maxLevel = 10; info.cost = manager.CostFist;
                    info.stat = $"큰 봉투 타격 {manager.FistDamage:0.0}";
                    break;
                case "mop":
                    info.level = manager.LvMop; info.maxLevel = 3; info.cost = manager.CostMop;
                    info.stat = manager.HasMopUnlocked ? $"닦기 ×{1f + manager.LvMop * 0.75f:0.00}" : "T자 대걸레 해금";
                    break;
                case "scrub":
                    info.level = manager.LvScrubPower; info.maxLevel = 8; info.cost = manager.CostScrub;
                    info.stat = $"세정력 +{manager.LvScrubPower * 25}%";
                    break;
                case "roomba":
                    info.level = manager.LvRoomba; info.maxLevel = 2; info.cost = manager.CostRoomba;
                    info.stat = $"로봇청소기 {manager.RoombaCount}대 (최대 3대)";
                    break;
                case "roombaspeed":
                    info.level = manager.LvRoombaSpeed; info.maxLevel = 6; info.cost = manager.CostRoombaSpeed;
                    info.stat = $"{manager.RoombaEatInterval:0.00}초에 1개";
                    break;
                case "truck":
                    info.level = manager.LvTruckSize; info.maxLevel = 9; info.cost = manager.CostTruck;
                    info.stat = manager.CurrentVehicleName;
                    break;
                default: // fever — 연속 흡입 게이지
                    info.level = manager.LvFever; info.maxLevel = 5; info.cost = manager.CostFever;
                    info.stat = $"연속 흡입 → 범위 +{manager.LvFever * 8}% · 수익 ×{1.15f + manager.LvFever * 0.10f:0.00}";
                    break;
            }

            info.maxed = info.level >= info.maxLevel;
            info.affordable = !info.maxed && manager.Money >= info.cost;
            return info;
        }

        /// <summary>강화 창에서 한 항목을 구매한다.</summary>
        public bool TryBuy(string nodeId)
        {
            if (manager == null) return false;

            KioskItem info = new KioskItem();
            for (int i = 0; i < NodeIds.Length; i++)
            {
                if (NodeIds[i] == nodeId) { info = GetItem(i); break; }
            }

            if (string.IsNullOrEmpty(info.nodeId)) return false;
            if (info.maxed)
            {
                manager.ShowToast($"{info.title} 강화는 이미 MAX");
                return false;
            }
            if (manager.Money < info.cost)
            {
                if (manager.sfx != null) manager.sfx.PlayAlert();
                manager.ShowToast($"현금 부족 · ${info.cost:N0} 필요 (보유 ${manager.Money:N0})");
                return false;
            }
            if (!manager.BuyNode(info.nodeId))
            {
                if (manager.sfx != null) manager.sfx.PlayAlert();
                return false;
            }

            _buyPulse = 1f;
            if (manager.sfx != null) manager.sfx.PlayUpgrade();
            manager.SpawnFloatingText(transform.position + Vector3.up * 2.3f,
                $"▲ {info.title} 강화! -${info.cost:N0}", info.color, 1.6f);
            return true;
        }

        private void Update()
        {
            _pulseTime += Time.deltaTime;
            if (_buyPulse > 0f) _buyPulse = Mathf.MoveTowards(_buyPulse, 0f, Time.deltaTime * 2.2f);

            if (_lamp != null)
                _lamp.intensity = 8.5f + Mathf.Sin(_pulseTime * 2.4f) * 1.2f + _buyPulse * 6f;

            if (_signMat != null)
            {
                float e = 1.05f + 0.25f * Mathf.Sin(_pulseTime * 2.0f) + _buyPulse * 0.9f;
                if (_signMat.HasProperty("_EmissionColor"))
                    _signMat.SetColor("_EmissionColor", new Color(0.98f, 0.86f, 0.36f) * e);
            }
            if (_screenMat != null)
            {
                float e = 1.0f + 0.18f * Mathf.Sin(_pulseTime * 3.4f) + _buyPulse * 0.8f;
                if (_screenMat.HasProperty("_EmissionColor"))
                    _screenMat.SetColor("_EmissionColor", new Color(0.36f, 0.88f, 0.78f) * e);
            }
        }

        private GameObject CreateBox(string name, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
        {
            GameObject go = CreatePrimitive(PrimitiveType.Cube, name, transform, localPos, localScale, mat);
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                if (keepCollider) c.isTrigger = true;   // 강화소는 플레이어 이동을 막지 않는다
                else Object.Destroy(c);
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
