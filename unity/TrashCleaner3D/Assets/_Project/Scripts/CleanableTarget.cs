using UnityEngine;

namespace TrashCleaner3D
{
    public enum CleanableKind
    {
        TrashPile,   // 대형 쓰레기봉투 덩어리 (흡입 시 크기가 줄어들며 분해)
        SmallTrash,  // 개별 소품 (박스/페트병/캔 — 노즐로 슈우욱 빨려 들어감)
        FloorStain   // 바닥 곰팡이/국물 얼룩 (2번 장비로 닦아 투명도 감소)
    }

    /// <summary>
    /// 물리(Rigidbody) OFF 최적화 기반 청소 가능 오브젝트.
    /// 조준 시 Emission 하이라이트, 흡입/닦기 시 부드러운 스케일·알파 애니메이션 후 비활성화(철수 후에도 상태 유지).
    /// </summary>
    public class CleanableTarget : MonoBehaviour
    {
        public CleanableKind kind = CleanableKind.SmallTrash;
        public string displayName = "쓰레기";
        public float maxHp = 1.0f;
        public int rewardMoney = 8;
        public int bagCost = 1;
        public float cleanWeight = 1.0f;

        private float _hp;
        private bool _isCleaned;
        private bool _isSuckingAway;
        private Transform _suckTarget;
        private Vector3 _initialScale;
        private Vector3 _initialPos;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _highlighted;

        public bool IsCleaned => _isCleaned || _isSuckingAway;
        public float CleanProgress01 => maxHp > 0.001f ? Mathf.Clamp01(1f - (_hp / maxHp)) : 0f;

        private void Awake()
        {
            _hp = maxHp;
            _initialScale = transform.localScale;
            _initialPos = transform.position;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on || _isCleaned) return;
            _highlighted = on;
            Color em = on ? new Color(0.28f, 0.35f, 0.14f) : Color.black;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", em);
                _renderers[i].SetPropertyBlock(_mpb);
                // Ensure emission keyword is active on URP Lit
                if (on && _renderers[i].sharedMaterial != null)
                {
                    _renderers[i].sharedMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        /// <summary>
        /// 청소기/맨손/걸레로 데미지 적용. 완료 시 true 반환.
        /// </summary>
        public bool ApplyCleanDamage(float damage, Transform nozzleTransform)
        {
            if (_isCleaned || _isSuckingAway) return false;

            _hp -= damage;

            if (kind == CleanableKind.TrashPile)
            {
                float ratio = Mathf.Clamp01(_hp / maxHp);
                float visualScale = Mathf.Lerp(0.32f, 1.0f, ratio);
                transform.localScale = _initialScale * visualScale;
                // 미세한 흡입 진동 손맛
                Vector3 jitter = new Vector3(
                    Mathf.Sin(Time.time * 48f) * 0.018f,
                    0f,
                    Mathf.Cos(Time.time * 52f) * 0.018f
                );
                transform.position = _initialPos + jitter;
            }
            else if (kind == CleanableKind.FloorStain)
            {
                float ratio = Mathf.Clamp01(_hp / maxHp);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Lerp(0.08f, 1f, ratio)));
                    _renderers[i].SetPropertyBlock(_mpb);
                }
            }
            else if (kind == CleanableKind.SmallTrash)
            {
                float ratio = Mathf.Clamp01(_hp / maxHp);
                transform.localScale = _initialScale * Mathf.Lerp(0.75f, 1.0f, ratio);
            }

            if (_hp <= 0f)
            {
                if (kind == CleanableKind.SmallTrash && nozzleTransform != null)
                {
                    _isSuckingAway = true;
                    _suckTarget = nozzleTransform;
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

        public void AutoCleanInstant()
        {
            if (_isCleaned) return;
            _hp = 0f;
            FinishClean();
        }

        public void ResetForNewRun()
        {
            _isCleaned = false;
            _isSuckingAway = false;
            _hp = maxHp;
            transform.localScale = _initialScale;
            transform.position = _initialPos;
            gameObject.SetActive(true);
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
            if (!_isSuckingAway) return;

            if (_suckTarget == null)
            {
                FinishClean();
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, _suckTarget.position, 9.5f * Time.deltaTime);
            transform.Rotate(Vector3.one * (420f * Time.deltaTime));
            transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.zero, 4.2f * Time.deltaTime);

            if (Vector3.Distance(transform.position, _suckTarget.position) < 0.18f || transform.localScale.x < 0.04f)
            {
                FinishClean();
            }
        }

        private void FinishClean()
        {
            _isCleaned = true;
            _isSuckingAway = false;
            SetHighlighted(false);
            gameObject.SetActive(false);
        }
    }
}
