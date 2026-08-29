using System.Collections.Generic;
using Framework.Features.Items;

namespace Framework.Features.Tests.Fakes
{
    public class FakeInventory : IInventory
    {
        public bool AddResult { get; set; } = true;
        public bool RemoveResult { get; set; } = true;
        public bool IsEnoughResult { get; set; } = true;

        public List<ItemOperation> Added { get; } = new();
        public List<ItemOperation> Removed { get; } = new();

        public bool TryGetCounter(string key, out IItemCounter itemController)
        {
            itemController = null;
            return false;
        }

        public bool Add(ItemOperation config)
        {
            Added.Add(config);
            return AddResult;
        }

        public bool Remove(ItemOperation config)
        {
            Removed.Add(config);
            return RemoveResult;
        }

        public bool IsEnough(ItemOperation config) => IsEnoughResult;
    }
}
