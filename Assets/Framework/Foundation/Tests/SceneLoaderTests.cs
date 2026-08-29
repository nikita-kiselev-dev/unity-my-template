using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Foundation.Scenes;
using Framework.Foundation.Scenes.Signals;
using Framework.Foundation.Signals;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class SceneLoaderTests
    {
        [Test]
        public void PrepareSceneLoad_RejectsRequest_WhenLoadIsActive()
        {
            var signalBus = new ReactiveSignalBus();
            var loadGate = new UniTaskCompletionSource();
            var loadedScene = string.Empty;
            var firstCallbackCount = 0;
            var secondCallbackCount = 0;
            var logger = new FakeLogChannel();
            var loader = new SceneLoader(
                signalBus,
                () => "Core",
                sceneName =>
                {
                    loadedScene = sceneName;
                    return loadGate.Task;
                },
                (_, _) => UniTask.CompletedTask,
                logger);

            var firstAccepted = loader.PrepareSceneLoad("Meta", () => firstCallbackCount++);
            var loadTask = loader.LoadAsync();
            var secondAccepted = loader.PrepareSceneLoad("Start", () => secondCallbackCount++);

            Assert.IsTrue(firstAccepted);
            Assert.IsFalse(secondAccepted);
            Assert.AreEqual("Meta", loadedScene);
            Assert.AreEqual(1, logger.Errors.Count);
            StringAssert.Contains("Scene load 'Start' rejected", logger.Errors[0]);

            loadGate.TrySetResult();
            loadTask.GetAwaiter().GetResult();

            Assert.AreEqual(1, firstCallbackCount);
            Assert.AreEqual(0, secondCallbackCount);
            ((IDisposable)loader).Dispose();
            signalBus.Dispose();
        }

        [Test]
        public void LoadAsync_TriggersLoadFailedSignal_WhenSceneLoadThrows()
        {
            var signalBus = new ReactiveSignalBus();
            var logger = new FakeLogChannel();
            var failure = new InvalidOperationException("no addressable scene");
            SceneLoadFailedSignal received = null;
            var loader = new SceneLoader(
                signalBus,
                () => "Core",
                _ => UniTask.FromException(failure),
                (_, _) => UniTask.CompletedTask,
                logger);
            signalBus.Subscribe<SceneLoadFailedSignal>(signal => received = signal);

            loader.PrepareSceneLoad("Meta");
            loader.LoadAsync().GetAwaiter().GetResult();

            Assert.IsNotNull(received);
            Assert.AreEqual("Meta", received.SceneName);
            Assert.AreSame(failure, received.Exception);
            Assert.AreEqual(1, logger.Errors.Count);

            // Запрос сброшен: после провала загрузку можно повторить.
            Assert.IsTrue(loader.PrepareSceneLoad("Meta"));
            ((IDisposable)loader).Dispose();
            signalBus.Dispose();
        }

        [Test]
        public void LoadAsync_DoesNotTriggerLoadFailedSignal_WhenRequestIsCancelled()
        {
            var signalBus = new ReactiveSignalBus();
            var logger = new FakeLogChannel();
            var loadGate = new UniTaskCompletionSource();
            var failedCount = 0;
            var loader = new SceneLoader(
                signalBus,
                () => "Core",
                _ => loadGate.Task,
                (_, _) => UniTask.CompletedTask,
                logger);
            signalBus.Subscribe<SceneLoadFailedSignal>(_ => failedCount++);

            loader.PrepareSceneLoad("Meta");
            var loadTask = loader.LoadAsync();
            ((IDisposable)loader).Dispose();
            loadGate.TrySetCanceled();
            loadTask.GetAwaiter().GetResult();

            Assert.AreEqual(0, failedCount);
            signalBus.Dispose();
        }

        [Test]
        public void PrepareSceneLoad_LogsError_WhenLogsDisabled()
        {
            var signalBus = new ReactiveSignalBus();
            var logger = new FakeLogChannel();
            logger.SetLogsStatus(false);
            var loader = new SceneLoader(
                signalBus,
                () => "Core",
                _ => new UniTaskCompletionSource().Task,
                (_, _) => UniTask.CompletedTask,
                logger);

            loader.PrepareSceneLoad("Meta");
            var secondAccepted = loader.PrepareSceneLoad("Start");

            Assert.IsFalse(secondAccepted);
            Assert.AreEqual(1, logger.Errors.Count);
            ((IDisposable)loader).Dispose();
            signalBus.Dispose();
        }
    }
}
