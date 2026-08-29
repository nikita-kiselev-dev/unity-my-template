namespace Framework.Foundation.Utilities.Extensions
{
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string str)
        {
            return string.IsNullOrEmpty(str);
        }

        public static string UseAsFormat(this string str, params object[] args)
        {
            return string.Format(str, args);
        }
    }
}