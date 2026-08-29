namespace Framework.Foundation.Configs
{
    public interface IConfigStorage
    {
        string Description { get; }
        string Load();
        void Save(string json);
        void Quarantine();
    }
}
