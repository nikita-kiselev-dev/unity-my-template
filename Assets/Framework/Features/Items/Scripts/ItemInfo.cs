using System.Numerics;
using R3;
using UnityEngine;

namespace Framework.Features.Items
{
    public class ItemInfo
    {
        public string Key { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public Sprite Icon { get; private set; }
        public ReadOnlyReactiveProperty<BigInteger> Value { get; private set; }

        public ItemInfo(
            string key, 
            string name, 
            string description, 
            Sprite icon,
            ReadOnlyReactiveProperty<BigInteger> value)
        {
            Key = key;
            Name = name;
            Description = description;
            Icon = icon;
            Value = value;
        }
    }
}
