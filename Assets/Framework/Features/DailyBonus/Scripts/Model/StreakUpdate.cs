namespace Framework.Features.DailyBonus.Model
{
    public readonly struct StreakUpdate
    {
        public bool StreakChanged { get; }
        public bool StreakLost { get; }
        public int LostStreakDay { get; }

        private StreakUpdate(bool streakChanged, bool streakLost, int lostStreakDay)
        {
            StreakChanged = streakChanged;
            StreakLost = streakLost;
            LostStreakDay = lostStreakDay;
        }

        public static StreakUpdate None => default;
        public static StreakUpdate Restarted => new(true, false, 0);
        public static StreakUpdate Lost(int lostStreakDay) => new(true, true, lostStreakDay);
    }
}
