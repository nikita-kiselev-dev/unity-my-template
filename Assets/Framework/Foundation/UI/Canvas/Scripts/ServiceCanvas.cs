using UnityEngine;

namespace Framework.Foundation.UI.Canvas
{
    public class ServiceCanvas : MonoBehaviour
    {
        private void Start()
        {
            DontDestroyOnLoad(this);
        }
    }
}
