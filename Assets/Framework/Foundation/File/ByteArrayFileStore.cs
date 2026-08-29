namespace Framework.Foundation.File
{
    public class ByteArrayFileStore : IFileStore
    {
        public object Load(string filePath)
        {
            var loadedObject = System.IO.File.Exists(filePath) ? System.IO.File.ReadAllBytes(filePath) : null;
            return loadedObject;
        }

        public void Save(string filePath, object fileContent)
        {
            // Прямая запись при падении процесса оставляет обрезанный файл,
            // поэтому пишем во временный и атомарно подменяем, сохраняя .bak предыдущей версии.
            var tempPath = filePath + ".tmp";
            System.IO.File.WriteAllBytes(tempPath, (byte[])fileContent);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Replace(tempPath, filePath, filePath + ".bak");
            }
            else
            {
                System.IO.File.Move(tempPath, filePath);
            }
        }
    }
}