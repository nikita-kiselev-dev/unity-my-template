using Framework.Foundation.SaveLoad;

namespace Framework.Features.SaveLoad
{
    /// <summary>Save tags reserved for Features-side <see cref="SaveBlob"/> subclasses. Range: 100..199. Next free: 104.</summary>
    public static class FeaturesSaveTags
    {
        public const ushort SettingsData = 100;
        public const ushort DailyBonusData    = 101;
        public const ushort ClickerData       = 102;

        // Переехал из FoundationSaveTags (тег 1) вместе с фичей: номер сменился, старый сейв
        // предметов не читается и блоб штатно получает PrepareNewData по неизвестному тегу.
        public const ushort ItemsData         = 103;
    }
}
