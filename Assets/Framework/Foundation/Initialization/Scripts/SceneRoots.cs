using UnityEngine;

namespace Framework.Foundation.Initialization
{
    public class SceneRoots : MonoBehaviour
    {
        [SerializeField] private Transform m_SystemsRoot;
        [SerializeField] private Transform m_UserInterfaceRoot;
        
        public Transform SystemsRoot => m_SystemsRoot;
        public Transform UserInterfaceRoot => m_UserInterfaceRoot;
    }
}