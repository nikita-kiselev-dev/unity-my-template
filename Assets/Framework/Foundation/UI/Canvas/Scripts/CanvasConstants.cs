using UnityEngine.UI;

namespace Framework.Foundation.UI.Canvas
{
    public static class CanvasConstants
    {
        public static class Canvases
        {
            public const string Popup = "PopupCanvas";
            public const string Window = "WindowCanvas";
        }

        public static class DefaultResolution
        {
            public const int Width = 1920;
            public const int Height = 1080;
        }

        public const CanvasScaler.ScreenMatchMode DefaultScreenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        public const float DefaultMatch = 0f;
    }
}