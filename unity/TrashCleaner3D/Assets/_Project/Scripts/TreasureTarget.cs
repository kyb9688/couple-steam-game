using UnityEngine;

namespace TrashCleaner3D
{
    public enum TreasureState
    {
        HiddenInRoom,
        ReturnedToClient,
        StolenByPlayer
    }

    /// <summary>
    /// 쓰레기 산 속 숨겨진 보물 또는 의뢰인 지정 분실물(슈킹 시 100% 발각).
    /// </summary>
    public class TreasureTarget : MonoBehaviour
    {
        public string itemName = "숨겨진 보물";
        public int value = 100;
        public bool isDesignatedLostItem = false;

        public TreasureState State { get; private set; } = TreasureState.HiddenInRoom;

        private Vector3 _initialPos;
        private Quaternion _initialRot;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _mpb;
        private bool _highlighted;

        private void Awake()
        {
            _initialPos = transform.position;
            _initialRot = transform.rotation;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _mpb = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r != null && r.sharedMaterial != null)
                {
                    r.sharedMaterial.EnableKeyword("_EMISSION");
                }
            }
        }

        private void Update()
        {
            if (State != TreasureState.HiddenInRoom) return;

            // 은은한 황금빛 반짝임 (지정 분실물은 푸른/청록빛 펄스)
            float pulse = 0.18f + 0.18f * Mathf.Sin(Time.time * 3.5f);
            if (_highlighted) pulse += 0.35f;

            Color glow = isDesignatedLostItem
                ? new Color(0.15f, 0.72f, 0.95f) * pulse
                : new Color(0.95f, 0.72f, 0.12f) * pulse;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", glow);
                _renderers[i].SetPropertyBlock(_mpb);
            }
        }

        public void SetHighlighted(bool on)
        {
            _highlighted = on;
        }

        public void MarkReturned(Transform returnShelfSlot)
        {
            State = TreasureState.ReturnedToClient;
            _highlighted = false;
            foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
            if (returnShelfSlot != null)
            {
                transform.position = returnShelfSlot.position;
                transform.rotation = Quaternion.identity;
                // 살짝 진열된 느낌으로 유지
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].GetPropertyBlock(_mpb);
                    _mpb.SetColor("_EmissionColor", Color.black);
                    _renderers[i].SetPropertyBlock(_mpb);
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
            gameObject.SetActive(false);
        }

        public void ResetForNewRun()
        {
            State = TreasureState.HiddenInRoom;
            _highlighted = false;
            transform.position = _initialPos;
            transform.rotation = _initialRot;
            gameObject.SetActive(true);
            foreach (var col in GetComponentsInChildren<Collider>(true)) col.enabled = true;
        }
    }
}
