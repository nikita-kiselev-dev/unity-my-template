using Framework.Foundation.Initialization;
using Framework.Foundation.Utilities.Extensions;
using UnityEngine;

namespace Framework.Foundation.UI.Canvas
{
    public class WindowCanvas : MonoBehaviour, IWindowCanvas
    {
        [SerializeField] private UnityEngine.Canvas m_Canvas;

        public Transform ViewParentTransform => gameObject.transform;

        public void Init()
        {
            m_Canvas.SetCameraByTag(GameConstants.MainCameraKey);
        }
    }
}