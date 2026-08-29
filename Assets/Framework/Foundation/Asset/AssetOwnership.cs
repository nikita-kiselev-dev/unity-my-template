using System.Collections.Generic;

namespace Framework.Foundation.Asset
{
    // Кто держит ключ. Владельцев у ключа несколько (scope фичи, корневой владелец провайдера),
    // и handle освобождается только когда не осталось ни одного: иначе релиз одной фичи
    // выдёргивал бы ассет из-под другой.
    internal sealed class AssetOwnership
    {
        // Запись существует только пока в ней есть владельцы, поэтому «ключ есть в словаре»
        // и «ключом владеют» — одно и то же.
        private readonly Dictionary<string, HashSet<object>> _ownersByKey = new();
        private readonly Dictionary<string, HashSet<object>> _persistentOwnersByKey = new();

        public IReadOnlyCollection<string> Keys => _ownersByKey.Keys;

        public IReadOnlyCollection<string> PersistentKeys => _persistentOwnersByKey.Keys;

        public void Acquire(string key, object owner, bool persistent)
        {
            Add(_ownersByKey, key, owner);

            if (persistent)
            {
                Add(_persistentOwnersByKey, key, owner);
            }
        }

        // true — владельцев не осталось, ключ можно освобождать. Заявка на persistent уходит
        // вместе с владельцем: снять её может только тот, кто её поставил.
        public bool Release(string key, object owner)
        {
            Remove(_persistentOwnersByKey, key, owner);
            Remove(_ownersByKey, key, owner);
            return !IsOwned(key);
        }

        public bool IsOwned(string key) => _ownersByKey.ContainsKey(key);

        public bool IsPersistent(string key) => _persistentOwnersByKey.ContainsKey(key);

        public void Clear()
        {
            _ownersByKey.Clear();
            _persistentOwnersByKey.Clear();
        }

        private static void Add(Dictionary<string, HashSet<object>> owners, string key, object owner)
        {
            if (!owners.TryGetValue(key, out var keyOwners))
            {
                keyOwners = new HashSet<object>();
                owners.Add(key, keyOwners);
            }

            keyOwners.Add(owner);
        }

        private static void Remove(Dictionary<string, HashSet<object>> owners, string key, object owner)
        {
            if (!owners.TryGetValue(key, out var keyOwners))
            {
                return;
            }

            keyOwners.Remove(owner);

            if (keyOwners.Count == 0)
            {
                owners.Remove(key);
            }
        }
    }
}
