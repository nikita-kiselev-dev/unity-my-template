namespace Framework.Foundation.Utilities
{
    public interface IReadOnlyEntityStatus
    {
        bool IsEnabled { get; }
        bool IsInited { get; }
        bool IsActive { get; }
    }
}
