using System.IO;

namespace Framework.Foundation.File
{
    public class StringFileStore : IFileStore
    {
        public object Load(string filePath)
        {
            var loadedObject = System.IO.File.Exists(filePath) ? System.IO.File.ReadAllText(filePath) : null;
            return loadedObject;
        }
        
        public void Save(string filePath, object fileContent)
        {
            using var writer = new StreamWriter(filePath, false);
            writer.WriteLine(fileContent);
        }
    }
}