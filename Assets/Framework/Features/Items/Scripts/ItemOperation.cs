using System.Numerics;

namespace Framework.Features.Items
{
    public class ItemOperation
    {
        public string Key { get; }
        public BigInteger Value { get; }
        
        public ItemOperation(string key, BigInteger value)
        {
            Key = key;
            Value = value;
        }

        public ItemOperation(BigInteger value)
        {
            Key = ItemsConstants.MainCurrencyKey;
            Value = value;
        }
        
        public ItemOperation()
        {
            Key = ItemsConstants.MainCurrencyKey;
            Value = BigInteger.One;
        }
    }
}