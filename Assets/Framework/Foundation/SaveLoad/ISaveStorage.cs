using Cysharp.Threading.Tasks;

namespace Framework.Foundation.SaveLoad
{
    public interface ISaveStorage
    {
        string Description { get; }
        UniTask<SaveReadResult> TryReadAsync();
        UniTask WriteAsync(byte[] bytes);
        void Write(byte[] bytes);
        UniTask QuarantineAsync();
    }
}
