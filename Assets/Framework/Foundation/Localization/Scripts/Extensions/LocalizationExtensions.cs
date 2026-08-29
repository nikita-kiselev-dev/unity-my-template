using Cysharp.Threading.Tasks;
using UnityEngine.Localization;

namespace Framework.Foundation.Localization.Extensions
{
    public static class LocalizationExtensions
    {
        public static UniTask<string> Localize(this string localizationKey)
        {
            var localizedString = new LocalizedString(LocalizationConstants.Tables.General, localizationKey);
            var localizedStringOperation = localizedString.GetLocalizedStringAsync().ToUniTask();
            return localizedStringOperation;
        }
        
        public static UniTask<string> Localize(this string localizationKey, string localizationTableKey)
        {
            var localizedString = new LocalizedString(localizationTableKey, localizationKey);
            var localizedStringOperation = localizedString.GetLocalizedStringAsync().ToUniTask();
            return localizedStringOperation;
        }
    }
}