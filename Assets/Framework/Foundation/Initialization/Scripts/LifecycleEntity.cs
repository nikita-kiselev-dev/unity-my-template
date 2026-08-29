using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization.Decorators;
using Framework.Foundation.Logger;
using Framework.Foundation.Utilities;
using System.Threading;

namespace Framework.Foundation.Initialization
{
    public abstract class LifecycleEntity : IEntityStatus, IDisposable
    {
        private readonly List<LifecycleEntity> _wrappers = new();

        private bool _isInitedSetInPhase;

        public IReadOnlyList<LifecycleEntity> Wrappers => _wrappers;
        public EntityStatus Status { get; }

        protected CancellationToken CancellationToken { get; private set; }

        protected LifecycleEntity()
        {
            Status = new EntityStatus(GetType().Name);
        }

        internal void AddWrapper(LifecycleEntity wrapper) => _wrappers.Add(wrapper);

        public UniTask LoadPhase(CancellationToken ct)
        {
            CancellationToken = ct;
            return Load();
        }

        public async UniTask InitPhase(CancellationToken ct)
        {
            CancellationToken = ct;
            _isInitedSetInPhase = false;
            await Init();

            // Ручной SetInited в Init — осознанный отказ от статуса (ранний выход), lifecycle его не перебивает.
            if (!_isInitedSetInPhase)
            {
                Status.SetInited(true);
            }
        }

        public UniTask PostInitPhase(CancellationToken ct)
        {
            CancellationToken = ct;
            return PostInit();
        }

        protected virtual UniTask Load() => UniTask.CompletedTask;
        protected virtual UniTask Init() => UniTask.CompletedTask;
        protected virtual UniTask PostInit() => UniTask.CompletedTask;

        protected void SetEnabled(bool value) => Status.SetEnabled(value);
        protected void SetInited(bool value = true)
        {
            _isInitedSetInPhase = true;
            Status.SetInited(value);
        }
        protected void SetActive(bool value = true) => Status.SetActive(value);

        protected void EnableStatusLogs(LogCategory entityType = LogCategory.System)
        {
            Status.EnableLogging(entityType);
        }

        bool IReadOnlyEntityStatus.IsEnabled => Status.IsEnabled;
        bool IReadOnlyEntityStatus.IsInited => Status.IsInited;
        bool IReadOnlyEntityStatus.IsActive => Status.IsActive;

        protected virtual void Unload()
        {
            foreach (var wrapper in _wrappers)
            {
                if (wrapper is IDisposableLifecycleWrapper disposableWrapper)
                {
                    disposableWrapper.Dispose();
                }
            }

            _wrappers.Clear();
        }

        public virtual void Dispose()
        {
            Unload();
            Status.Dispose();
        }
    }
}
