using Framework.Foundation.Utilities;

namespace Framework.Foundation.Localization
{
    /// <summary>
    /// Источник кода языка игрока: платформа, лаунчер, системные настройки. Синхронный — читается
    /// в фазе <c>Init</c>, когда всё внешнее уже готово по барьеру фаз. Отсутствие языка (источник
    /// не поднялся, платформа не дала) — штатный исход, а не исключение.
    /// </summary>
    public interface ILocaleSource
    {
        Result<string> TryGetLocaleCode();
    }
}
