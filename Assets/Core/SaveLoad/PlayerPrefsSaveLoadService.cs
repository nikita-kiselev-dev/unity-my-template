﻿using System;
using Core.File;
using Core.Initialization.Scripts;
using Core.Initialization.Scripts.InitOrder;
using Core.Initialization.Scripts.Scopes;
using Core.Logger;
using Cysharp.Threading.Tasks;
using MemoryPack;
using UnityEngine;
using VContainer;

namespace Core.SaveLoad
{
    [ControlEntityOrder(nameof(BootstrapScope), (int)BootstrapSceneInitOrder.SaveLoadService)]
    public class PlayerPrefsSaveLoadService : ControlEntity, ISaveLoadService, IDataSaver
    {
        [Inject] private readonly IDataManager _dataManager;
        [Inject] private readonly IFileService _fileService;

        private readonly ILogManager _logger = new LogManager(nameof(PlayerPrefsSaveLoadService));
        
        private bool _isInited;      
        
        protected override UniTask Init()
        {
            LoadData();
            _isInited = true;
            return UniTask.CompletedTask;
        }

        public void SaveData()
        {
            if (!_isInited)
            {
                return;
            }
            
            var serializedSave = MemoryPackSerializer.Serialize(_dataManager.DataRepository);
            var serializedStringSave = Convert.ToBase64String(serializedSave);
            PlayerPrefs.SetString(SaveLoadInfo.SaveFileName, serializedStringSave);
            PlayerPrefs.Save();
            
            _logger.Log("Save data saved in PlayerPrefs.");
        }
        
        private void LoadData()
        {
            _dataManager.PrepareNewData();
            var saveFile = PlayerPrefs.GetString(SaveLoadInfo.SaveFileName);
            
            if (string.IsNullOrEmpty(saveFile))
            {
                _logger.Log("Save data is empty, nothing to load.");
                return;
            }

            var byteArraySave = Convert.FromBase64String(saveFile);
            var deserializedSave = MemoryPackSerializer.Deserialize<IDataRepository>(byteArraySave);
            _dataManager.SetDataRepository(deserializedSave);
            
            _logger.Log("Save data loaded from PlayerPrefs.");
        }
    }
}