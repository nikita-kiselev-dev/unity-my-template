using System.IO;

namespace Framework.Foundation.File
{
    public class CsvFileStore : IFileStore
    {
        public object Load(string filePath)
        {
            var text = System.IO.File.ReadAllText(filePath);
            return CsvParser.Read(text);
        }

        public void Save(string filePath, object fileContent)
        {
            using var writer = new StreamWriter(filePath, false);
            writer.WriteLine(fileContent);
        }
    }
}