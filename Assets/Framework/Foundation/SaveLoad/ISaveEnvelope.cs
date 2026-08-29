using System;

namespace Framework.Foundation.SaveLoad
{
    public interface ISaveEnvelope
    {
        byte[] Serialize();
        void Deserialize(ReadOnlySpan<byte> bytes);
        void PrepareNewData();
    }
}
