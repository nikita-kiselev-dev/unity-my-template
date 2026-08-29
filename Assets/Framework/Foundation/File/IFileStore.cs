namespace Framework.Foundation.File
{
    public interface IFileStore
    {
        public object Load(string filePath);
        public void Save(string filePath, object fileContent);
    }
}