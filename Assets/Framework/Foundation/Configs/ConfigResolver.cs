using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Logger;
using Newtonsoft.Json;

namespace Framework.Foundation.Configs
{
    /// Источники пробуются по одному: сервер -> кэш -> dummy. Битое значение источника не валит
    /// старт, а логируется и уступает следующему — симметрично изоляции блобов в SaveEnvelope.
    public class ConfigResolver : IConfigResolver
    {
        private const string ServerSource = "server";
        private const string CacheSource = "cache";
        private const string DummySource = "dummy";

        private readonly Func<string, CancellationToken, UniTask<string>> _dummyReader;
        private readonly IReadOnlyDictionary<string, string> _cachedValues;
        private readonly ILogChannel _logger;
        private readonly Action _saveAction;

        private IReadOnlyDictionary<string, string> _serverValues;

        public ConfigResolver(
            Func<string, CancellationToken, UniTask<string>> dummyReader,
            IReadOnlyDictionary<string, string> cachedValues,
            ILogChannel logger,
            Action saveAction)
        {
            _dummyReader = dummyReader;
            _cachedValues = cachedValues;
            _logger = logger;
            _saveAction = saveAction;
        }

        public async UniTask<IConfig> Read(Type configType, string configName, CancellationToken cancellationToken = default)
        {
            if (TryRead(configType, configName, ServerSource, _serverValues, out var config))
            {
                return config;
            }

            if (TryRead(configType, configName, CacheSource, _cachedValues, out config))
            {
                return config;
            }

            var dummyJson = await _dummyReader(configName, cancellationToken);

            // Dummy лежит в Addressables рядом с кодом: его невалидность — ошибка сборки,
            // отступать некуда, и молчаливый null здесь опаснее падения.
            if (!TryDeserialize(configType, configName, DummySource, dummyJson, out config))
            {
                throw new InvalidOperationException(
                    $"Config '{configName}' is invalid in every source ({ServerSource}, {CacheSource}, {DummySource}).");
            }

            return config;
        }

        public void SetServerValues(IReadOnlyDictionary<string, string> config)
        {
            _serverValues = config;

            if (!AreValueMapsEqual(_serverValues, _cachedValues))
            {
                _saveAction?.Invoke();
            }
        }

        private bool TryRead(
            Type configType,
            string configName,
            string source,
            IReadOnlyDictionary<string, string> values,
            out IConfig config)
        {
            config = null;

            if (values == null || !values.TryGetValue(configName, out var json))
            {
                return false;
            }

            return TryDeserialize(configType, configName, source, json, out config);
        }

        private bool TryDeserialize(Type configType, string configName, string source, string json, out IConfig config)
        {
            config = null;

            try
            {
                config = (IConfig)JsonConvert.DeserializeObject(json, configType);
            }
            catch (Exception exception)
            {
                _logger.LogError($"Config '{configName}' from {source} is malformed, trying next source. {exception}");
                return false;
            }

            if (config == null)
            {
                _logger.LogError($"Config '{configName}' from {source} deserialized to null, trying next source.");
                return false;
            }

            _logger.Log($"Loaded config: {configName} from {source}.");
            return true;
        }

        private static bool AreValueMapsEqual(
            IReadOnlyDictionary<string, string> first,
            IReadOnlyDictionary<string, string> second)
        {
            return first != null
                   && second != null
                   && first.Count == second.Count
                   && first.All(pair => second.TryGetValue(pair.Key, out var value) && value == pair.Value);
        }
    }
}
