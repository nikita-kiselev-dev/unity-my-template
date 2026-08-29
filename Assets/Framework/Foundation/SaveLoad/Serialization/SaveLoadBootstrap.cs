using Framework.Foundation.SaveLoad.Serialization.Formatters;
using MemoryPack;
using UnityEngine;

namespace Framework.Foundation.SaveLoad.Serialization
{
    public static class SaveLoadBootstrap
    {
        // internal: EditMode-тесты регистрируют форматтеры сами — RuntimeInitializeOnLoadMethod в них не вызывается.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        internal static void Init()
        {
            MemoryPackFormatterProvider.Register(new BigIntegerFormatter());
        }
    }
}
