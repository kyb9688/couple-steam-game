using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 밖에서 [F] 키로 직접 열고 들어가는 현관문 (열릴 때 방 안 1,000개 쓰레기가 극적으로 공개!).
    /// 마우스를 올리면 하얀색 테두리 아웃라인 표시.
    /// </summary>
    public class InteractiveDoor : MonoBehaviour
    {
        public Transform hingePivot;
        public Collider doorCollider;
        public float closedAngleY = 0f;
        public float openAngleY = -98f;
        public float openSpeed = 3.2f;

        public bool IsOpen { get; private set; }
        public bool HasOpenedOnce { get; private set; }

        private float _open01;
        private GameObject _outlineRoot;
        private bool _highlighted;

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

            Material outlineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            outlineMat.SetColor("_BaseColor", Color.white);
            outlineMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);

            _outlineRoot = new GameObject("_WhiteOutline");
            _outlineRoot.transform.SetParent(hingePivot, false);
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                GameObject copy = new GameObject(mf.name + "_Outline");
                copy.transform.SetParent(_outlineRoot.transform, false);
                copy.transform.position = mf.transform.position;
                copy.transform.rotation = mf.transform.rotation;
                copy.transform.localScale = Vector3.Scale(mf.transform.lossyScale, new Vector3(1.035f, 1.02f, 1.35f));
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
            IsOpen = !IsOpen;
            if (IsOpen) HasOpenedOnce = true;
            if (sfx != null) sfx.PlayPop(IsOpen ? 0.55f : 0.42f);
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
