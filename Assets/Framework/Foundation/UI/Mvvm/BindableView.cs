using UnityEngine;

namespace Framework.Foundation.UI.Mvvm
{
    public abstract class BindableView<TViewModel> : MonoBehaviour where TViewModel : ViewModel
    {
        protected TViewModel ViewModel { get; private set; }

        // Bind вызывается один раз за жизнь view: подписки в OnBind живут через .AddTo(this)
        // до Destroy, повторный Bind задублирует их.
        public void Bind(TViewModel viewModel)
        {
            ViewModel = viewModel;
            OnBind(viewModel);
        }

        protected abstract void OnBind(TViewModel viewModel);
    }
}
