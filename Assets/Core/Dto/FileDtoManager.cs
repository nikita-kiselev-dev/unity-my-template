﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.Asset;
using Core.File;
using Core.LiveOps;
using Core.LiveOps.Signals;
using Core.Logger;
using Core.SignalBus;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Core.Dto
{
    public class FileDtoManager : IDtoManager, IDtoReader, IStartable
    {
        [Inject] private readonly IAssetLoader _assetLoader;
        [Inject] private readonly ISignalBus _signalBus;
        [Inject] private readonly IDtoService _dtoService;
        [Inject] private readonly IFileService _fileService;
        
        private readonly ILogManager _logger = new LogManager(nameof(FileDtoManager));
        
        private Dictionary<string, string> _serverDto;
        private Dictionary<string, string> _dataDto;

        private bool _isInited;
        
        public async UniTask<T> Read<T>(string configName)
        {
            if (!_isInited)
            {
                return default;
            }
            
            string config;
            var serverConfigExists = IsConfigExists(configName, _serverDto);
            var dataConfigExists = IsConfigExists(configName, _dataDto);
            var areDtosEqual = AreDtosEqual(_serverDto, _dataDto);

            if (serverConfigExists && !areDtosEqual)
            {
                config = _serverDto[configName];
                Save();
            }
            else if (dataConfigExists)
            {
                config = _dataDto[configName];
            }
            else
            {
                config = await GetDummyDto(configName);
            }
            
            var result = (T)JsonConvert.DeserializeObject(config, typeof(T));
            
            Debug.Log($"{GetType().Name} - loaded config file:\n{result}");
            
            return result;
        }
        
        private void Init()
        {
            TryCreateDirectory();
            TryGetDataDto();
            _signalBus.Subscribe<GetLiveOpsDataCompletedSignal>(this, SetServerDto);
            _isInited = true;
        }
        
        private void Save()
        {
            if (!_isInited || _serverDto == null)
            {
                return;
            }
            
            var stringDto = JsonConvert.SerializeObject(_serverDto);
            _fileService.Save<string>(DtoManagerInfo.ConfigPath, stringDto);
            Debug.Log($"{GetType().Name}: dto config saved, path: {DtoManagerInfo.ConfigPath}");
        }
        
        private void TryCreateDirectory()
        {
            if (!Directory.Exists(DtoManagerInfo.ConfigDirectory))
            {
                Directory.CreateDirectory(DtoManagerInfo.ConfigDirectory);
            }
        }

        private void TryGetDataDto()
        {
            var config = _fileService.Load<string>(DtoManagerInfo.ConfigPath);
            
            if (string.IsNullOrEmpty(config))
            {
                return;
            }
            
            _dataDto = JsonConvert.DeserializeObject<Dictionary<string, string>>(config);
            var stringDto = BuildString(_dataDto);
            Debug.Log($"{GetType().Name} - data config file:\n{stringDto}");
        }

        private bool IsConfigExists(string configName, IReadOnlyDictionary<string, string> dto)
        {
            var isConfigExists = dto != null && dto.ContainsKey(configName);
            return isConfigExists;
        }
        
        private async UniTask<string> GetDummyDto(string configName)
        {
            var configAsset = await _assetLoader.LoadAsync<TextAsset>(configName);
            var config = configAsset.ToString();
            _logger.Log($"Dummy config file:\n{config}.");
            _assetLoader.Release(configName);
            return config;
        }

        private bool AreDtosEqual(Dictionary<string, string> firstDto, Dictionary<string, string> secondDto)
        {
            if (firstDto == null || secondDto == null)
            {
                return false;
            }
            
            var areEqual = firstDto
                .OrderBy(kv => kv.Key)
                .SequenceEqual(secondDto.OrderBy(kvp => kvp.Key));

            return areEqual;
        }

        private void SetServerDto()
        {
            _serverDto = _dtoService.GetDto();

            if (_serverDto is null)
            {
                return;
            }
            
            var stringDto = BuildString(_serverDto);
            Debug.Log($"{GetType().Name} - server config file:\n{stringDto}");
        }
        
        private string BuildString(Dictionary<string, string> dto)
        {
            var stringBuilder = new StringBuilder();

            foreach (var kvp in dto)
            {
                stringBuilder.Append($"{kvp.Key}: {kvp.Value}\n");
            }

            var resultString = stringBuilder.ToString();
            stringBuilder.Clear();
            return resultString;
        }
        
        void IStartable.Start()
        {
            Init();
        }
    }
}