using System;

namespace Framework.Foundation.SaveLoad
{
    public enum SaveReadStatus
    {
        Empty,
        Success,
        Corrupted
    }

    public readonly struct SaveReadResult
    {
        public SaveReadStatus Status { get; }

        // ReadOnlyMemory, а не byte[]: payload читается один раз в ISaveEnvelope.Deserialize
        // (ReadOnlySpan), поэтому окно поверх исходного массива обходится без копии.
        public ReadOnlyMemory<byte> Bytes { get; }

        private SaveReadResult(SaveReadStatus status, ReadOnlyMemory<byte> bytes)
        {
            Status = status;
            Bytes = bytes;
        }

        public static SaveReadResult Empty() => new(SaveReadStatus.Empty, ReadOnlyMemory<byte>.Empty);

        public static SaveReadResult Success(ReadOnlyMemory<byte> bytes) => new(SaveReadStatus.Success, bytes);

        public static SaveReadResult Corrupted() => new(SaveReadStatus.Corrupted, ReadOnlyMemory<byte>.Empty);
    }
}
