using UnityEngine;
using ZLinq;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class CanvasExtensions
    {
        public static void SetCameraByTag(this Canvas canvas, string cameraTag)
        {
            var mainCamera = GameObject
                .FindGameObjectsWithTag(cameraTag)
                .AsValueEnumerable()
                .First()
                .GetComponent<Camera>();

            canvas.worldCamera = mainCamera;
        }
    }
}
