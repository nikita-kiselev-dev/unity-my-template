using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Initialization;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace Framework.Foundation.Asset
{
    // Singleton: Scoped дал бы отдельный инстанс на каждый сценовый scope плюс скрытый root-инстанс
    // у Singleton-потребителей (captive dependency) — два кэша вместо одного. Изоляцию ассетов по
    // сценам обеспечивает не lifetime, а ReleaseAllNonPersistent по шторке.
    [AutoRegistration(Lifetime.Singleton)]
    public sealed class AddressableAssetProvider : IAssetProvider, IAssetOwnerHost, IAssetProviderDiagnostics, IDisposable
    {
        [Inject] private readonly ISignalBus _signalBus;

        private readonly Dictionary<string, CachedAssetHandle> _cachedHandles = new();
        private readonly InflightLoads<CachedAssetHandle> _inflightLoads = new();
        private readonly Dictionary<string, int> _inflightWaiters = new();
        private readonly AssetOwnership _ownership = new();
        private readonly InstanceRegistry<GameObject> _instances = new(static instance => instance != null);

        // Владелец всего, что загружено прямыми вызовами IAssetProvider: инфраструктура Foundation
        // scope-ов не заводит, но её ключи тоже кто-то должен держать.
        private readonly object _rootOwner = new();

        private DisposableBag _subscriptions;
        private bool _disposed;

        // Чистое чтение состояния для дебаг-оверлея: мёртвые инстансы только считаются, но не
        // удаляются, чтобы съём состояния не мутировал провайдер.
        public AssetProviderSnapshot GetSnapshot()
        {
            var cached = new List<CachedAssetInfo>(_cachedHandles.Count);
            foreach (var pair in _cachedHandles)
            {
                cached.Add(new CachedAssetInfo(
                    pair.Key,
                    pair.Value.AssetType?.Name ?? "?",
                    _ownership.IsPersistent(pair.Key),
                    _instances.CountAlive(pair.Key)));
            }

            var instances = new List<InstanceGroupInfo>(_instances.Keys.Count);
            foreach (var key in _instances.Keys)
            {
                instances.Add(new InstanceGroupInfo(key, _instances.CountAlive(key)));
            }

            return new AssetProviderSnapshot(
                cached,
                new List<string>(_inflightLoads.Keys),
                new List<string>(_ownership.PersistentKeys),
                instances);
        }

        [Inject]
        private void Init()
        {
            _signalBus.Subscribe<LoadingCurtainShownSignal>(ReleaseAllNonPersistent).AddTo(ref _subscriptions);
        }

        public UniTask<T> LoadAssetAsync<T>(
            string key,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object =>
            LoadOwnedAsync<T>(key, _rootOwner, persistent, cancellationToken);

        public UniTask<T> InstantiateAsync<T>(
            string key,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default) =>
            InstantiateOwnedAsync<T>(key, _rootOwner, parent, worldPositionStays, setActive, persistent, cancellationToken);

        // AssetReference-перегрузки делегируют в строковые: GUID из RuntimeKey — валидный
        // Addressables-ключ, поэтому весь кэш/владение/persistent переиспользуется без изменений.
        public UniTask<T> LoadAssetAsync<T>(
            AssetReference reference,
            bool persistent = false,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object =>
            LoadAssetAsync<T>(ResolveKey(reference), persistent, cancellationToken);

        public UniTask<T> InstantiateAsync<T>(
            AssetReference reference,
            Transform parent = null,
            bool worldPositionStays = false,
            bool setActive = false,
            bool persistent = false,
            CancellationToken cancellationToken = default) =>
            InstantiateAsync<T>(ResolveKey(reference), parent, worldPositionStays, setActive, persistent, cancellationToken);

        public void ReleaseAsset(AssetReference reference) => ReleaseAsset(ResolveKey(reference));

        UniTask<T> IAssetOwnerHost.LoadAssetAsync<T>(
            string key,
            object owner,
            CancellationToken cancellationToken) =>
            LoadOwnedAsync<T>(key, owner, persistent: false, cancellationToken);

        UniTask<T> IAssetOwnerHost.InstantiateAsync<T>(
            string key,
            object owner,
            Transform parent,
            bool worldPositionStays,
            bool setActive,
            CancellationToken cancellationToken) =>
            InstantiateOwnedAsync<T>(key, owner, parent, worldPositionStays, setActive, persistent: false, cancellationToken);

        void IAssetOwnerHost.ReleaseCompletely(string key, object owner) => ReleaseOwnedCompletely(key, owner);

        private static string ResolveKey(AssetReference reference) => reference.RuntimeKey.ToString();

        // Уничтожает инстанс, но НЕ освобождает ассет-handle: ассет остаётся в кэше до
        // «шторки» (ReleaseAllNonPersistent) или явного ReleaseAsset/ReleaseCompletely,
        // чтобы переживать пересоздание инстансов между показами без повторной загрузки.
        // Учёт снимается и с уже уничтоженного инстанса — иначе запись жила бы до конца процесса.
        public void ReleaseInstance(GameObject instance)
        {
            if (!_instances.TryUntrack(instance).HasValue)
            {
                return;
            }

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }
        }

        public void ReleaseAsset(string key)
        {
            // persistent снимает только владелец заявки и только через ReleaseCompletely.
            if (_ownership.IsPersistent(key))
            {
                return;
            }

            ReleaseOwned(key, _rootOwner);
        }

        public void ReleaseCompletely(string key) => ReleaseOwnedCompletely(key, _rootOwner);

        public IAssetScope CreateScope() => new AssetScope(this);

        private UniTask<T> LoadOwnedAsync<T>(
            string key,
            object owner,
            bool persistent,
            CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            // Попадание в кэш — горячий путь (спавн иконок, открытие окон) и не должно платить
            // за async-машину. Несовпадение типа и отменённый токен уходят в async-ветку, чтобы
            // исключение осталось в задаче, а не летело синхронно из метода.
            if (!cancellationToken.IsCancellationRequested
                && _cachedHandles.TryGetValue(key, out var cached)
                && cached.AssetType == typeof(T))
            {
                _ownership.Acquire(key, owner, persistent);
                return UniTask.FromResult((T)cached.Asset);
            }

            return ResolveAssetAsync<T>(key, owner, persistent, cancellationToken);
        }

        private async UniTask<T> InstantiateOwnedAsync<T>(
            string key,
            object owner,
            Transform parent,
            bool worldPositionStays,
            bool setActive,
            bool persistent,
            CancellationToken cancellationToken)
        {
            var prefab = await LoadOwnedAsync<GameObject>(key, owner, persistent, cancellationToken);

            // Отмена во время загрузки: вызывающий ушёл, и созданный инстанс остался бы сиротой
            // в учёте — до шторки его никто не заберёт.
            cancellationToken.ThrowIfCancellationRequested();

            // Временно гасим кэшированный prefab, чтобы Awake/OnEnable на инстансе не сработали
            // до настройки вызывающим кодом; в finally возвращаем исходный activeSelf.
            var instance = AssetInstantiation.InstantiateDeferredAwake(
                prefab, parent, worldPositionStays, setActive);

            var component = instance.GetComponent<T>();
            if (component == null)
            {
                UnityEngine.Object.Destroy(instance);
                throw AssetInstantiation.MissingComponent<T>(key);
            }

            _instances.Track(instance, key, owner);
            return component;
        }

        private void ReleaseOwnedCompletely(string key, object owner)
        {
            DestroyOwnedInstances(key, owner);
            ReleaseOwned(key, owner);
        }

        // Владелец отдаёт ключ. Handle освобождается, только когда ключ не держит никто и не
        // осталось живых инстансов: иначе релиз одной фичи выдёргивал бы ассет из-под другой.
        private void ReleaseOwned(string key, object owner)
        {
            if (!_ownership.Release(key, owner))
            {
                return;
            }

            if (_instances.HasAlive(key))
            {
                return;
            }

            ReleaseHandle(key);
        }

        // Инстансы того же ключа, созданные другим владельцем, остаются жить: persistent-флаг
        // относится к ассету, а владение — к каждому объекту отдельно.
        private void DestroyOwnedInstances(string key, object owner)
        {
            if (!_instances.TryTakeAll(key, owner).TryGet(out var instances))
            {
                return;
            }

            DestroyAll(instances);
        }

        private static void DestroyAll(IReadOnlyCollection<GameObject> instances)
        {
            foreach (var instance in instances)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }
        }

        private void ReleaseHandle(string key)
        {
            if (_cachedHandles.Remove(key, out var cached))
            {
                Addressables.Release(cached.Handle);
            }
        }

        private async UniTask<T> ResolveAssetAsync<T>(
            string key,
            object owner,
            bool persistent,
            CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            var handle = await ResolveHandleAsync<T>(key, cancellationToken);

            // Владение и persistent выставляем только после успешного резолва и для каждого
            // вызывающего отдельно: одну in-flight загрузку могут ждать несколько вызовов.
            _ownership.Acquire(key, owner, persistent);
            return (T)handle.Asset;
        }

        private UniTask<CachedAssetHandle> ResolveHandleAsync<T>(
            string key,
            CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            // Кэш и in-flight отдаются синхронно, поэтому отмену проверяем явно: без этого
            // отменённый вызывающий получал бы ассет и создавал инстанс.
            cancellationToken.ThrowIfCancellationRequested();

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AddressableAssetProvider));
            }

            if (_cachedHandles.TryGetValue(key, out var cached))
            {
                if (cached.AssetType != typeof(T))
                {
                    throw new InvalidOperationException(
                        $"Asset '{key}' was cached as {cached.AssetType.Name}, requested as {typeof(T).Name}.");
                }

                return UniTask.FromResult(cached);
            }

            if (_inflightLoads.IsInflight(key))
            {
                return JoinLoadAsync(key, typeof(T), cancellationToken);
            }

            return StartLoadAsync<T>(key, cancellationToken);
        }

        private async UniTask<CachedAssetHandle> JoinLoadAsync(
            string key,
            Type requestedType,
            CancellationToken cancellationToken)
        {
            var pending = _inflightLoads.Join(key, requestedType, cancellationToken);
            AddWaiter(key);

            try
            {
                return await pending;
            }
            finally
            {
                ForgetWaiter(key);
            }
        }

        // Ждущий регистрируется до старта загрузки: закончись она синхронно, LoadHandleAsync
        // счёл бы её брошенной и освободил handle прямо под инициатором.
        private async UniTask<CachedAssetHandle> StartLoadAsync<T>(
            string key,
            CancellationToken cancellationToken) where T : UnityEngine.Object
        {
            AddWaiter(key);
            _inflightLoads.Begin(key, typeof(T));

            try
            {
                return await LoadHandleAsync<T>(key).AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                ForgetWaiter(key);
            }
        }

        private void AddWaiter(string key)
        {
            _inflightWaiters.TryGetValue(key, out var waiters);
            _inflightWaiters[key] = waiters + 1;
        }

        private void ForgetWaiter(string key)
        {
            var waiters = _inflightWaiters[key] - 1;

            if (waiters == 0)
            {
                _inflightWaiters.Remove(key);
                return;
            }

            _inflightWaiters[key] = waiters;
        }

        private async UniTask<CachedAssetHandle> LoadHandleAsync<T>(string key) where T : UnityEngine.Object
        {
            // handle объявлен снаружи try, но создаётся внутри: синхронный бросок Addressables
            // обязан дойти до Fail, иначе присоединившиеся ждут обещание, которое никто не закроет.
            AsyncOperationHandle<T> handle = default;

            try
            {
                handle = Addressables.LoadAssetAsync<T>(key);
                await handle.ToUniTask();

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    throw handle.OperationException ?? new InvalidOperationException($"Failed to load asset {key}");
                }

                // Провайдер умер, пока ассет грузился: в _cachedHandles запись уже никто не
                // заберёт, поэтому handle отдаём на релиз в catch, а не оставляем без владельца.
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(AddressableAssetProvider));
                }

                var result = new CachedAssetHandle(handle, handle.Result, typeof(T));

                _cachedHandles[key] = result;

                // Результат раздаётся присоединившимся до выхода: инициатор мог быть отменён, и
                // тогда его собственный await уже не вернётся, а ждущие останутся висеть.
                _inflightLoads.Complete(key, result);
                return result;
            }
            catch (Exception exception)
            {
                // Ошибка загрузки или смерть провайдера: освобождаем handle, иначе addressable-ресурс утечёт.
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                _inflightLoads.Fail(key, exception);
                throw;
            }
            finally
            {
                // Загрузка пережила всех, кто её ждал (шторка сменила сцену, вызывающие
                // отменились): владельца у ключа не появится, а в кэше ассет дожил бы до
                // следующей сцены как загруженный на всякий случай.
                if (!_inflightWaiters.ContainsKey(key))
                {
                    ReleaseHandle(key);
                }
            }
        }

        // Шторка = корневой владелец отпускает всё, что не заявлено persistent. Ключ, который
        // держит живой scope, её переживает: владелец ещё жив и сам его освободит.
        private void ReleaseAllNonPersistent()
        {
            var keys = new List<string>(_cachedHandles.Keys);

            foreach (var key in keys)
            {
                if (!_ownership.IsPersistent(key))
                {
                    ReleaseOwnedCompletely(key, _rootOwner);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // Флаг до релизов: загрузка, которая завершится после этой точки, обязана увидеть
            // мёртвый провайдер и освободить свой handle сама.
            _disposed = true;
            _subscriptions.Dispose();

            var keys = new List<string>(_cachedHandles.Keys);

            // Владение на закрытии провайдера уже ничего не значит: живых владельцев не
            // останется ни у одного ключа.
            foreach (var key in keys)
            {
                if (_instances.TryTakeAll(key).TryGet(out var instances))
                {
                    DestroyAll(instances);
                }

                ReleaseHandle(key);
            }

            _cachedHandles.Clear();
            _instances.Clear();
            _ownership.Clear();
        }
    }

}
