using UnityEngine;

namespace TrashCleaner3D
{
    /// <summary>
    /// 레거시 호환 스텁 (v0.9부터 보물은 큰 봉투/쓰레기 수거 시 확률 자동 팝업으로 전환됨)
    /// </summary>
    public class BuriedTreasureObject : MonoBehaviour
    {
        public string itemName = "";
        public int baseValue = 0;
        public TreasureRarity rarity = TreasureRarity.Rare;
        public bool isMentionedByClient = false;
        public float baseSuspicionGain = 18f;
        public int coverThreshold = 4;
        public float coverCheckRadius = 0.72f;
        public GamePrototypeManager manager;

        private void Awake()
        {
            gameObject.SetActive(false);
        }
    }
}
