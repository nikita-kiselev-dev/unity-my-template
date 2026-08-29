namespace AutoDecorators.Generator.Tests
{
    /// <summary>
    /// Минимальные копии типов Core, на которые опирается генератор и сгенерированный код.
    /// Настоящий Core.dll собирает Unity, в CI его нет — поэтому компилируем тестовый код
    /// против стабов. Сигнатуры обязаны совпадать с Assets/Framework/Foundation:
    /// AutoWindowAttribute/AutoPopupAttribute/AutoLoggerAttribute, IAutoViewHost, AutoViewBinding, ViewKind, MonoView,
    /// ILogChannel(Factory), LogCategory, LifecycleEntity.EnableStatusLogs, VContainer.Inject.
    /// </summary>
    internal static class FrameworkStubs
    {
        public const string Source = @"
using System;

namespace Framework.Foundation.Logger
{
    public enum LogCategory
    {
        System,
        Feature
    }

    public interface ILogChannel
    {
        void Log(string message);
    }

    public interface ILogChannelFactory
    {
        ILogChannel Get(string entityName, LogCategory entityType = LogCategory.System);
    }
}

namespace Framework.Foundation.UI.Views
{
    public enum ViewKind
    {
        Window,
        Popup
    }

    public abstract class MonoView
    {
    }
}

namespace Framework.Foundation.Initialization
{
    public abstract class LifecycleEntity
    {
        protected void EnableStatusLogs(Framework.Foundation.Logger.LogCategory entityType =
            Framework.Foundation.Logger.LogCategory.System)
        {
        }
    }
}

namespace Framework.Foundation.Initialization.Decorators.AutoView
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoWindowAttribute : Attribute
    {
        public string ViewKey { get; }

        public AutoWindowAttribute(string viewKey)
        {
            ViewKey = viewKey;
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class AutoPopupAttribute : Attribute
    {
        public string ViewKey { get; }

        public AutoPopupAttribute(string viewKey)
        {
            ViewKey = viewKey;
        }
    }

    public readonly struct AutoViewBinding
    {
        public readonly string ViewKey;
        public readonly Framework.Foundation.UI.Views.ViewKind ViewKind;
        public readonly Action<Framework.Foundation.UI.Views.MonoView> Assign;

        public AutoViewBinding(
            string viewKey,
            Framework.Foundation.UI.Views.ViewKind viewKind,
            Action<Framework.Foundation.UI.Views.MonoView> assign)
        {
            ViewKey = viewKey;
            ViewKind = viewKind;
            Assign = assign;
        }
    }

    public interface IAutoViewHost
    {
        AutoViewBinding[] GetAutoViewBindings();
    }
}

namespace Framework.Foundation.Initialization.Decorators.AutoLogger
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AutoLoggerAttribute : Attribute
    {
        public string LogName { get; }
        public Framework.Foundation.Logger.LogCategory EntityType { get; }
        public bool StatusLogs { get; set; }

        public AutoLoggerAttribute(
            string logName,
            Framework.Foundation.Logger.LogCategory entityType = Framework.Foundation.Logger.LogCategory.System)
        {
            LogName = logName;
            EntityType = entityType;
        }
    }
}

namespace VContainer
{
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InjectAttribute : Attribute
    {
    }
}
";
    }
}
