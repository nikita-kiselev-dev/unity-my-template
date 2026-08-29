using System.IO;
using UnityEngine;

namespace Framework.Foundation.SaveLoad
{
    public static class SaveLoadConstants
    {
        public const string SaveFileName = "SaveFile.bin";
        public static readonly string SaveFileDirectory = $"{Application.persistentDataPath}/Data/";
        public static readonly string SaveFilePath = Path.Combine(SaveFileDirectory, SaveFileName);
    }
}