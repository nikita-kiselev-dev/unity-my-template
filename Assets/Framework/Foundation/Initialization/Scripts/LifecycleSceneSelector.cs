using System;
using System.Collections.Generic;
using System.Reflection;
using ZLinq;

namespace Framework.Foundation.Initialization
{
    public static class LifecycleSceneSelector
    {
        private static readonly Dictionary<(Type, string), LifecycleOrderAttribute> _orderCache = new();

        public static LifecycleEntity[] SelectForScene(
            IReadOnlyList<LifecycleEntity> lifecycleEntities,
            string sceneName)
        {
            return lifecycleEntities
                .AsValueEnumerable()
                .Select(entity => (entity, order: GetOrder(entity.GetType(), sceneName)))
                .Where(x => x.order != null)
                .OrderBy(x => x.order.InitOrder)
                // При равных InitOrder порядок иначе зависел бы от порядка скана сборок — недетерминизм.
                .ThenBy(x => x.entity.GetType().Name)
                .Select(x => x.entity)
                .ToArray();
        }

        private static LifecycleOrderAttribute GetOrder(Type entityType, string sceneName)
        {
            var key = (entityType, sceneName);
            if (_orderCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var order = entityType
                .GetCustomAttributes<LifecycleOrderAttribute>(false)
                .AsValueEnumerable()
                .FirstOrDefault(x => x.SceneScopeName == sceneName);

            _orderCache[key] = order;
            return order;
        }
    }
}
