using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Foundation.SaveLoad;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeSaveStorage : ISaveStorage
    {
        public SaveReadResult ReadResult = SaveReadResult.Empty();
        public bool CompleteWritesImmediately = true;

        public List<byte[]> Writes { get; } = new();
        public List<byte[]> ImmediateWrites { get; } = new();
        public int QuarantineCount { get; private set; }

        private readonly Queue<UniTaskCompletionSource> _pendingWrites = new();

        public string Description => nameof(FakeSaveStorage);

        public UniTask<SaveReadResult> TryReadAsync() => UniTask.FromResult(ReadResult);

        public UniTask WriteAsync(byte[] bytes)
        {
            Writes.Add(bytes);

            if (CompleteWritesImmediately)
            {
                return UniTask.CompletedTask;
            }

            var pendingWrite = new UniTaskCompletionSource();
            _pendingWrites.Enqueue(pendingWrite);
            return pendingWrite.Task;
        }

        public void CompleteWrite() => _pendingWrites.Dequeue().TrySetResult();

        public void Write(byte[] bytes) => ImmediateWrites.Add(bytes);

        public UniTask QuarantineAsync()
        {
            QuarantineCount++;
            return UniTask.CompletedTask;
        }
    }
}
