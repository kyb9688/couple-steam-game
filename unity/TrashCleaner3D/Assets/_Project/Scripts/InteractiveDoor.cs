using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 밖에서 [F] 키로 직접 열고 들어가는 현관문 (열릴 때 방 안 쓰레기가 극적으로 공개!).
    /// v1.2: 라운드 시작 시 매니저가 자동으로 열어 두고(트럭 왕복 동선 확보), 청소 중에는 닫히지 않는다.
    /// 마우스를 올리면 하얀색 테두리 아웃라인 표시.
    /// </summary>
    public class InteractiveDoor : MonoBehaviour
    {
        public Transform hingePivot;
        public Collider doorCollider;
        public float closedAngleY = 0f;
        public float openAngleY = -98f;
        public float openSpeed = 3.2f;
        /// <summary>문 앞에 플레이어가 있을 때 닫기를 막는 안전장치 (끼임/동선 차단 방지)</summary>
        public bool blockCloseWhenPlayerNear = true;
        public GamePrototypeManager manager;

        public bool IsOpen { get; private set; }
        public bool HasOpenedOnce { get; private set; }

        private float _open01;
        private GameObject _outlineRoot;
        private bool _highlighted;
        private FirstPersonCleanerController _player;

        private void Awake()
        {
            if (hingePivot == null) hingePivot = transform;
            if (doorCollider == null) doorCollider = GetComponentInChildren<Collider>();
            BuildWhiteOutline();
        }

        private void BuildWhiteOutline()
        {
            var mfs = GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return;

            Shader sh = Shader.Find("TrashCleaner3D/WhiteOutlineInstanced") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material outlineMat = new Material(sh);
            outlineMat.SetColor("_OutlineColor", Color.white);
            outlineMat.SetFloat("_OutlineWidth", 0.022f);

            _outlineRoot = new GameObject("_WhiteOutline");
            _outlineRoot.transform.SetParent(hingePivot, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_Outline");
                copy.transform.SetParent(_outlineRoot.transform, false);
                copy.transform.position = mf.transform.position;
                copy.transform.rotation = mf.transform.rotation;
                copy.transform.localScale = mf.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = copy.AddComponent<MeshRenderer>();
                mr.sharedMaterial = outlineMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            _outlineRoot.SetActive(false);
        }

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;
            if (_outlineRoot != null) _outlineRoot.SetActive(on);
        }

        public void ToggleDoor(ProceduralSfx sfx = null)
        {
            SetOpen(!IsOpen, sfx);
        }

        /// <summary>
        /// 스테이지 시작 시 문을 조건 없이 즉시 닫힌 각도로 스냅한다.
        /// </summary>
        public void ForceCloseImmediate()
        {
            IsOpen = false;
            _open01 = 0f;
            if (hingePivot != null)
            {
                Vector3 e = hingePivot.localEulerAngles;
                hingePivot.localEulerAngles = new Vector3(e.x, closedAngleY, e.z);
            }
            if (doorCollider != null) doorCollider.enabled = true;
        }

        /// <summary>
        /// [v2.0] 문을 열거나 닫는다. 청소 중에도 문 뒤 쓰레기봉지 청소를 위해 언제든 자유롭게 닫고 열 수 있다.
        /// </summary>
        public void SetOpen(bool open, ProceduralSfx sfx = null)
        {
            if (IsOpen == open) return;

            if (!open && blockCloseWhenPlayerNear && IsPlayerAtDoorway())
            {
                if (manager != null) manager.ShowToast("문틀 바로 앞에서는 닫히지 않아요 · 한 걸음 비켜서 [F]");
                return;
            }

            IsOpen = open;
            if (open) HasOpenedOnce = true;
            if (sfx != null) sfx.PlayPop(open ? 0.55f : 0.42f);
        }

        private bool IsCleaningRound()
        {
            return manager != null &&
                   (manager.Phase == GamePhase.Phase1_TrashRemoval || manager.Phase == GamePhase.Phase2_FloorMopping);
        }

        private bool IsPlayerAtDoorway()
        {
            if (_player == null) _player = FindFirstObjectByType<FirstPersonCleanerController>();
            if (_player == null || hingePivot == null) return false;
            Vector3 d = _player.transform.position - hingePivot.position;
            d.y = 0f;
            return d.sqrMagnitude <= 0.62f * 0.62f;
        }

        private void Update()
        {
            float target = IsOpen ? 1f : 0f;
            if (!Mathf.Approximately(_open01, target))
            {
                _open01 = Mathf.MoveTowards(_open01, target, openSpeed * Time.deltaTime);
                float eased = _open01 * _open01 * (3f - 2f * _open01);
                float angle = Mathf.Lerp(closedAngleY, openAngleY, eased);
                hingePivot.localRotation = Quaternion.Euler(0f, angle, 0f);

                // 문이 열리는 동안 플레이어가 밀리지 않도록 콜라이더 임시 해제
                if (doorCollider != null)
                {
                    doorCollider.enabled = (_open01 <= 0.02f || _open01 >= 0.98f);
                }
            }
        }
    }
}
