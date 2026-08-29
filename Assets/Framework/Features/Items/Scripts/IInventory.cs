namespace Framework.Features.Items
{
    public interface IInventory
    {
        bool TryGetCounter(string key, out IItemCounter itemController);
        bool Add(ItemOperation config);
        bool Remove(ItemOperation config);
        bool IsEnough(ItemOperation config);
    }
}