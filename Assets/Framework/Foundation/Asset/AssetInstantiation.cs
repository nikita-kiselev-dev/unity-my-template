using System;
using UnityEngine;

namespace Framework.Foundation.Asset
{
    internal static class AssetInstantiation
    {
        public static GameObject InstantiateDeferredAwake(
            GameObject prefab,
            Transform parent,
            bool worldPositionStays,
            bool setActive)
        {
            var wasActive = prefab.activeSelf;
            try
            {
                if (wasActive)
                {
                    prefab.SetActive(false);
                }

                var instance = UnityEngine.Object.Instantiate(prefab, parent, worldPositionStays);
                if (setActive)
                {
                    instance.SetActive(true);
                }

                return instance;
            }
            finally
            {
                if (wasActive)
                {
                    prefab.SetActive(true);
                }
            }
        }

        public static InvalidOperationException MissingComponent<T>(string key) =>
            new($"Prefab '{key}' has no component {typeof(T).Name}.");
    }
}
