namespace TrashCleaner3D
{
    /// <summary>
    /// v1.0 코어 루프 집중 스위치 — '무더기 밀기 → 트럭에 와르르 환전 → 트럭 옆 현장 강화'.
    ///
    /// CoreLoopOnly = true 이면 코어 손맛을 가리던 부가 기믹이 전부 꺼진다:
    ///   · 동종 연속 압축(5개/3개 → 수거통 비움 ×2)
    ///   · 의심 게이지 · 80~95% 아드레날린 스릴 구간
    ///   · 막판 10% 잭팟
    ///   · 보물 자동 발견 → [Q] 전달 / [E] 슈킹 · 신뢰도(★) · 암시장(♦) 분기
    /// false 로 되돌리면 v0.9.3 규칙이 그대로 살아난다(코드는 지우지 않고 분기만 했으므로 한 줄로 복구).
    ///
    /// 악취·환기는 방 안/밖을 오가는 공간 루프의 템포 장치라 기본 켜둔다.
    /// </summary>
    public static class CoreLoopConfig
    {
        public const bool CoreLoopOnly = true;

        public const bool EnableStreakCompression = !CoreLoopOnly;
        public const bool EnableSuspicionBranch = !CoreLoopOnly;
        public const bool EnableLast10Jackpot = !CoreLoopOnly;
        public const bool EnableTreasureBranch = !CoreLoopOnly;
        public const bool EnableStenchGimmick = true;

        /// <summary>수거음(도레미파솔)이 한 옥타브를 다 올라간 뒤 다시 '도'로 돌아가는 기준 계단 수.</summary>
        public const int MelodyTopStep = 7;

        /// <summary>트럭에 정산한 순간 돈이 드르르륵 올라가는 기본 연출 시간(초).</summary>
        public const float CashCrunchBaseDuration = 0.55f;
        public const float CashCrunchMaxDuration = 1.25f;
    }
}
