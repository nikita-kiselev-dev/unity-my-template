using Framework.Foundation.Initialization;
using Framework.Foundation.Utilities.Extensions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Framework.Foundation.UI.Canvas
{
    public class PopupCanvas : MonoBehaviour, IPopupCanvas
    {
        [SerializeField] private UnityEngine.Canvas m_Canvas;
        [SerializeField] private Button m_BackgroundButton;
        [SerializeField] private CanvasGroup m_BackgroundCanvasGroup;

        public Transform ViewParentTransform => gameObject.transform;
        public CanvasGroup BackgroundCanvasGroup => m_BackgroundCanvasGroup;

        public void Init(UnityAction onBackgroundClicked)
        {
            m_Canvas.SetCameraByTag(GameConstants.MainCameraKey);
            m_BackgroundButton.AddListenerClean(onBackgroundClicked);
        }
    }
}