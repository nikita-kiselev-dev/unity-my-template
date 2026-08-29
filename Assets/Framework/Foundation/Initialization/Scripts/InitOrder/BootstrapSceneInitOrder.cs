namespace Framework.Foundation.Initialization.InitOrder
{
    public enum BootstrapSceneInitOrder
    {
        First,
        SaveLoadService,
        AdsController,
        LocalizationController,
        Inventory,
        Last
    }
}