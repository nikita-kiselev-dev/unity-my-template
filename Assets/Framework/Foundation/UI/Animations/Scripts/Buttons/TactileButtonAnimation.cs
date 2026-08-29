using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Framework.Foundation.UI.Animations.Buttons
{
    public class TactileButtonAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private TactileButtonAnimationSettings m_Config;
        [SerializeField] private Transform m_ContentTransform;

        private Sequence _sequence;

        public void OnPointerDown(PointerEventData eventData)
        {
            _sequence.Complete();

            _sequence = Sequence
                .Create()
                .Group(Tween.Scale(transform, m_Config.BackgroundPressScale, m_Config.PressDuration, Ease.OutQuad))
                .Group(Tween.Scale(m_ContentTransform, m_Config.ContentPressScale, m_Config.PressDuration, Ease.OutQuad));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _sequence.Complete();

            _sequence = Sequence
                .Create()
                .Group(Tween.Scale(transform, 1f, m_Config.ReleaseDuration, m_Config.ReleaseEase))
                .Group(Tween.Scale(m_ContentTransform, 1f, m_Config.ReleaseDuration, m_Config.ReleaseEase));
        }

        private void OnDisable()
        {
            _sequence.Complete();
        }
    }
}