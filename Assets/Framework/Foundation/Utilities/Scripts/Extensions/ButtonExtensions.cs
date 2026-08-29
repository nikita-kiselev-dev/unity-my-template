using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class ButtonExtensions
    {
        public static void AddListenerClean(this Button button, UnityAction listenerAction)
        {
            RemoveListener(button, listenerAction);
            AddListener(button, listenerAction);
        }
        
        public static void AddListener(this Button button, UnityAction listenerAction)
        {
            button.onClick.AddListener(listenerAction);
        }

        public static void RemoveListener(this Button button, UnityAction listenerAction)
        {
            button.onClick.RemoveListener(listenerAction);
        }

        public static void RemoveAllListeners(this Button button)
        {
            button.onClick.RemoveAllListeners();
        }
    }
}