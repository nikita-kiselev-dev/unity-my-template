using UnityEngine;

namespace Framework.Foundation.Configs
{
    public sealed class PlayerPrefsConfigStorage : IConfigStorage
    {
        public string Description => $"PlayerPrefs[{ConfigStorageConstants.ConfigName}]";

        public string Load() => PlayerPrefs.GetString(ConfigStorageConstants.ConfigName);

        public void Save(string json)
        {
            PlayerPrefs.SetString(ConfigStorageConstants.ConfigName, json);
            PlayerPrefs.Save();
        }

        public void Quarantine()
        {
            var json = PlayerPrefs.GetString(ConfigStorageConstants.ConfigName);
            PlayerPrefs.SetString($"{ConfigStorageConstants.ConfigName}.corrupted", json);
            PlayerPrefs.DeleteKey(ConfigStorageConstants.ConfigName);
            PlayerPrefs.Save();
        }
    }
}
