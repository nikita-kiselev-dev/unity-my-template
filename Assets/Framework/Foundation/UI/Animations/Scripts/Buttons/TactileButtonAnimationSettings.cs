using PrimeTween;
using UnityEngine;

namespace Framework.Foundation.UI.Animations.Buttons
{
    [CreateAssetMenu(fileName = "TactileButtonAnimationSettings", menuName = "ScriptableObjects/Animation/UI/TactileButtonAnimationSettings", order = 1)]
    public class TactileButtonAnimationSettings : ScriptableObject
    {
        [Header("Press Settings (Down)")]
        [SerializeField] private float m_PressDuration = 0.1f;
        [SerializeField] private float m_BackgroundPressScale = 0.9f;
        [SerializeField] private float m_ContentPressScale = 0.8f;

        [Header("Release Settings (Up)")]
        [SerializeField] private float m_ReleaseDuration = 0.5f;
        [SerializeField] private Ease m_ReleaseEase = Ease.OutElastic;

        public float PressDuration => m_PressDuration;
        public float BackgroundPressScale => m_BackgroundPressScale;
        public float ContentPressScale => m_ContentPressScale;
        public float ReleaseDuration => m_ReleaseDuration;
        public Ease ReleaseEase => m_ReleaseEase;
    }
}
