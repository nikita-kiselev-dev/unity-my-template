using R3;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Framework.Features.Clicker.View
{
    public class ClickAreaView : MonoBehaviour, IPointerDownHandler
    {
        private readonly Subject<Unit> _clicked = new();

        public Observable<Unit> Clicked => _clicked;

        public void OnPointerDown(PointerEventData eventData) => _clicked.OnNext(Unit.Default);

        private void OnDestroy() => _clicked.Dispose();
    }
}
