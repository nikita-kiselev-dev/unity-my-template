using PrimeTween;
using UnityEngine;

namespace Framework.Foundation.UI.Animations
{
    public class PopTextAnimation : MonoBehaviour
    {
        private const float HalfLoopDuration = 1.5f;
        private readonly Vector3 _endScale = new(1.2f, 1.2f, 1.2f);

        private Sequence _sequence;

        private void OnEnable()
        {
            Animate();
        }

        private void OnDisable()
        {
            _sequence.Complete();
        }

        private void Animate()
        {
            _sequence = Sequence
                .Create(sequenceEase: Ease.InSine, cycles: -1)
                .Chain(Tween.Scale(transform, _endScale, HalfLoopDuration))
                .Chain(Tween.Scale(transform, Vector3.one, HalfLoopDuration));
        }
    }
}
