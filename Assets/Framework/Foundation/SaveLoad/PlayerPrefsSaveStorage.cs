using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.Foundation.SaveLoad
{
    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        private bool _immediateWriteStarted;

        public string Description => $"PlayerPrefs[{SaveLoadConstants.SaveFileName}]";

        public UniTask<SaveReadResult> TryReadAsync()
        {
            var encoded = PlayerPrefs.GetString(SaveLoadConstants.SaveFileName);
            return UniTask.FromResult(Decode(encoded));
        }

        internal static SaveReadResult Decode(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
            {
                return SaveReadResult.Empty();
            }

            try
            {
                var bytes = Convert.FromBase64String(encoded);
                return SaveReadResult.Success(bytes);
            }
            catch (FormatException)
            {
                return SaveReadResult.Corrupted();
            }
        }

        public UniTask WriteAsync(byte[] bytes)
        {
            // Финальная запись уже началась (quit/pause) — отложенный payload устарел.
            if (_immediateWriteStarted)
            {
                return UniTask.CompletedTask;
            }

            WriteInternal(bytes);
            return UniTask.CompletedTask;
        }

        public void Write(byte[] bytes)
        {
            _immediateWriteStarted = true;
            WriteInternal(bytes);
        }

        public UniTask QuarantineAsync()
        {
            var encoded = PlayerPrefs.GetString(SaveLoadConstants.SaveFileName);
            PlayerPrefs.SetString($"{SaveLoadConstants.SaveFileName}.corrupted", encoded);
            PlayerPrefs.DeleteKey(SaveLoadConstants.SaveFileName);
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        // Без Save() запись живёт только в памяти: kill из фона теряет прогресс, потому что
        // PlayerPrefs сбрасываются на диск сами лишь на OnApplicationQuit.
        private static void WriteInternal(byte[] bytes)
        {
            var encoded = Convert.ToBase64String(bytes);
            PlayerPrefs.SetString(SaveLoadConstants.SaveFileName, encoded);
            PlayerPrefs.Save();
        }
    }
}
