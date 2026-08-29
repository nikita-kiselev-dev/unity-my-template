using System.Threading;

namespace Framework.Foundation.Utilities.Extensions
{
    public static class CancellationTokenExtensions
    {
        public static CancellationTokenSource LinkedWith(this CancellationToken token)
        {
            return CancellationTokenSource.CreateLinkedTokenSource(token);
        }
    }
}
