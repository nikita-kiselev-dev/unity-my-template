using System.IO;
using Framework.Foundation.Initialization.Scopes;
using Framework.Foundation.Logger;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Foundation.SaveLoad
{
    public class SaveLoadMenu
    {
        // Editor-меню: статические [MenuItem]-методы вызывает Unity, контейнера в этот момент нет.
        private static readonly ILogChannel _logger = new LogChannel<SaveLoadMenu>();

        [MenuItem("Raycast Productions/Data/Open Folder")]
        private static void OpenSaveFolder()
        {
            CreateDirectory();
            EditorUtility.RevealInFinder(SaveLoadConstants.SaveFileDirectory);
            _logger.Log("Save Folder Opened.");
        }

        [MenuItem("Raycast Productions/Data/Clean All")]
        private static void DeleteSaveAndConfigs()
        {
            if (Application.isPlaying && !TryResetRuntimeData())
            {
                return;
            }

            DeleteSaveFiles();

            // Без Save() удаление живёт только в памяти процесса до выхода из редактора.
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            _logger.Log("Save and Configs are deleted.");
        }

        /// <summary>
        /// Диск — только половина сейва: живой <see cref="ISaveEnvelope"/> переживает чистку и
        /// возвращает прогресс ближайшим автосейвом или записью на выходе из Play. Поэтому
        /// конверт сбрасывается до остановки — тогда quit-сейв запишет уже пустые данные.
        /// </summary>
        private static bool TryResetRuntimeData()
        {
            var isConfirmed = EditorUtility.DisplayDialog(
                "Clean All",
                "В Play mode сохранённые данные живут в памяти и будут записаны обратно при выходе.\n\n" +
                "Сбросить их и остановить Play mode?",
                "Сбросить и остановить",
                "Отмена");

            if (!isConfirmed)
            {
                return false;
            }

            var rootScope = LifetimeScope.Find<RootScope>();

            if (rootScope == null || rootScope.Container == null)
            {
                _logger.LogError("Root scope is not available, runtime save data was not reset.");
                return false;
            }

            rootScope.Container.Resolve<ISaveEnvelope>().PrepareNewData();
            EditorApplication.isPlaying = false;

            return true;
        }

        private static void DeleteSaveFiles()
        {
            if (!Directory.Exists(SaveLoadConstants.SaveFileDirectory))
            {
                return;
            }

            var filePaths = Directory.GetFiles(SaveLoadConstants.SaveFileDirectory);

            foreach (var filePath in filePaths)
            {
                System.IO.File.Delete(filePath);
            }
        }

        private static void CreateDirectory()
        {
            if (!Directory.Exists(SaveLoadConstants.SaveFileDirectory))
            {
                Directory.CreateDirectory(SaveLoadConstants.SaveFileDirectory);
            }
        }
    }
}
