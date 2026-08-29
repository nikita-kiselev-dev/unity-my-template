namespace Framework.Features.DailyBonus.Model
{
    public readonly struct DailyBonusDecision
    {
        public bool ShouldShowPopup { get; }
        public StreakUpdate StreakUpdate { get; }

        public DailyBonusDecision(bool shouldShowPopup, StreakUpdate streakUpdate)
        {
            ShouldShowPopup = shouldShowPopup;
            StreakUpdate = streakUpdate;
        }
    }
}
