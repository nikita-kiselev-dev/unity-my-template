using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Foundation.Initialization.InitOrder;
using Framework.Foundation.Logger;
using Framework.Foundation.Scenes;
using Framework.Foundation.Utilities.Extensions;
using VContainer;

namespace Framework.Foundation.SaveLoad
{
    [LifecycleOrder(SceneConstants.Scenes.Bootstrap, (int)BootstrapSceneInitOrder.SaveLoadService)]
    [AutoLogger(nameof(SaveLoadService))]
    public sealed partial class SaveLoadService : LifecycleEntity, IDataSaver
    {
        [Inject] private readonly ISaveEnvelope _saveEnvelope;
        [Inject] private readonly ISaveStorage _storage;

        // [Inject] на этом ctor обязателен: рядом есть internal-шов с параметрами, а VContainer
        // без явной пометки выбрал бы конструктор с наибольшим числом параметров (TypeAnalyzer).
        [Inject]
        public SaveLoadService()
        {
        }

        // Тестовый шов: в проде поля и Logger заполняет VContainer.
        internal SaveLoadService(ISaveEnvelope saveEnvelope, ISaveStorage storage, ILogChannel logger)
        {
            _saveEnvelope = saveEnvelope;
            _storage = storage;
            Logger = logger;
        }

        private bool _isInited;
        private bool _isSaving;
        private bool _saveQueued;

        protected override async UniTask Load()
        {
            var result = await _storage.TryReadAsync();

            if (result.Status == SaveReadStatus.Success)
            {
                // Битый сейв не должен ронять запуск игры: карантиним файл и стартуем с новыми данными.
                try
                {
                    _saveEnvelope.Deserialize(result.Bytes.Span);
                    Logger.Log($"Save data loaded from {_storage.Description}.");
                }
                catch (Exception exception)
                {
                    await _storage.QuarantineAsync();
                    _saveEnvelope.PrepareNewData();
                    Logger.LogError($"Save data is corrupted and was quarantined, starting fresh. {exception}");
                }
            }
            else if (result.Status == SaveReadStatus.Corrupted)
            {
                await _storage.QuarantineAsync();
                _saveEnvelope.PrepareNewData();
                Logger.LogError("Save data is corrupted and was quarantined, starting fresh.");
            }
            else
            {
                _saveEnvelope.PrepareNewData();
                Logger.Log("Save data is empty, nothing to load.");
            }

            _isInited = true;
        }

        protected override UniTask Init()
        {
            SetEnabled(true);
            return UniTask.CompletedTask;
        }

        public void SaveData()
        {
            if (!_isInited)
            {
                return;
            }

            if (_isSaving)
            {
                _saveQueued = true;
                return;
            }

            SaveAsync().Forget(Logger);
        }

        public void SaveDataImmediate()
        {
            if (!_isInited)
            {
                return;
            }

            var bytes = _saveEnvelope.Serialize();
            _storage.Write(bytes);
            Logger.Log($"Save data saved immediately to {_storage.Description}.");
        }

        private async UniTask SaveAsync()
        {
            _isSaving = true;

            try
            {
                do
                {
                    _saveQueued = false;
                    var bytes = _saveEnvelope.Serialize();
                    await _storage.WriteAsync(bytes);
                    Logger.Log($"Save data saved to {_storage.Description}.");
                } while (_saveQueued);
            }
            finally
            {
                _isSaving = false;
            }
        }
    }
}
