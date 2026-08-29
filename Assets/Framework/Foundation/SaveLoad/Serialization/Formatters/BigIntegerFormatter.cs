using System.Numerics;
using MemoryPack;

namespace Framework.Foundation.SaveLoad.Serialization.Formatters
{
    public class BigIntegerFormatter : MemoryPackFormatter<BigInteger>
    {
        public override void Serialize<TBufferWriter>(
            ref MemoryPackWriter<TBufferWriter> writer, 
            ref BigInteger value)
        {
            var bytes = value.ToByteArray();
            writer.WriteValue(bytes);
        }

        public override void Deserialize(
            ref MemoryPackReader reader, 
            ref BigInteger value)
        {
            var bytes = reader.ReadValue<byte[]>();
            value = new BigInteger(bytes);
        }
    }
}