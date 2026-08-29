namespace Framework.Foundation.Utilities
{
    public interface IEntityStatus : IReadOnlyEntityStatus
    {
        EntityStatus Status { get; }
    }
}
