namespace Framework.Foundation.Time
{
    /// <summary>
    /// Насколько можно доверять <c>IClock.ServerUtcNow</c>.
    /// Дефолт — <see cref="LocalFallback"/>: до синхронизации доверия нет.
    /// </summary>
    public enum ClockTrust
    {
        LocalFallback,
        ServerVerified
    }
}
