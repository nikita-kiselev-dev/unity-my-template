using UnityEngine;

namespace Framework.Foundation.UI.LoadingCurtain.View
{
    /// <summary>
    /// Абстрактный класс, а не интерфейс: MonoBehaviour за интерфейсом не сериализуется
    /// в Inspector, поэтому ссылку на шторку нельзя было бы проставить в префабе.
    /// </summary>
    public abstract class LoadingCurtainViewBase : MonoBehaviour
    {
        public abstract void SetLoadingText(string text);
    }
}