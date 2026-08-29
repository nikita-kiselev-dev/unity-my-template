using Framework.Foundation.UI.Views.ViewAnimation;
using PrimeTween;
using UnityEngine;

namespace Framework.Foundation.UI.Animations.Popup
{
    [CreateAssetMenu(fileName = "PopupObjectJumpAnimationSettings", menuName = "ScriptableObjects/Animation/UI/PopupObjectJumpAnimationSettings", order = 1)]
    public class PopupObjectJumpAnimationSettings : ScriptableObject
    {
        [SerializeField] private float m_PopInDuration = 0.15f;
        [SerializeField] private float m_PopOutDuration = 0.1f;
        [SerializeField] private float m_TransformDelta = 25.0f;
        [SerializeField] private float m_Delay = ViewAnimationConstants.Popup.PopInDuration - 0.15f;
        [SerializeField] private Vector3 m_EndScale = new(1.2f, 1.2f, 1.2f);
        [SerializeField] private Ease m_StartEase = Ease.OutBack;
        [SerializeField] private Ease m_EndEase = Ease.Linear;

        public float PopInDuration => m_PopInDuration;
        public float PopOutDuration => m_PopOutDuration;
        public float TransformDelta => m_TransformDelta;
        public float Delay => m_Delay;
        public Vector3 EndScale => m_EndScale;
        public Ease StartEase => m_StartEase;
        public Ease EndEase => m_EndEase;
    }
}
