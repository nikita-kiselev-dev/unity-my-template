using Framework.Features.Settings.ViewModel;
using Framework.Foundation.UI.Mvvm;
using Framework.Foundation.UI.Views;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Framework.Features.Settings.View
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class SettingsPopupView : MonoView<SettingsViewModel>
    {
        [SerializeField] private Button m_CloseButton;

        [SerializeField] private Slider m_SoundsVolumeSlider;
        [SerializeField] private Slider m_MusicVolumeSlider;

        protected override void OnBind(SettingsViewModel viewModel)
        {
            // Порядок важен: сначала VM → слайдер (выставляет сохранённое значение),
            // потом слайдер → VM: OnValueChangedAsObservable реплеит текущее значение слайдера
            // при подписке, и к этому моменту оно уже должно совпадать с моделью.
            viewModel.SoundsVolume.Subscribe(volume => m_SoundsVolumeSlider.SetValueWithoutNotify(volume)).AddTo(this);
            viewModel.MusicVolume.Subscribe(volume => m_MusicVolumeSlider.SetValueWithoutNotify(volume)).AddTo(this);

            m_SoundsVolumeSlider.OnValueChangedAsObservable().Subscribe(viewModel.SetSoundsVolume).AddTo(this);
            m_MusicVolumeSlider.OnValueChangedAsObservable().Subscribe(viewModel.SetMusicVolume).AddTo(this);

            m_CloseButton.OnClickAsObservable().Subscribe(_ => Close()).AddTo(this);
        }
    }
}
