using UnityEngine;

namespace TrashCleaner3D
{
    public enum TreasureState
    {
        HiddenInRoom,
        ReturnedToClient,
        StolenByPlayer
    }

    public enum TreasureVisualKind
    {
        GoldRing,
        DogTag,
        LuckyTwoDollar,
        BabyGoldRing,
        CorporateSeal,
        PocketWatch,
        SportsCarKey,
        CashBundle,
        SmartWatch,
        GoldBar
    }

    /// <summary>
    /// 쓰레기 산 속 숨겨진 보물 또는 의뢰인 지정 분실물(슈킹 시 100% 발각).
    /// [F] 의뢰인 반환(안전 팁 + 발각 위험 하락) / [R] 몰래 슈킹(즉시 고액 현금 + 발각 위험 증가).
    /// </summary>
    public class TreasureTarget : MonoBehaviour
    {
        public string itemName = "숨겨진 보물";
        public int value = 100;
        public bool isDesignatedLostItem = false;
        public float suspicionGain = 18f;
        public bool isFromBox = false;

        public TreasureState State { get; private set; } = TreasureState.HiddenInRoom;

        private Vector3 _initialPos;
        private Quaternion _initialRot;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _highlighted;
        private Transform _beaconBeam;
        private Transform _beaconRing;
        private float _sparkleTimer;

        private void Awake()
        {
            _initialPos = transform.position;
            _initialRot = transform.rotation;
            _sparkleTimer = Random.Range(0.4f, 2.0f);
            CacheRenderers();
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderers != null)
            {
                foreach (var r in _renderers)
                {
                    if (r != null && r.sharedMaterial != null)
                    {
                        r.sharedMaterial.EnableKeyword("_EMISSION");
                    }
                }
            }
        }

        private void EnsureBeaconVisuals()
        {
            if (_beaconBeam != null) return;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            Color beamCol = isDesignatedLostItem
                ? new Color(0.22f, 0.88f, 1.0f, 0.30f)
                : new Color(1.0f, 0.80f, 0.16f, 0.28f);

            Material MakeAdditive(Color c)
            {
                Material m = new Material(unlit);
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 15;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c;
                return m;
            }

            // 수직 빛기둥(멀리서도 보물·분실물 위치가 은은하게 반짝여 발견 쾌감 극대화!)
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beam.GetComponent<Collider>());
            beam.name = "_TreasureBeaconBeam";
            beam.transform.SetParent(transform, false);
            beam.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            beam.transform.localScale = new Vector3(0.085f, 0.65f, 0.085f);
            var br = beam.GetComponent<Renderer>();
            br.sharedMaterial = MakeAdditive(beamCol);
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;
            _beaconBeam = beam.transform;

