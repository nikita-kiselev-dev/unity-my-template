using System;
using System.Collections.Generic;
using Framework.Foundation.SaveLoad;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeSaveEnvelope : ISaveEnvelope
    {
        public byte[] SerializedBytes = { 1, 2, 3 };
        public bool DeserializeThrows;

        public int SerializeCount { get; private set; }
        public int PrepareNewDataCount { get; private set; }
        public List<byte[]> DeserializedBytes { get; } = new();

        public byte[] Serialize()
        {
            SerializeCount++;
            return SerializedBytes;
        }

        public void Deserialize(ReadOnlySpan<byte> bytes)
        {
            if (DeserializeThrows)
            {
                throw new InvalidOperationException("Corrupted save.");
            }

            DeserializedBytes.Add(bytes.ToArray());
        }

        public void PrepareNewData() => PrepareNewDataCount++;
    }
}
