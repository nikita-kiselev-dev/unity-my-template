using System;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Logger;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class UniTaskExtensions
    {
        public static void Forget(this UniTask task, ILogChannel logger)
        {
            ForgetCore(task, logger).Forget();
        }

        public static void Forget(this UniTask task, Action<Exception> onError)
        {
            ForgetCore(task, onError).Forget();
        }

        private static async UniTaskVoid ForgetCore(UniTask task, ILogChannel logger)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                logger.LogError(e.ToString());
            }
        }

        private static async UniTaskVoid ForgetCore(UniTask task, Action<Exception> onError)
        {
            try
            {
                await task;
            }
            catch (Exception e)
            {
                onError?.Invoke(e);
            }
        }

    }
}
