using System.IO;
using Framework.Foundation.File;
using VContainer;

namespace Framework.Foundation.Configs
{
    public sealed class FileConfigStorage : IConfigStorage
    {
        [Inject] private readonly IFileService _fileService;

        public string Description => ConfigStorageConstants.ConfigPath;

        public string Load()
        {
            EnsureDirectory();
            return _fileService.Load<string>(ConfigStorageConstants.ConfigPath);
        }

        public void Save(string json)
        {
            EnsureDirectory();
            _fileService.Save<string>(ConfigStorageConstants.ConfigPath, json);
        }

        public void Quarantine()
        {
            EnsureDirectory();
            var corruptedPath = ConfigStorageConstants.ConfigPath + ".corrupted";
            System.IO.File.Delete(corruptedPath);
            System.IO.File.Move(ConfigStorageConstants.ConfigPath, corruptedPath);
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(ConfigStorageConstants.ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigStorageConstants.ConfigDirectory);
            }
        }
    }
}
