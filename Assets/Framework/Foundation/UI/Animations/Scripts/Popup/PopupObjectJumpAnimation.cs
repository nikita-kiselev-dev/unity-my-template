using PrimeTween;
using UnityEngine;

namespace Framework.Foundation.UI.Animations.Popup
{
    public class PopupObjectJumpAnimation : MonoBehaviour
    {
        [SerializeField] private PopupObjectJumpAnimationSettings m_Config;

        private RectTransform _rectTransform;
        private Vector3 _startScale;
        private Vector2 _startPosition;
        private Vector2 _parentPanelCenter;
        private Sequence _sequence;
        
        private void Awake()
        {
            _rectTransform = gameObject.transform.GetComponent<RectTransform>();
            _startScale = _rectTransform.localScale;
            _startPosition = _rectTransform.anchoredPosition;
            GetParentPanelCenter();
        }

        private void OnEnable()
        {
            Show();
        }

        private void OnDisable()
        {
            _sequence.Complete();
        }

        // ContextMenu вместо Odin [Button]: Foundation едет в общий upstream и не должен
        // требовать лицензию Odin.
        [ContextMenu("Show")]
        private void Show()
        {
            BeforeShow();
            
            var direction = (_startPosition - _parentPanelCenter).normalized;
            var endLocalPos = _startPosition + direction * m_Config.TransformDelta;

            _sequence = Sequence
                .Create()
                .ChainDelay(m_Config.Delay)
                .Chain(Tween.Scale(_rectTransform, m_Config.EndScale, m_Config.PopInDuration, m_Config.StartEase))
                .Group(Tween.UIAnchoredPosition(_rectTransform, endLocalPos, m_Config.PopInDuration, m_Config.StartEase))
                .Chain(Tween.UIAnchoredPosition(_rectTransform, _startPosition, m_Config.PopOutDuration, m_Config.EndEase))
                .Group(Tween.Scale(_rectTransform, _startScale, m_Config.PopOutDuration, m_Config.EndEase))
                .OnComplete(Reset);
        }

        private void BeforeShow()
        {
            _sequence.Complete();
            Reset();
        }
        
        private void GetParentPanelCenter()
        {
            var panel = _rectTransform.parent.GetComponent<RectTransform>();
            
            var newPosition = new Vector2(
                panel.rect.width * 0.5f - panel.rect.width * panel.pivot.x,
                panel.rect.height * 0.5f - panel.rect.height * panel.pivot.y);

            var panelCenterWorld = panel.TransformPoint(newPosition);
            _parentPanelCenter = _rectTransform.InverseTransformPoint(panelCenterWorld);
        }

        private void Reset()
        {
            _rectTransform.localScale = _startScale;
            _rectTransform.anchoredPosition = _startPosition;
        }
    }
}