using System.IO;
using Framework.Foundation.Logger;
using Framework.Foundation.Utilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Framework.Foundation.Scenes
{
    /// <summary>
    /// Кнопка главного тулбара рядом с Play: запускает игру со сцены бутстрапа независимо от того,
    /// какая сцена открыта. Подменяет <see cref="EditorSceneManager.playModeStartScene"/> только на
    /// время своего запуска — обычный Play после выхода снова стартует открытую сцену.
    /// </summary>
    [InitializeOnLoad]
    internal static class BootstrapPlayButton
    {
        private const string ElementPath = "Raycast Productions/Play From Bootstrap";
        private const string OverrideActiveKey = "BootstrapPlayButton.OverrideActive";
        private const string PreviousStartScenePathKey = "BootstrapPlayButton.PreviousStartScene";

        // Editor-инструмент: статический ctor и фабрику элемента зовёт Unity, контейнера в этот момент нет.
        private static readonly ILogChannel _logger = new LogChannel(nameof(BootstrapPlayButton));

        private static MainToolbarButton _button;

        static BootstrapPlayButton()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle, defaultDockIndex = 0)]
        private static MainToolbarButton CreateButton()
        {
            var content = new MainToolbarContent(
                "Bootstrap",
                EditorGUIUtility.IconContent("PlayButton").image as Texture2D,
                $"Запустить Play со сцены {SceneConstants.Scenes.Bootstrap}");

            _button = new MainToolbarButton(content, StartPlayFromBootstrap)
            {
                enabled = !EditorApplication.isPlayingOrWillChangePlaymode
            };

            return _button;
        }

        private static void StartPlayFromBootstrap()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!TryFindBootstrapScene().TryGet(out var bootstrapScene))
            {
                _logger.LogError($"Scene '{SceneConstants.Scenes.Bootstrap}' is not found, play mode was not started.");
                return;
            }

            var previousStartScene = EditorSceneManager.playModeStartScene;

            SessionState.SetString(
                PreviousStartScenePathKey,
                previousStartScene == null ? string.Empty : AssetDatabase.GetAssetPath(previousStartScene));
            SessionState.SetBool(OverrideActiveKey, true);

            EditorSceneManager.playModeStartScene = bootstrapScene;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (_button != null)
            {
                _button.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
            }

            if (stateChange == PlayModeStateChange.EnteredEditMode)
            {
                RestoreStartScene();
            }
        }

        /// <summary>
        /// Прежнее значение хранится в <see cref="SessionState"/>, а не в статическом поле: между
        /// кликом и выходом из Play редактор перезагружает домен и поле обнулилось бы.
        /// </summary>
        private static void RestoreStartScene()
        {
            if (!SessionState.GetBool(OverrideActiveKey, false))
            {
                return;
            }

            var previousPath = SessionState.GetString(PreviousStartScenePathKey, string.Empty);

            SessionState.EraseBool(OverrideActiveKey);
            SessionState.EraseString(PreviousStartScenePathKey);

            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previousPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousPath);
        }

        /// <summary>
        /// Поиск по имени, а не по пути: сцену можно перемещать, ломаться от этого кнопка не должна.
        /// </summary>
        private static Result<SceneAsset> TryFindBootstrapScene()
        {
            var guids = AssetDatabase.FindAssets($"t:SceneAsset {SceneConstants.Scenes.Bootstrap}");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) != SceneConstants.Scenes.Bootstrap)
                {
                    continue;
                }

                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

                if (scene != null)
                {
                    return Result<SceneAsset>.Success(scene);
                }
            }

            return Result<SceneAsset>.Failure();
        }
    }
}
