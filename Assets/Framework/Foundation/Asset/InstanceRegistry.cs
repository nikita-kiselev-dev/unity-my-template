using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Framework.Foundation.Utilities;

namespace Framework.Foundation.Asset
{
    // Двусторонний учёт «ключ ↔ инстансы» без зависимости от Unity: живость инстанса задаёт
    // делегат, поэтому логика проверяется в fast-tests, а не только в плеере.
    internal sealed class InstanceRegistry<TInstance> where TInstance : class
    {
        private readonly Func<TInstance, bool> _isAlive;
        private readonly Predicate<TInstance> _forgetIfDead;

        private readonly Dictionary<string, HashSet<TInstance>> _instancesByKey = new();
        private readonly Dictionary<TInstance, InstanceOrigin> _originByInstance = new(ReferenceComparer.Instance);

        public InstanceRegistry(Func<TInstance, bool> isAlive)
        {
            _isAlive = isAlive;
            _forgetIfDead = ForgetIfDead;
        }

        public IReadOnlyCollection<string> Keys => _instancesByKey.Keys;

        public void Track(TInstance instance, string key, object owner)
        {
            if (_originByInstance.TryGetValue(instance, out var previous))
            {
                RemoveFromKey(instance, previous.Key);
            }

            if (!_instancesByKey.TryGetValue(key, out var instances))
            {
                instances = new HashSet<TInstance>(ReferenceComparer.Instance);
                _instancesByKey.Add(key, instances);
            }

            instances.Add(instance);
            _originByInstance[instance] = new InstanceOrigin(key, owner);
        }

        // Живость не проверяется намеренно: инстанс, уничтоженный мимо провайдера, обязан уйти
        // из учёта — иначе запись висит до следующего обращения к тому же ключу или навсегда.
        public Result<string> TryUntrack(TInstance instance)
        {
            if (instance is null || !_originByInstance.Remove(instance, out var origin))
            {
                return Result<string>.Failure();
            }

            RemoveFromKey(instance, origin.Key);
            return Result<string>.Success(origin.Key);
        }

        public bool HasAlive(string key)
        {
            if (!_instancesByKey.TryGetValue(key, out var instances))
            {
                return false;
            }

            instances.RemoveWhere(_forgetIfDead);

            if (instances.Count > 0)
            {
                return true;
            }

            _instancesByKey.Remove(key);
            return false;
        }

        // Чистое чтение для снапшота: мёртвые считаются, но из учёта не выпадают.
        public int CountAlive(string key)
        {
            if (!_instancesByKey.TryGetValue(key, out var instances))
            {
                return 0;
            }

            var alive = 0;
            foreach (var instance in instances)
            {
                if (_isAlive(instance))
                {
                    alive++;
                }
            }

            return alive;
        }

        // Отдаёт владение набором: реестр о ключе больше не знает, мёртвые инстансы вызывающий
        // отсеивает сам — ему всё равно нужно уничтожать живые.
        public Result<IReadOnlyCollection<TInstance>> TryTakeAll(string key)
        {
            if (!_instancesByKey.Remove(key, out var instances))
            {
                return Result<IReadOnlyCollection<TInstance>>.Failure();
            }

            foreach (var instance in instances)
            {
                _originByInstance.Remove(instance);
            }

            return Result<IReadOnlyCollection<TInstance>>.Success(instances);
        }

        // Владелец забирает только то, что создал сам: инстансы того же ключа, созданные другим
        // владельцем, остаются в учёте.
        public Result<IReadOnlyCollection<TInstance>> TryTakeAll(string key, object owner)
        {
            if (!_instancesByKey.TryGetValue(key, out var instances))
            {
                return Result<IReadOnlyCollection<TInstance>>.Failure();
            }

            List<TInstance> owned = null;
            foreach (var instance in instances)
            {
                if (ReferenceEquals(_originByInstance[instance].Owner, owner))
                {
                    owned ??= new List<TInstance>();
                    owned.Add(instance);
                }
            }

            if (owned is null)
            {
                return Result<IReadOnlyCollection<TInstance>>.Failure();
            }

            foreach (var instance in owned)
            {
                instances.Remove(instance);
                _originByInstance.Remove(instance);
            }

            if (instances.Count == 0)
            {
                _instancesByKey.Remove(key);
            }

            return Result<IReadOnlyCollection<TInstance>>.Success(owned);
        }

        public void Clear()
        {
            _instancesByKey.Clear();
            _originByInstance.Clear();
        }

        private void RemoveFromKey(TInstance instance, string key)
        {
            if (!_instancesByKey.TryGetValue(key, out var instances))
            {
                return;
            }

            instances.Remove(instance);

            if (instances.Count == 0)
            {
                _instancesByKey.Remove(key);
            }
        }

        private bool ForgetIfDead(TInstance instance)
        {
            if (_isAlive(instance))
            {
                return false;
            }

            _originByInstance.Remove(instance);
            return true;
        }

        private readonly struct InstanceOrigin
        {
            public InstanceOrigin(string key, object owner)
            {
                Key = key;
                Owner = owner;
            }

            public string Key { get; }
            public object Owner { get; }
        }

        // Уничтоженный UnityEngine.Object отвечает на Equals по instanceID, а не по живости:
        // идентичность инстанса здесь — только ссылка.
        private sealed class ReferenceComparer : IEqualityComparer<TInstance>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(TInstance x, TInstance y) => ReferenceEquals(x, y);

            public int GetHashCode(TInstance obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
