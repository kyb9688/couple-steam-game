using UnityEngine;

namespace TrashCleaner3D
{
    public enum CleanableKind
    {
        TrashPile,   // 단단한 큰 봉투/박스 — 주먹으로 여러 번 퍽!퍽! 때려 터뜨리기
        SmallTrash,  // 레거시 호환
        FloorStain   // Phase 2 바닥 얼룩 — 좌클릭+마우스 흔들어 닦기
    }

    /// <summary>
    /// v0.5 청소 오브젝트:
    /// - 기둥(Beacon) 완전 삭제! 대신 10% 이하 탐지 시 시야를 가리지 않는 은은한 투시/외곽 테두리 펄스만 표시!
    /// - 호버 시 선명한 하얀색 외곽선 표시.
    /// </summary>
    public class CleanableTarget : MonoBehaviour
    {
        public CleanableKind kind = CleanableKind.SmallTrash;
        public string displayName = "쓰레기";
        public float maxHp = 10.0f;
        public int rewardMoney = 0;
        public int bagCost = 0;
        public float cleanWeight = 1.0f;

        [Header("Burst Settings (TrashPile only)")]
        public int burstSpawnCount = 20;
        public bool containsTreasureChance = true;

        private float _hp;
        private bool _isCleaned;
        private bool _isSuckingAway;
        private Transform _suckTarget;
        private Vector3 _initialScale;
        private Vector3 _initialPos;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _highlighted;
        private bool _detectOutlineActive;
        private float _hitShakeTimer;
        private GameObject _whiteOutlineRoot;
        private GameObject _detectOutlineRoot;

        public bool IsCleaned => _isCleaned || _isSuckingAway;
        public float CleanProgress01 => maxHp > 0.001f ? Mathf.Clamp01(1f - (_hp / maxHp)) : 0f;

        private void Awake()
        {
            _hp = maxHp;
            _initialScale = transform.localScale;
            _initialPos = transform.position;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            if (kind == CleanableKind.TrashPile)
            {
                BuildOutlineRoots();
            }
        }

        private void BuildOutlineRoots()
        {
            var mfs = GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return;

            Shader whiteSh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material whiteMat = new Material(whiteSh);
            whiteMat.SetColor("_OutlineColor", Color.white);
            whiteMat.SetFloat("_OutlineWidth", 0.018f);

            Shader detectSh = Shader.Find("TrashCleaner3D/DetectOutlineXRay") ?? whiteSh;
            Material detectMat = new Material(detectSh);
            detectMat.SetColor("_OutlineColor", new Color(1f, 0.92f, 0.42f, 0.85f));
            detectMat.SetFloat("_OutlineWidth", 0.015f);

            _whiteOutlineRoot = CreateOutlineContainer("_WhiteOutline", mfs, whiteMat);
            _detectOutlineRoot = CreateOutlineContainer("_DetectXRayOutline", mfs, detectMat);
        }

        private GameObject CreateOutlineContainer(string rootName, MeshFilter[] mfs, Material mat)
        {
            GameObject root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_Out");
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

        /// <summary>
        /// 요청 #9 & #10: 방해되는 초록/파란 기둥 완전 삭제!
        /// 큰 봉투는 은은한 황금 투시 외곽 테두리, 바닥 얼룩은 바닥 자체의 은은한 밝은 펄스만 표시!
        /// </summary>
        public void SetDetectOutlineVisible(bool visible)
        {
            if (_isCleaned) visible = false;
            _detectOutlineActive = visible;
            if (_detectOutlineRoot != null)
            {
                _detectOutlineRoot.SetActive(visible && !_highlighted);
            }
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on || _isCleaned) return;
            _highlighted = on;
            if (_whiteOutlineRoot != null) _whiteOutlineRoot.SetActive(on);
            if (_detectOutlineRoot != null) _detectOutlineRoot.SetActive(_detectOutlineActive && !on);

            ApplyEmissionColor();
        }

        private void ApplyEmissionColor()
        {
            Color em = Color.black;
            if (_highlighted)
            {
                em = kind == CleanableKind.FloorStain ? new Color(0.32f, 0.42f, 0.48f) : new Color(0.22f, 0.22f, 0.22f);
            }
            else if (_detectOutlineActive && kind == CleanableKind.FloorStain)
            {
                float pulse = 0.20f + 0.18f * Mathf.Abs(Mathf.Sin(Time.time * 4.5f));
                em = new Color(0.75f, 0.90f, 1.0f) * pulse;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", em);
                _renderers[i].SetPropertyBlock(_mpb);
                if (em.maxColorComponent > 0.01f && _renderers[i].sharedMaterial != null)
                {
                    _renderers[i].sharedMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        public bool ApplyCleanDamage(float damage, Transform suckDestination)
        {
            if (_isCleaned || _isSuckingAway) return false;

            _hp -= damage;

            if (kind == CleanableKind.TrashPile)
            {
                _hitShakeTimer = 0.14f;
                float ratio = Mathf.Clamp01(_hp / maxHp);
                transform.localScale = new Vector3(
                    _initialScale.x * Mathf.Lerp(1.12f, 1.0f, ratio),
                    _initialScale.y * Mathf.Lerp(0.68f, 1.0f, ratio),
                    _initialScale.z * Mathf.Lerp(1.12f, 1.0f, ratio)
                );
            }
            else if (kind == CleanableKind.FloorStain)
            {
                float ratio = Mathf.Clamp01(_hp / maxHp);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Lerp(0.05f, 1f, ratio)));
                    _renderers[i].SetPropertyBlock(_mpb);
                }
            }

            if (_hp <= 0f)
            {
                FinishClean();
                return true;
            }
            return false;
        }

        public void ResetForNewRun()
        {
            _isCleaned = false;
            _isSuckingAway = false;
            _hp = maxHp;
            transform.localScale = _initialScale;
            transform.position = _initialPos;
            gameObject.SetActive(true);
            SetDetectOutlineVisible(false);
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = true;
            if (kind == CleanableKind.FloorStain)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", Color.white);
                    _renderers[i].SetPropertyBlock(_mpb);
                }
            }
            SetHighlighted(false);
        }

        private void Update()
        {
            if (_detectOutlineActive && kind == CleanableKind.FloorStain && !_isCleaned)
            {
                ApplyEmissionColor();
            }

            if (_hitShakeTimer > 0f && kind == CleanableKind.TrashPile)
            {
                _hitShakeTimer -= Time.deltaTime;
                Vector3 jitter = new Vector3(
                    Mathf.Sin(Time.time * 70f) * 0.035f,
                    0f,
                    Mathf.Cos(Time.time * 64f) * 0.035f
                );
                transform.position = _hitShakeTimer > 0f ? _initialPos + jitter : _initialPos;
            }
        }

        private void FinishClean()
        {
            _isCleaned = true;
            _isSuckingAway = false;
            SetDetectOutlineVisible(false);
            SetHighlighted(false);
            gameObject.SetActive(false);
        }
    }
}
