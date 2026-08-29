using System.Numerics;

namespace Framework.Features.Items
{
    public interface IItemCounter
    {
        ItemInfo Info { get; }
        bool Add(BigInteger itemCount);
        bool Remove(BigInteger itemCount);
    }
}