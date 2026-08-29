using System;
using System.Collections.Generic;
using Framework.Foundation.Initialization;
using VContainer;

namespace Framework.Foundation.File
{
    [AutoRegistration(Lifetime.Singleton)]
    public class FileService : IFileService
    {
        private readonly Dictionary<Type, IFileStore> _fileStores = new();

        public FileService()
        {
            _fileStores.Add(typeof(string), new StringFileStore());
            _fileStores.Add(typeof(byte[]), new ByteArrayFileStore());
            _fileStores.Add(typeof(CsvTable), new CsvFileStore());
        }

        public T Load<T>(string filePath)
        {
            var fileStore = GetFileStore<T>();
            var result = fileStore.Load(filePath);
            var convertedResult = (T)result;
            return convertedResult;
        }

        public void Save<T>(string filePath, object fileContent)
        {
            var fileStore = GetFileStore<T>();
            fileStore.Save(filePath, fileContent);
        }

        private IFileStore GetFileStore<T>()
        {
            var type = typeof(T);

            if (!_fileStores.TryGetValue(type, out var fileStore))
            {
                throw new ArgumentNullException($"FileService: there is no file store for type: {type}!");
            }

            return fileStore;
        }
    }
}