namespace Framework.Foundation.Asset
{
    // Единственная зависимость фичи по ассетам: она получает свой scope и не видит ни persistent,
    // ни ReleaseAsset — то есть не может забыть релиз и не может выключить его флагом.
    public interface IAssetScopeFactory
    {
        IAssetScope CreateScope();
    }
}
