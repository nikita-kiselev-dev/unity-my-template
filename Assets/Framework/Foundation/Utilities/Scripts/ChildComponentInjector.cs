using System;
using UnityEngine;
using VContainer;
using ZLinq;

namespace Framework.Foundation.Utilities
{
    public static class ChildComponentInjector
    {
        public static void Inject<T, U>(IObjectResolver resolver, MonoBehaviour monoBehaviour) 
            where T : Attribute 
            where U : MonoBehaviour
        {
            var viewHasAttribute = monoBehaviour
                .GetType()
                .GetCustomAttributes(typeof(T), inherit: false)
                .AsValueEnumerable()
                .Any();

            if (!viewHasAttribute)
            {
                return;
            }
            
            var childrenViews = monoBehaviour.transform.GetComponentsInChildren<U>(true);

            foreach (var childrenView in childrenViews)
            {
                resolver.Inject(childrenView);
            }
        }
    }
}