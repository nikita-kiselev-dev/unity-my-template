using System.IO;
using UnityEngine;

namespace Framework.Foundation.Configs
{
    public static class ConfigStorageConstants
    {
        public const string ConfigName = "Config.bin";
        public static readonly string ConfigDirectory = $"{Application.persistentDataPath}/Data/";
        public static readonly string ConfigPath = Path.Combine(ConfigDirectory, ConfigName);
    }
}