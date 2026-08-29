using UnityEngine;
using UnityEngine.Events;

namespace Framework.Foundation.UI.Canvas
{
    public interface IPopupCanvas : ICanvas
    {
        CanvasGroup BackgroundCanvasGroup { get; }
        void Init(UnityAction onBackgroundClicked);
    }
}