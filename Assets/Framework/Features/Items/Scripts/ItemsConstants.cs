namespace Framework.Features.Items
{
    public static class ItemsConstants
    {
        public const string LogName = "Items";
        public const string MainCurrencyKey = "dollar";
        
        public static class Configs
        {
            public const string Currencies = "CurrenciesConfig";
        }

        public static class Localization
        {
            public const string Currencies = "currencies";
            public const string Items = "items";
        }
        
        public static class Formats
        {
            public const string Name = "{0}_name";
            public const string Description = "{0}_description";
        }

        public static class RawParameters
        {
            public const string ID = "id";
            public const string Key = "key";
            public const string LocalizationTableKey = "localization_table_key";
            public const string NameKey = "name_key";
            public const string DescriptionKey = "description_key";
            public const string IconKey = "icon_key";
            public const string AtlasKey = "atlas_key";
        }
    }
}