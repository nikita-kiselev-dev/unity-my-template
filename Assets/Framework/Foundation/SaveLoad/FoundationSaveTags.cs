namespace Framework.Foundation.SaveLoad
{
    /// <summary>Save tags reserved for Foundation <see cref="SaveBlob"/> subclasses. Range: 1..99. Next free: 3.</summary>
    public static class FoundationSaveTags
    {
        // 1 занимал ItemsData, уехавший в Features. Значение не переиспользуем:
        // старый сейв всё ещё несёт тег 1, и новый блоб под этим номером прочитал бы чужой payload.
        public const ushort AdsData = 2;
    }
}
