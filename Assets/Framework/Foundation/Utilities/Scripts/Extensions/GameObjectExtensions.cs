using UnityEngine;
using UnityEngine.UI;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class GameObjectExtensions
    {
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (!go.TryGetComponent<T>(out var component))
            {
                component = go.AddComponent<T>();
            }

            return component;
        }

        public static void SetActiveSafe(this GameObject go, bool active)
        {
            if (go != null)
            {
                go.SetActive(active);
            }
        }

        public static void DestroyChildren(this Transform transform)
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
