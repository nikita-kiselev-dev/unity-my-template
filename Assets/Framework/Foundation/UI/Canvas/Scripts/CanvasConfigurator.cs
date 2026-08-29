using UnityEngine;
using UnityEngine.UI;

namespace Framework.Foundation.UI.Canvas
{
    [RequireComponent(typeof(UnityEngine.Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class CanvasConfigurator : MonoBehaviour
    {
        private void Awake()
        {
            ConfigureCanvas();
        }
        
        // ContextMenu вместо Odin [Button]: Foundation едет в общий upstream и не должен
        // требовать лицензию Odin.
        [ContextMenu("Configure Canvas")]
        private void ConfigureCanvas()
        {
            var canvasScaler = TryGetComponent<CanvasScaler>(out var canvasScalerComponent)
                ? canvasScalerComponent
                : gameObject.AddComponent<CanvasScaler>();
            
            canvasScaler.referenceResolution = new Vector2(
                CanvasConstants.DefaultResolution.Width, 
                CanvasConstants.DefaultResolution.Height);
            
            canvasScaler.screenMatchMode = CanvasConstants.DefaultScreenMatchMode;
            canvasScaler.matchWidthOrHeight = CanvasConstants.DefaultMatch;
            
            if (!TryGetComponent<GraphicRaycaster>(out var graphicRaycaster))
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }
    }
}