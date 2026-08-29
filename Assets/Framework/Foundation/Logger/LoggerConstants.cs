namespace Framework.Foundation.Logger
{
    public static class LoggerConstants
    {
        public static class Colors
        {
            public const string System = "#4CA57D";
            public const string Feature = "#3D77FF";
            public const string Error = "#CE342A";
        }

        public static class Formats
        {
            public const string Default = "[<color={0}>{1}</color>]: {2}";
            public const string JustColor = "<color={0}>{1}</color>";
        }
    }
}