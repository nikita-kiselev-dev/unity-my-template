using System.Collections.Generic;
using System.Numerics;
using Framework.Features.SaveLoad;
using Framework.Foundation.SaveLoad;
using Framework.Foundation.Utilities;
using MemoryPack;

namespace Framework.Features.Items.Data
{
    [SaveTag(FeaturesSaveTags.ItemsData)]
    [MemoryPackable]
    public partial class ItemsData : SaveBlob
    {
        [MemoryPackInclude]
        internal Dictionary<string, BigInteger> Items { get; set; }

        public override void PrepareNewData()
        {
            Items = new Dictionary<string, BigInteger>();
        }

        internal Result<BigInteger> GetValue(string itemKey)
        {
            return !Items.TryGetValue(itemKey, out var currentItemCount) ?
                Result<BigInteger>.Failure() :
                Result<BigInteger>.Success(currentItemCount);
        }

        internal void AddNewItem(string itemKey)
        {
            if (!Items.ContainsKey(itemKey))
            {
                Items.Add(itemKey, BigInteger.Zero);
            }
        }

        internal bool AddItem(string itemKey, BigInteger itemCount)
        {
            if (itemCount <= 0 || !Items.TryGetValue(itemKey, out var currentItemCount))
            {
                return false;
            }

            Items[itemKey] = currentItemCount + itemCount;
            return true;
        }

        internal bool RemoveItem(string itemKey, BigInteger itemCount)
        {
            if (itemCount <= 0 || !Items.TryGetValue(itemKey, out var currentItemCount))
            {
                return false;
            }

            var calculationResult = currentItemCount - itemCount;

            if (calculationResult < 0)
            {
                return false;
            }

            Items[itemKey] = calculationResult;
            return true;
        }
    }
}
