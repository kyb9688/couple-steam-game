using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// v0.8 쓰레기 '지층(발굴)' 구조:
    /// 쓰레기 산 아래에 물리적으로 파묻혀 있다가, 위를 덮고 있는 쓰레기를 걷어내면
    /// 바닥에서 황금빛으로 노출되어 직접 발굴(전달 vs 슈킹)하는 3D 오브젝트!
    /// </summary>
    public class BuriedTreasureObject : MonoBehaviour
    {
        public string itemName = "숨겨진 보물";
        public int baseValue = 160;
        public TreasureRarity rarity = TreasureRarity.Rare;
        public bool isMentionedByClient = false; // 의뢰인 힌트에 언급된 물건(슈킹 시 단가 ×2 & 의심 ×2!)
        public float baseSuspicionGain = 22f;
        public int coverThreshold = 4;           // 위를 덮은 쓰레기가 이 이하로 줄면 노출!
        public float coverCheckRadius = 0.72f;

        public GamePrototypeManager manager;
        public bool IsUncovered { get; private set; }
        public bool IsCollected { get; private set; }

        private Vector3 _basePos;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private GameObject _whiteOutline;
        private GameObject _xrayOutline;
        private bool _highlighted;
        private float _checkTimer;

        private void Awake()
        {
            _basePos = transform.position;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            BuildOutlines();
        }

        private void Start()
        {
            if (manager == null) manager = FindFirstObjectByType<GamePrototypeManager>();
        }

        private void BuildOutlines()
        {
            var mfs = GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return;

            Shader whiteSh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material wMat = new Material(whiteSh);
            wMat.SetColor("_OutlineColor", Color.white);
            wMat.SetFloat("_OutlineWidth", 0.016f);

            Shader xraySh = Shader.Find("TrashCleaner3D/DetectOutlineXRay") ?? whiteSh;
            Material xMat = new Material(xraySh);
            xMat.SetColor("_OutlineColor", isMentionedByClient ? new Color(1f, 0.38f, 0.25f, 0.85f) : new Color(1f, 0.88f, 0.28f, 0.85f));
            xMat.SetFloat("_OutlineWidth", 0.016f);

            _whiteOutline = MakeOutlineGroup("_WhiteOut", mfs, wMat);
            _xrayOutline = MakeOutlineGroup("_XRayOut", mfs, xMat);
        }

        private GameObject MakeOutlineGroup(string n, MeshFilter[] mfs, Material mat)
        {
            GameObject root = new GameObject(n);
            root.transform.SetParent(transform, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_O");
                copy.transform.SetParent(root.transform, false);
                copy.transform.position = mf.transform.position;
                copy.transform.rotation = mf.transform.rotation;
                copy.transform.localScale = mf.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = copy.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            root.SetActive(false);
            return root;
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on || IsCollected) return;
            _highlighted = on;
            if (_whiteOutline != null) _whiteOutline.SetActive(on && IsUncovered);
        }

        public void Collect()
        {
            if (IsCollected || !IsUncovered) return;
            IsCollected = true;
            SetHighlighted(false);
            if (_xrayOutline != null) _xrayOutline.SetActive(false);
            gameObject.SetActive(false);
            if (manager != null) manager.OnBuriedTreasureExcavated(this);
        }

        public void ResetForNewRun()
        {
            IsCollected = false;
            IsUncovered = false;
            _highlighted = false;
            transform.position = _basePos;
            gameObject.SetActive(true);
            if (_whiteOutline != null) _whiteOutline.SetActive(false);
            if (_xrayOutline != null) _xrayOutline.SetActive(false);
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
        }

        private void Update()
        {
            if (IsCollected || manager == null) return;

            // 보물 투시 탐지기(신뢰도 토큰 해금!) 보유 시 쓰레기 아래 파묻혀 있어도 은은한 투시 외곽선 표시
            bool showXRay = manager.HasTreasureDetector && !IsCollected && !_highlighted;
            if (_xrayOutline != null && _xrayOutline.activeSelf != showXRay)
            {
                _xrayOutline.SetActive(showXRay);
            }

            if (!IsUncovered)
            {
                _checkTimer -= Time.deltaTime;
                if (_checkTimer <= 0f)
                {
                    _checkTimer = 0.25f;
                    int covering = manager.trashSwarm != null
                        ? manager.trashSwarm.CountIdleCovering(_basePos, coverCheckRadius)
                        : 0;
                    if (covering <= coverThreshold)
                    {
                        IsUncovered = true;
                        manager.OnTreasureUncoveredAlert(this);
                    }
                }
            }
            else
            {
                // 발굴 노출 후 바닥에서 살짝 떠오르며 반짝임!
                float bob = 0.05f + 0.03f * Mathf.Sin(Time.time * 4.2f);
                transform.position = _basePos + Vector3.up * bob;

                float pulse = 0.28f + 0.28f * Mathf.Abs(Mathf.Sin(Time.time * 4.5f));
                Color em = isMentionedByClient
                    ? new Color(1.0f, 0.45f, 0.15f) * pulse
                    : new Color(1.0f, 0.84f, 0.22f) * pulse;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor("_EmissionColor", em);
                    _renderers[i].SetPropertyBlock(_mpb);
                    if (_renderers[i].sharedMaterial != null) _renderers[i].sharedMaterial.EnableKeyword("_EMISSION");
                }
            }
        }
    }
}
