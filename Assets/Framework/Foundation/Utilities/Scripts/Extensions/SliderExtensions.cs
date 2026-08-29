using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class SliderExtensions
    {
        public static void AddListenerClean(this Slider slider, UnityAction<float> listenerAction)
        {
            RemoveListener(slider, listenerAction);
            AddListener(slider, listenerAction);
        }
        
        public static void AddListener(this Slider slider, UnityAction<float> listenerAction)
        {
            slider.onValueChanged.AddListener(listenerAction);
        }

        public static void RemoveListener(this Slider slider, UnityAction<float> listenerAction)
        {
            slider.onValueChanged.RemoveListener(listenerAction);
        }

        public static void RemoveAllListeners(this Slider slider)
        {
            slider.onValueChanged.RemoveAllListeners();
        }
    }
}