using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Framework.Foundation.Configs
{
    /// Все конфиги грузятся одним WarmUp до создания потребителей: [Inject] синхронный,
    /// а чтение конфига — нет.
    public sealed class ConfigProvider : IConfigProvider
    {
        private readonly IConfigReader _reader;
        private readonly IReadOnlyList<ConfigTypeEntry> _entries;
        private readonly Dictionary<Type, IConfig> _configs = new();

        private UniTask? _warmUp;
        private bool _isWarmedUp;

        public ConfigProvider(IConfigReader reader, IReadOnlyList<ConfigTypeEntry> entries)
        {
            _reader = reader;
            _entries = entries;
        }

        public async UniTask WarmUp(CancellationToken cancellationToken = default)
        {
            if (_isWarmedUp)
            {
                return;
            }

            // Preserve — WarmUp зовёт SceneStarter каждой сцены, а UniTask нельзя await дважды.
            _warmUp ??= LoadAll(cancellationToken).Preserve();

            try
            {
                await _warmUp.Value;
            }
            catch
            {
                // Иначе отмена или ошибка первой загрузки мемоизируется и валит все следующие сцены.
                _warmUp = null;
                throw;
            }

            _isWarmedUp = true;
        }

        public IConfig Get(Type configType)
        {
            if (!_configs.TryGetValue(configType, out var config))
            {
                throw new InvalidOperationException(
                    $"Config {configType.Name} is not loaded: mark it with [{nameof(ConfigKeyAttribute)}] and warm up {nameof(IConfigProvider)} before resolving.");
            }

            return config;
        }

        private async UniTask LoadAll(CancellationToken cancellationToken)
        {
            var tasks = new UniTask[_entries.Count];

            for (var i = 0; i < _entries.Count; i++)
            {
                tasks[i] = Load(_entries[i], cancellationToken);
            }

            await UniTask.WhenAll(tasks);
        }

        private async UniTask Load(ConfigTypeEntry entry, CancellationToken cancellationToken)
        {
            var result = await _reader.Read(entry.ConfigType, entry.ConfigKey, cancellationToken);

            if (!result.HasValue)
            {
                throw new InvalidOperationException(
                    $"Failed to load config '{entry.ConfigKey}' for {entry.ConfigType.Name}: " +
                    "no source (server, cache, dummy) returned a valid value.");
            }

            _configs[entry.ConfigType] = result.Value;
        }
    }
}
