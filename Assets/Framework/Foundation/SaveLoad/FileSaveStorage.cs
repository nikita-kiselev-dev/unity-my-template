using System.IO;
using Cysharp.Threading.Tasks;
using Framework.Foundation.File;
using VContainer;

namespace Framework.Foundation.SaveLoad
{
    public sealed class FileSaveStorage : ISaveStorage
    {
        [Inject] private readonly IFileService _fileService;
        private readonly object _writeLock = new();
        private bool _immediateWriteStarted;

        public string Description => SaveLoadConstants.SaveFilePath;

        public async UniTask<SaveReadResult> TryReadAsync()
        {
            return await UniTask.RunOnThreadPool(() =>
            {
                EnsureDirectory();

                if (!System.IO.File.Exists(SaveLoadConstants.SaveFilePath))
                {
                    return SaveReadResult.Empty();
                }

                var bytes = _fileService.Load<byte[]>(SaveLoadConstants.SaveFilePath);
                return bytes is { Length: > 0 }
                    ? SaveReadResult.Success(bytes)
                    : SaveReadResult.Corrupted();
            });
        }

        public async UniTask WriteAsync(byte[] bytes)
        {
            await UniTask.RunOnThreadPool(() =>
            {
                WriteAsyncInternal(bytes);
            });
        }

        public void Write(byte[] bytes)
        {
            lock (_writeLock)
            {
                _immediateWriteStarted = true;
                WriteInternal(bytes);
            }
        }

        private void WriteAsyncInternal(byte[] bytes)
        {
            lock (_writeLock)
            {
                if (_immediateWriteStarted)
                {
                    return;
                }

                WriteInternal(bytes);
            }
        }

        private void WriteInternal(byte[] bytes)
        {
            EnsureDirectory();
            _fileService.Save<byte[]>(SaveLoadConstants.SaveFilePath, bytes);
        }

        public UniTask QuarantineAsync()
        {
            var corruptedPath = SaveLoadConstants.SaveFilePath + ".corrupted";
            System.IO.File.Delete(corruptedPath);
            System.IO.File.Move(SaveLoadConstants.SaveFilePath, corruptedPath);
            return UniTask.CompletedTask;
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SaveLoadConstants.SaveFileDirectory))
            {
                Directory.CreateDirectory(SaveLoadConstants.SaveFileDirectory);
            }
        }
    }
}
