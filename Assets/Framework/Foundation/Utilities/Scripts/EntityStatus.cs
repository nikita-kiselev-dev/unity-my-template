using System;
using Framework.Foundation.Logger;
using R3;

namespace Framework.Foundation.Utilities
{
    public sealed class EntityStatus : IReadOnlyEntityStatus, IDisposable
    {
        private readonly ReactiveProperty<bool> _isEnabled = new();
        private readonly ReactiveProperty<bool> _isInited = new();
        private readonly ReactiveProperty<bool> _isActive = new();
        private readonly LogChannel _logger;
        private LogCategory _entityType;
        private DisposableBag _subscriptions;
        private bool _isDisposed;

        public ILogChannel Logger => _logger;

        public bool IsEnabled => _isEnabled.Value;
        public bool IsInited => _isInited.Value;
        public bool IsActive => _isActive.Value;

        public EntityStatus(string entityName, LogCategory entityType = LogCategory.System, bool areLogsEnabled = false)
        {
            _entityType = entityType;
            // Статус создаётся в ctor LifecycleEntity, до инжекта: фабрику здесь взять негде.
            _logger = new LogChannel(entityName, entityType);
            _logger.SetLogsStatus(areLogsEnabled);
            SubscribeStatusChanges(_isEnabled, nameof(IsEnabled));
            SubscribeStatusChanges(_isInited, nameof(IsInited));
            SubscribeStatusChanges(_isActive, nameof(IsActive));
        }

        // No-op после Dispose: VContainer может повторить Dispose entity при teardown scope
        // (например, после раннего Dispose по OnClosed) — сбросы статусов не должны бить
        // по задиспозенным ReactiveProperty.
        public EntityStatus SetEnabled(bool value)
        {
            if (!_isDisposed)
            {
                _isEnabled.Value = value;
            }

            return this;
        }

        public EntityStatus SetInited(bool value)
        {
            if (!_isDisposed)
            {
                _isInited.Value = value;
            }

            return this;
        }

        public EntityStatus SetActive(bool value)
        {
            if (!_isDisposed)
            {
                _isActive.Value = value;
            }

            return this;
        }

        public void EnableLogging(LogCategory entityType = LogCategory.System)
        {
            _entityType = entityType;
            _logger.SetEntityType(entityType);
            _logger.SetLogsStatus(true);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _subscriptions.Dispose();
            _isEnabled.Dispose();
            _isInited.Dispose();
            _isActive.Dispose();
        }

        private void SubscribeStatusChanges(ReactiveProperty<bool> property, string statusName)
        {
            property
                .DistinctUntilChanged()
                .Skip(1)
                .Subscribe(value => LogStatusChange(statusName, value))
                .AddTo(ref _subscriptions);
        }

        private void LogStatusChange(string statusName, bool value)
        {
            var statusString = value.ToString();

            switch (_entityType)
            {
                case LogCategory.System:
                    statusString = statusString.SetSystemColor();
                    break;
                case LogCategory.Feature:
                    statusString = statusString.SetFeatureColor();
                    break;
            }

            _logger.Log($"Entity {statusName} status: {statusString}.");
        }
    }
}
