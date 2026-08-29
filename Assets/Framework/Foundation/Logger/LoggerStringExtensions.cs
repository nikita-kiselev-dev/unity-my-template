using Framework.Foundation.Utilities.Extensions;

namespace Framework.Foundation.Logger
{
    public static class LoggerStringExtensions
    {
        public static string FormatAsSystemLog(this string str, string entityName)
        {
            return LoggerConstants.Formats.Default.UseAsFormat(
                LoggerConstants.Colors.System,
                entityName,
                str);
        }
        
        public static string FormatAsFeatureLog(this string str, string entityName)
        {
            return LoggerConstants.Formats.Default.UseAsFormat(
                LoggerConstants.Colors.Feature,
                entityName,
                str);
        }

        public static string FormatAsErrorLog(this string str, string entityName)
        {
            return LoggerConstants.Formats.Default.UseAsFormat(
                LoggerConstants.Colors.Error,
                entityName,
                str);
        }

        public static string SetSystemColor(this string str)
        {
            return LoggerConstants.Formats.JustColor.UseAsFormat(LoggerConstants.Colors.System, str);
        }
        
        public static string SetFeatureColor(this string str)
        {
            return LoggerConstants.Formats.JustColor.UseAsFormat(LoggerConstants.Colors.Feature, str);
        }

        public static string SetErrorColor(this string str)
        {
            return LoggerConstants.Formats.JustColor.UseAsFormat(LoggerConstants.Colors.Error, str);
        }
    }
}