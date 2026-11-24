using UnityEngine;

namespace TrashCleaner3D
{
    public enum CleanableKind
    {
        TrashPile,   // 단단한 큰 봉투/박스 — 주먹으로 여러 번 퍽!퍽! 때려 터뜨리면 작은 쓰레기 + 보물 가챠!
        SmallTrash,  // 작은 쓰레기 — 집어서 왼손 쓰레기봉투에 쏙쏙 담기
        FloorStain   // Phase 2 바닥 얼룩 — 마우스 좌우 흔들어 물티슈/대걸레로 문질러 닦기
    }

    /// <summary>
    /// v0.2 물리 OFF 청소 오브젝트:
    /// - TrashPile: 타격 시 찌그러짐+진동, 터질 때 작은 쓰레기 & 보물 가챠 발동
    /// - SmallTrash: 0.5초 포물선 팝업 후 정착, 주울 때 왼손 봉투로 쏙 흡입
    /// - FloorStain: 마우스 이동량 기반 문질러 닦기 (Phase 2 전용)
    /// - 10% 이하 탐지 업그레이드 시 빛기둥(Beacon) 자동 점등
    /// </summary>
    public class CleanableTarget : MonoBehaviour
    {
        public CleanableKind kind = CleanableKind.SmallTrash;
        public string displayName = "쓰레기";
        public float maxHp = 4.0f;
        public int rewardMoney = 6;
        public int bagCost = 1;
        public float cleanWeight = 1.0f;

        [Header("Burst Settings (TrashPile only)")]
        public int burstSpawnCount = 3;
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
        private float _hitShakeTimer;
        private GameObject _beaconObj;
        private GameObject _whiteOutlineRoot;

        // Burst pop-out animation (0.55s parabola then zero-physics rest!)
        private bool _isPoppingOut;
        private float _popTime;
        private Vector3 _popStart;
        private Vector3 _popEnd;

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
                BuildWhiteOutline();
            }
        }

        private void BuildWhiteOutline()
        {
            var mfs = GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return;

            Shader sh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material outlineMat = new Material(sh);
            outlineMat.SetColor("_OutlineColor", Color.white);
            outlineMat.SetFloat("_OutlineWidth", 0.018f);

            _whiteOutlineRoot = new GameObject("_WhiteOutline");
            _whiteOutlineRoot.transform.SetParent(transform, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_Outline");
                copy.transform.SetParent(_whiteOutlineRoot.transform, false);
                copy.transform.position = mf.transform.position;
                copy.transform.rotation = mf.transform.rotation;
                copy.transform.localScale = mf.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = copy.AddComponent<MeshRenderer>();
                mr.sharedMaterial = outlineMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _whiteOutlineRoot.SetActive(false);
        }

        public void StartPopOut(Vector3 startPos, Vector3 landingPos)
        {
            _isPoppingOut = true;
            _popTime = 0f;
            _popStart = startPos;
            _popEnd = landingPos;
            _initialPos = landingPos;
            transform.position = startPos;
        }

        public void SetBeaconVisible(bool visible)
        {
            if (_isCleaned) visible = false;
            if (visible && _beaconObj == null)
            {
                _beaconObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _beaconObj.name = "DetectBeacon";
                _beaconObj.transform.SetParent(transform, false);
                _beaconObj.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                _beaconObj.transform.localScale = new Vector3(0.08f, 1.3f, 0.08f);
                Destroy(_beaconObj.GetComponent<Collider>());
                var r = _beaconObj.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.SetColor("_BaseColor", kind == CleanableKind.FloorStain
                    ? new Color(0.25f, 0.85f, 1f, 0.55f)
                    : new Color(0.45f, 1f, 0.45f, 0.55f));
                r.material = mat;
            }
            if (_beaconObj != null) _beaconObj.SetActive(visible);
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on || _isCleaned) return;
            _highlighted = on;
            if (_whiteOutlineRoot != null) _whiteOutlineRoot.SetActive(on);

            Color em = on
                ? (kind == CleanableKind.FloorStain ? new Color(0.28f, 0.34f, 0.38f) : new Color(0.22f, 0.22f, 0.22f))
                : Color.black;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", em);
                _renderers[i].SetPropertyBlock(_mpb);
                if (on && _renderers[i].sharedMaterial != null)
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
                // 퍽! 때릴 때마다 살짝 찌그러지며 터지기 직전 팽팽한 느낌
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
                if (kind == CleanableKind.SmallTrash && suckDestination != null)
                {
                    _isSuckingAway = true;
                    _suckTarget = suckDestination;
                    SetBeaconVisible(false);
                    SetHighlighted(false);
                    foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
                }
                else
                {
                    FinishClean();
                }
                return true;
            }

            return false;
        }

        public void ResetForNewRun()
        {
            _isCleaned = false;
            _isSuckingAway = false;
            _isPoppingOut = false;
            _hp = maxHp;
            transform.localScale = _initialScale;
            transform.position = _initialPos;
            gameObject.SetActive(true);
            SetBeaconVisible(false);
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
            if (_isPoppingOut)
            {
                _popTime += Time.deltaTime;
                float t = Mathf.Clamp01(_popTime / 0.45f);
                Vector3 p = Vector3.Lerp(_popStart, _popEnd, t);
                p.y += Mathf.Sin(t * Mathf.PI) * 0.55f;
                transform.position = p;
                transform.Rotate(Vector3.up * (260f * Time.deltaTime));
                if (t >= 1f)
                {
                    _isPoppingOut = false;
                    transform.position = _popEnd;
                }
                return;
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

            if (!_isSuckingAway) return;

            if (_suckTarget == null)
            {
                FinishClean();
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, _suckTarget.position, 10.5f * Time.deltaTime);
            transform.Rotate(Vector3.one * (440f * Time.deltaTime));
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, 4.8f * Time.deltaTime);

            if (Vector3.Distance(transform.position, _suckTarget.position) < 0.16f || transform.localScale.x < 0.04f)
            {
                FinishClean();
            }
        }

        private void FinishClean()
        {
            _isCleaned = true;
            _isSuckingAway = false;
            SetBeaconVisible(false);
            SetHighlighted(false);
            gameObject.SetActive(false);
        }
    }
}
