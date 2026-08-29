using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Asset;
using Framework.Foundation.LiveOps;
using Framework.Foundation.LiveOps.Signals;
using Framework.Foundation.Logger;
using Framework.Foundation.Signals;
using Framework.Foundation.Utilities;
using Newtonsoft.Json;
using R3;
using UnityEngine;
using VContainer;

namespace Framework.Foundation.Configs
{
    public class ConfigReader : IConfigReader, IDisposable
    {
        [Inject] private readonly IAssetProvider _assetProvider;
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly IRemoteConfigSource _remoteSource;
        [Inject] private readonly IConfigStorage _storage;
        [Inject] private readonly ILogChannelFactory _logChannelFactory;

        private ILogChannel _logger;
        private IConfigResolver _resolver;
        private IReadOnlyDictionary<string, string> _serverValues;
        private Dictionary<string, string> _cachedValues;
        private DisposableBag _subscriptions;

        private bool _isInited;

        // [Inject] на этом ctor обязателен: рядом есть internal-шов с параметрами, а VContainer
        // без явной пометки выбрал бы конструктор с наибольшим числом параметров (TypeAnalyzer).
        [Inject]
        public ConfigReader()
        {
        }

        // Тестовый шов: в проде поля и Init заполняет VContainer.
        internal ConfigReader(
            IAssetProvider assetProvider,
            ISignalBus signalBus,
            IRemoteConfigSource remoteSource,
            IConfigStorage storage,
            ILogChannelFactory logChannelFactory,
            IConfigResolver resolver = null)
        {
            _assetProvider = assetProvider;
            _signalBus = signalBus;
            _remoteSource = remoteSource;
            _storage = storage;
            _logChannelFactory = logChannelFactory;
            _resolver = resolver;
            Init();
        }

        public async UniTask<Result<IConfig>> Read(Type configType, string configName, CancellationToken cancellationToken = default)
        {
            if (!_isInited)
            {
                return new Result<IConfig>(null, false);
            }

            // Резолвер бросает только когда не остаётся ни одного валидного источника.
            // Наружу это уходит через Result, а не исключением: решение принимает ConfigProvider.
            try
            {
                var config = await _resolver.Read(configType, configName, cancellationToken);
                return new Result<IConfig>(config, config != null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError($"Config '{configName}' could not be read from any source. {exception}");
                return new Result<IConfig>(null, false);
            }
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // Логгер берётся здесь, а не через [AutoLogger]: ConfigResolver получает его при создании,
        // а порядок вызова нескольких [Inject]-методов VContainer не определяет.
        [Inject]
        private void Init()
        {
            _logger = _logChannelFactory.Get(nameof(ConfigReader));

            TryGetCachedValues();

            // Тестовый шов мог подставить резолвер до Init.
            _resolver ??= new ConfigResolver(
                ReadDummyConfig,
                _cachedValues,
                _logger,
                Save);

            _signalBus.Subscribe<ServerLoginCompletedSignal>(OnServerLoginCompleted).AddTo(ref _subscriptions);

            _isInited = true;
        }

        private async UniTask<string> ReadDummyConfig(string configName, CancellationToken cancellationToken)
        {
            var configAsset = await _assetProvider.LoadAssetAsync<TextAsset>(configName, cancellationToken: cancellationToken);
            var json = configAsset.text;
            _assetProvider.ReleaseAsset(configName);
            return json;
        }

        private void OnServerLoginCompleted()
        {
            _serverValues = _remoteSource.GetValues();
            _resolver.SetServerValues(_serverValues);
        }

        private void Save()
        {
            if (!_isInited || _serverValues == null)
            {
                return;
            }

            var serialized = JsonConvert.SerializeObject(_serverValues);
            _storage.Save(serialized);
            _logger.Log($"{GetType().Name}: config saved to {_storage.Description}.");
        }

        private void TryGetCachedValues()
        {
            var config = _storage.Load();

            if (string.IsNullOrEmpty(config))
            {
                return;
            }

            try
            {
                _cachedValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
                if (_cachedValues == null)
                {
                    throw new JsonSerializationException("Config cache does not contain an object.");
                }
            }
            catch (JsonException exception)
            {
                _storage.Quarantine();
                _logger.LogError($"Config cache is corrupted and was quarantined. {exception}");
            }
        }
    }
}
