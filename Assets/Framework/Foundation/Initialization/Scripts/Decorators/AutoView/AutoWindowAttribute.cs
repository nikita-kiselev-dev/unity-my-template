using System;

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
}
