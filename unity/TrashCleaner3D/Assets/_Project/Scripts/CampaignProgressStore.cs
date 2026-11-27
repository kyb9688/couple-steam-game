using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashCleaner3D
{
    public enum StageUnlockState
    {
        Locked,
        Available,
        Cleared
    }

    public struct StageCampaignInfo
    {
        public int stageNumber;
        public string sceneName;
        public string shortName;
        public string title;
        public string sizeLabel;
        public string trashLabel;
        public string cctvLabel;
        public string clientMemo;
        public string thumbResourcePath;
        public Color themeColor;
    }

    /// <summary>
    /// Stage 1~7 캠페인 진행 상태(클리어·해금·별점) 및 씬 전환 시 플레이어 보유 현금·강화 유지 스토어.
    /// - 시작(Play) 시 OpenMindMapOnNextLoad = true → 7종 스테이지 마인드맵 즉시 노출!
    /// - Stage N 클리어 → Stage N+1 순차 해금 (PlayerPrefs 저장 + 테스트 전체해금 토글 지원)
    /// </summary>
    public static class CampaignProgressStore
    {
        public const int TotalStages = 7;

        public static readonly StageCampaignInfo[] Stages =
        {
            new StageCampaignInfo
            {
                stageNumber = 1,
                sceneName = "Room01_PlayablePrototype",
                shortName = "반지하 원룸",
                title = "직장인 반지하 원룸",
                sizeLabel = "9.4m × 7.8m · 원룸",
                trashLabel = "쓰레기 1,650개",
                cctvLabel = "CCTV 없음",
                clientMemo = "야근하다 방이 이렇게 됐네요... 침대 밑 엄마가 주신 금반지만 꼭 선반에 놔주세요!",
                thumbResourcePath = "StageThumbs/stage_01",
                themeColor = new Color(0.84f, 0.64f, 0.24f)
            },
            new StageCampaignInfo
            {
                stageNumber = 2,
                sceneName = "Stage02_Layout_Eunju",
                shortName = "택배 오피스텔",
                title = "택배 중독 오피스텔",
                sizeLabel = "4.2m × 6.8m · 복도형",
                trashLabel = "쓰레기 1,800개",
                cctvLabel = "펫캠 1대",
                clientMemo = "택배 뜯을 힘도 없어요... 책상 근처 강아지 은색 인식표 꼭 챙겨주세요!",
                thumbResourcePath = "StageThumbs/stage_02",
                themeColor = new Color(0.76f, 0.56f, 0.36f)
            },
            new StageCampaignInfo
            {
                stageNumber = 3,
                sceneName = "Stage03_Layout_Eunju",
                shortName = "치킨집 주방",
                title = "치킨집 주방 대청소",
                sizeLabel = "5.5m × 5.0m · ㄷ자 주방",
                trashLabel = "쓰레기 2,200개",
                cctvLabel = "주방 CCTV 1대",
                clientMemo = "장사 접기 전에 위생 점검 때문에요. 카운터 개업 첫날 받은 행운의 2달러만 남겨주세요!",
                thumbResourcePath = "StageThumbs/stage_03",
                themeColor = new Color(0.88f, 0.48f, 0.18f)
            },
            new StageCampaignInfo
            {
                stageNumber = 4,
                sceneName = "Stage04_Layout_Eunju",
                shortName = "맞벌이 육아집",
                title = "육아 전쟁 맞벌이 부부 집",
                sizeLabel = "7.2m × 5.8m · 거실·안방·욕실",
                trashLabel = "쓰레기 3,000개",
                cctvLabel = "베이비캠 1대",
                clientMemo = "둘 다 야근이라 집이 난장판이네요... 거실장 위 아기 첫 돌 금반지만 꼭 챙겨주세요!",
                thumbResourcePath = "StageThumbs/stage_04",
                themeColor = new Color(0.86f, 0.44f, 0.50f)
            },
            new StageCampaignInfo
            {
                stageNumber = 5,
                sceneName = "Stage05_Layout_Eunju",
                shortName = "스타트업 사무실",
                title = "야근 지옥 스타트업 사무실",
                sizeLabel = "8.5m × 7.0m · 오피스·대표실·탕비실",
                trashLabel = "쓰레기 4,200개",
                cctvLabel = "교차 CCTV 2대",
                clientMemo = "투자사 실사 오기 전에 치워야 합니다! 대표실 법인 인감 도장은 절대 버리지 마세요!!",
                thumbResourcePath = "StageThumbs/stage_05",
                themeColor = new Color(0.30f, 0.54f, 0.80f)
            },
            new StageCampaignInfo
            {
                stageNumber = 6,
                sceneName = "Stage06_Layout_Eunju",
                shortName = "시골집 농막",
                title = "시골집 농막 청소 (최강 악취)",
                sizeLabel = "4.2m × 3.6m · 목조 농막",
                trashLabel = "쓰레기 2,400개 · 악취 ×1.6",
                cctvLabel = "처마 방범캠 1대",
                clientMemo = "오랜만에 내려왔더니 썩은 내가 진동하네... 구석 할아버지 회중시계만 찾아주게.",
                thumbResourcePath = "StageThumbs/stage_06",
                themeColor = new Color(0.42f, 0.62f, 0.30f)
            },
            new StageCampaignInfo
            {
                stageNumber = 7,
                sceneName = "Stage07_Layout_Eunju",
                shortName = "3층 펜트하우스",
                title = "최종 의뢰: 3층 복층 펜트하우스",
                sizeLabel = "10m × 8m × 3층 복층!",
                trashLabel = "쓰레기 6,000개 (1F·2F·3F)",
                cctvLabel = "고속 홈캠 2대",
                clientMemo = "파티 후 방치했더니 엉망이네요. 3층 금고 옆 스포츠카 차 키만 보관해 주세요. 수당 넉넉히 드립니다.",
                thumbResourcePath = "StageThumbs/stage_07",
                themeColor = new Color(0.92f, 0.72f, 0.16f)
            }
        };

        private const string PrefClearedMask = "TC3D_ClearedMask_v15";
        private const string PrefStarsPrefix = "TC3D_StageStars_";

        private static bool _initialized;
        private static int _clearedMask;
        private static readonly int[] _stageStars = new int[TotalStages + 1];

        /// <summary>Play 시작 시 바로 7종 스테이지 마인드맵을 띄울지 여부 (최초 실행 시 true!)</summary>
        public static bool OpenMindMapOnNextLoad = true;

        /// <summary>테스트/검수용 전체 스테이지 잠금 해제 토글 (기본 false = 순차 해금)</summary>
        public static bool DevUnlockAll = false;

        // ===== 씬 전환 간 플레이어 현금 · 28노드 강화 상태 보존 =====
        public static bool HasSavedPlayerState;
        public static int SavedMoney;
        public static int SavedTrustTokens;
        public static int SavedBlackMarketPts;
        public static int LvHold1, LvRad1, LvHold2, LvRad2, LvHold3, LvFever;
        public static bool HasBroom, HasVacuum;
        public static int LvBin1, LvPrice1, LvBin2, LvPrice2, LvBin3, LvComboTime;
        public static bool HasCart, HasDamas, HasTruck1Ton;
        public static int LvSpeed1, LvMask1, LvSpeed2, LvMask2, LvScrubPower;
        public static bool HasMopUnlocked;
        public static int LvFist1, LvRoombaCount, LvRoombaSpd1, LvFist2, LvRoombaSpd2;

        private static void EnsureLoaded()
        {
            if (_initialized) return;
            _initialized = true;
            _clearedMask = PlayerPrefs.GetInt(PrefClearedMask, 0);
            for (int s = 1; s <= TotalStages; s++)
            {
                _stageStars[s] = PlayerPrefs.GetInt(PrefStarsPrefix + s, 0);
            }
        }

        public static bool IsStageCleared(int stageNum)
        {
            EnsureLoaded();
            if (stageNum < 1 || stageNum > TotalStages) return false;
            return (_clearedMask & (1 << (stageNum - 1))) != 0;
        }

        public static int GetStageStars(int stageNum)
        {
            EnsureLoaded();
            if (stageNum < 1 || stageNum > TotalStages) return 0;
            return _stageStars[stageNum];
        }

        public static bool IsStageUnlocked(int stageNum)
        {
            EnsureLoaded();
            if (stageNum <= 1) return true;
            if (stageNum > TotalStages) return false;
            if (DevUnlockAll) return true;
            return IsStageCleared(stageNum - 1);
        }

        public static StageUnlockState GetStageState(int stageNum)
        {
            EnsureLoaded();
            if (IsStageCleared(stageNum)) return StageUnlockState.Cleared;
            if (IsStageUnlocked(stageNum)) return StageUnlockState.Available;
            return StageUnlockState.Locked;
        }

        public static int ClearedStageCount
        {
            get
            {
                EnsureLoaded();
                int c = 0;
                for (int s = 1; s <= TotalStages; s++) if (IsStageCleared(s)) c++;
                return c;
            }
        }

        public static void MarkStageCleared(int stageNum, int stars = 5)
        {
            EnsureLoaded();
            if (stageNum < 1 || stageNum > TotalStages) return;
            _clearedMask |= (1 << (stageNum - 1));
            _stageStars[stageNum] = Mathf.Max(_stageStars[stageNum], Mathf.Clamp(stars, 1, 5));
            PlayerPrefs.SetInt(PrefClearedMask, _clearedMask);
            PlayerPrefs.SetInt(PrefStarsPrefix + stageNum, _stageStars[stageNum]);
            PlayerPrefs.Save();
        }

        public static void ResetCampaignProgress()
        {
            _initialized = true;
            _clearedMask = 0;
            for (int s = 1; s <= TotalStages; s++)
            {
                _stageStars[s] = 0;
                PlayerPrefs.DeleteKey(PrefStarsPrefix + s);
            }
            PlayerPrefs.DeleteKey(PrefClearedMask);
            PlayerPrefs.Save();
            HasSavedPlayerState = false;
            SavedMoney = 0;
            SavedTrustTokens = 0;
            SavedBlackMarketPts = 0;
            LvHold1 = LvRad1 = LvHold2 = LvRad2 = LvHold3 = LvFever = 0;
            HasBroom = HasVacuum = false;
            LvBin1 = LvPrice1 = LvBin2 = LvPrice2 = LvBin3 = LvComboTime = 0;
            HasCart = HasDamas = HasTruck1Ton = false;
            LvSpeed1 = LvMask1 = LvSpeed2 = LvMask2 = LvScrubPower = 0;
            HasMopUnlocked = false;
            LvFist1 = LvRoombaCount = LvRoombaSpd1 = LvFist2 = LvRoombaSpd2 = 0;
        }

        public static StageCampaignInfo GetInfo(int stageNum)
        {
            int idx = Mathf.Clamp(stageNum - 1, 0, Stages.Length - 1);
            return Stages[idx];
        }

        public static int DetectStageFromActiveScene()
        {
            string name = SceneManager.GetActiveScene().name;
            for (int i = 0; i < Stages.Length; i++)
            {
                if (Stages[i].sceneName == name) return Stages[i].stageNumber;
            }
            return 1;
        }

        public static bool CanLoadStageScene(int stageNum)
        {
            var info = GetInfo(stageNum);
            return Application.CanStreamedLevelBeLoaded(info.sceneName);
        }
    }
}