            // 바닥 글로우 후광 원반
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.name = "_TreasureGlowDisc";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            ring.transform.localScale = new Vector3(0.36f, 0.008f, 0.36f);
            var rr = ring.GetComponent<Renderer>();
            rr.sharedMaterial = MakeAdditive(new Color(beamCol.r, beamCol.g, beamCol.b, 0.42f));
            rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rr.receiveShadows = false;
            _beaconRing = ring.transform;
        }

        private void SetBeaconActive(bool on)
        {
            if (_beaconBeam != null) _beaconBeam.gameObject.SetActive(on);
            if (_beaconRing != null) _beaconRing.gameObject.SetActive(on);
        }

        private void Update()
        {
            if (State != TreasureState.HiddenInRoom || _renderers == null || _mpb == null) return;

            // 우아한 공중 부유 + 회전으로 쓰레기와 확실히 구분되는 보물 존재감!
            float bob = 0.07f + 0.035f * Mathf.Sin(Time.time * 3.1f + _initialPos.x);
            transform.position = _initialPos + Vector3.up * bob;
            transform.Rotate(Vector3.up, 42f * Time.deltaTime, Space.World);

            if (_beaconBeam != null)
            {
                float w = 0.075f + 0.025f * Mathf.Sin(Time.time * 4.5f);
                if (_highlighted) w *= 1.45f;
                _beaconBeam.localScale = new Vector3(w, _highlighted ? 0.85f : 0.65f, w);
            }
            if (_beaconRing != null)
            {
                float rs = 0.34f + 0.08f * Mathf.Sin(Time.time * 3.8f);
                if (_highlighted) rs *= 1.30f;
                _beaconRing.localScale = new Vector3(rs, 0.008f, rs);
            }

            _sparkleTimer -= Time.deltaTime;
            if (_sparkleTimer <= 0f)
            {
                _sparkleTimer = _highlighted ? 0.8f : 2.4f;
                Color spCol = isDesignatedLostItem ? new Color(0.35f, 0.92f, 1f) : new Color(1f, 0.88f, 0.32f);
                SparkleBurst.Spawn(transform.position + Vector3.up * 0.14f, 6, 0.42f, spCol);
            }

            // 선명한 황금빛 / 시안-블루 발광 펄스
            float pulse = 0.42f + 0.32f * Mathf.Sin(Time.time * 4.2f);
            if (_highlighted) pulse += 0.65f;

            Color glow = isDesignatedLostItem
                ? new Color(0.20f, 0.84f, 1.0f) * pulse
                : new Color(1.0f, 0.80f, 0.16f) * pulse;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                if (_beaconBeam != null && _renderers[i].transform == _beaconBeam) continue;
                if (_beaconRing != null && _renderers[i].transform == _beaconRing) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", glow);
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }

        public void SetHighlighted(bool on)
        {
            _highlighted = on;
        }

        public void ConfigureForLayout(Vector3 worldPos, string label, int cashVal, bool designated, float suspGain, bool fromBox = false)
        {
            itemName = label;
            value = Mathf.Max(20, cashVal);
            isDesignatedLostItem = designated;
            suspicionGain = suspGain;
            isFromBox = fromBox;
            State = TreasureState.HiddenInRoom;
            _highlighted = false;
            transform.position = worldPos;
            _initialPos = worldPos;
            _initialRot = transform.rotation;
            gameObject.SetActive(true);
            EnsureCollider();
            EnsureBeaconVisuals();
            SetBeaconActive(true);
            CacheRenderers();
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = true;
        }

        private void EnsureCollider()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) box = gameObject.AddComponent<BoxCollider>();
            box.size = new Vector3(0.56f, 0.52f, 0.56f);
            box.center = new Vector3(0f, 0.14f, 0f);
            box.isTrigger = true;
        }

        public void MarkReturned(Transform returnShelfSlot, int slotIndex = 0)
        {
            State = TreasureState.ReturnedToClient;
            _highlighted = false;
            SetBeaconActive(false);
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;
            if (returnShelfSlot != null)
            {
                float colOff = (slotIndex % 3 - 1) * 0.20f;
                float rowOff = (slotIndex / 3) * 0.12f;
                transform.position = returnShelfSlot.position + new Vector3(colOff, 0.06f, -rowOff);
                transform.rotation = Quaternion.identity;
                if (_renderers != null && _mpb != null)
                {
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        if (_renderers[i] == null) continue;
                        _renderers[i].GetPropertyBlock(_mpb);
                        _mpb.SetColor("_EmissionColor", isDesignatedLostItem ? new Color(0.10f, 0.40f, 0.24f) : Color.black);
                        _renderers[i].SetPropertyBlock(_mpb);
                    }
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        public void MarkStolen()
        {
            State = TreasureState.StolenByPlayer;
            _highlighted = false;
            SetBeaconActive(false);
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = false;
            gameObject.SetActive(false);
        }

        public void ResetForNewRun()
        {
            if (isFromBox)
            {
                Destroy(gameObject);
                return;
            }
            State = TreasureState.HiddenInRoom;
            _highlighted = false;
            transform.position = _initialPos;
            transform.rotation = _initialRot;
            gameObject.SetActive(true);
            SetBeaconActive(true);
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = true;
        }

        /// <summary>Box/Cylinder 프리미티브만으로 보물 3D 모델을 생성 (WebGL 스트리핑 안전).</summary>
        public static TreasureTarget SpawnProcedural(Transform parent, TreasureVisualKind kind, Vector3 pos, string name, int cashVal, bool designated, float suspGain, bool fromBox = false)
        {
            GameObject go = new GameObject($"Treasure_{kind}_{name}");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f);
            go.transform.localScale = Vector3.one * 1.38f;

            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material MakeMat(Color c, float smooth, float metal = 0.25f)
            {
                Material m = new Material(lit);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.color = c;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
                m.EnableKeyword("_EMISSION");
                return m;
            }

            void AddPart(PrimitiveType pt, Vector3 lp, Vector3 rot, Vector3 sc, Material m)
            {
                GameObject p = GameObject.CreatePrimitive(pt);
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = lp;
                p.transform.localRotation = Quaternion.Euler(rot);
                p.transform.localScale = sc;
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Material mGold = MakeMat(new Color(0.98f, 0.80f, 0.22f), 0.88f, 0.85f);
            Material mSilver = MakeMat(new Color(0.84f, 0.88f, 0.94f), 0.86f, 0.80f);
            Material mBill = MakeMat(new Color(0.24f, 0.62f, 0.34f), 0.25f, 0.05f);

            switch (kind)
            {
                case TreasureVisualKind.GoldRing:
                case TreasureVisualKind.BabyGoldRing:
                    float rs = kind == TreasureVisualKind.BabyGoldRing ? 0.82f : 1.0f;
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.045f, 0f), new Vector3(12f, 0f, 0f), new Vector3(0.14f * rs, 0.028f, 0.14f * rs), mGold);
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.082f, 0.05f), new Vector3(0f, 45f, 0f), new Vector3(0.055f * rs, 0.045f, 0.055f * rs), MakeMat(new Color(0.45f, 0.92f, 1.0f), 0.95f, 0.2f));
                    break;
                case TreasureVisualKind.DogTag:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(0f, 18f, 0f), new Vector3(0.14f, 0.018f, 0.22f), mSilver);
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.032f, 0.12f), Vector3.zero, new Vector3(0.06f, 0.01f, 0.06f), mSilver);
                    break;
                case TreasureVisualKind.LuckyTwoDollar:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.025f, 0f), new Vector3(0f, 8f, 0f), new Vector3(0.28f, 0.015f, 0.14f), mBill);
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.06f, 0.016f, 0.142f), mGold);
                    break;
                case TreasureVisualKind.CorporateSeal:
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), Vector3.zero, new Vector3(0.085f, 0.075f, 0.085f), MakeMat(new Color(0.42f, 0.16f, 0.12f), 0.55f, 0.1f));
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.11f), Vector3.zero, new Vector3(0.11f, 0.035f, 0.11f), MakeMat(new Color(0.86f, 0.18f, 0.16f), 0.35f, 0.1f));
                    break;
                case TreasureVisualKind.PocketWatch:
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.035f, 0f), Vector3.zero, new Vector3(0.17f, 0.032f, 0.17f), mGold);
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.042f, 0f), Vector3.zero, new Vector3(0.125f, 0.034f, 0.125f), MakeMat(new Color(0.96f, 0.94f, 0.86f), 0.6f, 0.1f));
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.105f), Vector3.zero, new Vector3(0.035f, 0.025f, 0.045f), mGold);
                    break;
                case TreasureVisualKind.SportsCarKey:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.03f, -0.02f), Vector3.zero, new Vector3(0.10f, 0.032f, 0.18f), MakeMat(new Color(0.14f, 0.15f, 0.18f), 0.72f, 0.3f));
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.12f), Vector3.zero, new Vector3(0.036f, 0.012f, 0.13f), mSilver);
                    AddPart(PrimitiveType.Cylinder, new Vector3(0f, 0.048f, -0.03f), Vector3.zero, new Vector3(0.045f, 0.008f, 0.045f), MakeMat(new Color(0.92f, 0.22f, 0.18f), 0.6f, 0.2f));
                    break;
                case TreasureVisualKind.CashBundle:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.042f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0.28f, 0.078f, 0.15f), mBill);
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.045f, 0f), new Vector3(0f, 10f, 0f), new Vector3(0.06f, 0.082f, 0.154f), MakeMat(new Color(0.95f, 0.92f, 0.76f), 0.2f, 0f));
                    break;
                case TreasureVisualKind.SmartWatch:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), Vector3.zero, new Vector3(0.09f, 0.025f, 0.24f), MakeMat(new Color(0.16f, 0.18f, 0.22f), 0.45f, 0.2f));
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.045f, 0f), Vector3.zero, new Vector3(0.11f, 0.032f, 0.12f), MakeMat(new Color(0.18f, 0.75f, 0.95f), 0.9f, 0.5f));
                    break;
                case TreasureVisualKind.GoldBar:
                    AddPart(PrimitiveType.Cube, new Vector3(0f, 0.055f, 0f), new Vector3(0f, -12f, 0f), new Vector3(0.28f, 0.095f, 0.14f), mGold);
                    break;
            }

            TreasureTarget tt = go.AddComponent<TreasureTarget>();
            tt.ConfigureForLayout(pos, name, cashVal, designated, suspGain, fromBox);
            return tt;
        }
    }
}
