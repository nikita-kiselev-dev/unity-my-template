using System.Numerics;
using Framework.Foundation.Initialization.Decorators.AutoLogger;
using Framework.Features.Items;
using Framework.Foundation.Logger;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Framework.Features.Items.View
{
    [AutoLogger(ItemsConstants.LogName, LogCategory.Feature)]
    public partial class CurrencyView : MonoBehaviour
    {
        [Inject] private readonly IInventory _inventory;

        [SerializeField] private string m_Key;
        [SerializeField] private Image m_IconImage;
        [SerializeField] private TMP_Text m_ValueText;

        private void Start()
        {
            if (_inventory.TryGetCounter(m_Key, out var itemCounter))
            {
                gameObject.SetActive(true);

                // Значение меняется пачками (серия тапов за кадр), а увидеть игрок может только
                // последнее: без схлопывания каждый промежуточный тап платил бы за
                // BigInteger.ToString (280 B на 41-значное число).
                itemCounter.Info.Value
                    .ThrottleLastFrame(1, UnityFrameProvider.Update)
                    .Subscribe(SetValue)
                    .AddTo(this);

                SetIcon(itemCounter.Info.Icon);
            }
            else
            {
                gameObject.SetActive(false);
                Logger.LogError($"Item {m_Key} not found!");
            }
        }
        
        private void SetIcon(Sprite icon)
        {
            m_IconImage.sprite = icon;
        }

        private void SetValue(BigInteger value)
        {
            m_ValueText.text = value.ToString();
        }
    }
}