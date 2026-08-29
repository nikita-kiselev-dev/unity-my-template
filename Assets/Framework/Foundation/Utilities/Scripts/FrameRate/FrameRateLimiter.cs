using UnityEngine;

namespace Framework.Foundation.Utilities.FrameRate
{
    public class FrameRateLimiter : MonoBehaviour
    {
        [SerializeField] private TargetFrameRate m_TargetFrameRate = TargetFrameRate.Middle60;
        
        private void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = (int)m_TargetFrameRate;
        }
    }
}