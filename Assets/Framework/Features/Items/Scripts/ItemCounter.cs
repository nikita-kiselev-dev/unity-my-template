using System.Numerics;
using Framework.Features.Items.Data;
using R3;
using UnityEngine;

namespace Framework.Features.Items
{
    public sealed class ItemCounter : IItemCounter
    {
        private readonly ItemsData _itemsData;
        private readonly ReactiveProperty<BigInteger> _value;

        public ItemInfo Info { get; }

        public ItemCounter(
            ItemsData itemsData,
            string key,
            string name,
            string description,
            Sprite icon)
        {
            _itemsData = itemsData;
            _value = new ReactiveProperty<BigInteger>(itemsData.GetValue(key).Value);
            Info = new ItemInfo(key, name, description, icon, _value);
        }

        public bool Add(BigInteger itemCount)
        {
            if (!_itemsData.AddItem(Info.Key, itemCount))
            {
                return false;
            }

            SyncValue();
            return true;
        }

        public bool Remove(BigInteger itemCount)
        {
            if (!_itemsData.RemoveItem(Info.Key, itemCount))
            {
                return false;
            }

            SyncValue();
            return true;
        }

        private void SyncValue()
        {
            _value.Value = _itemsData.GetValue(Info.Key).Value;
        }
    }
}
