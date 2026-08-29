using System;

namespace Framework.Foundation.Initialization.Decorators.AutoView
{
    [AttributeUsage(AttributeTargets.Field)]
    public class AutoPopupAttribute : Attribute
    {
        public string ViewKey { get; }

        public AutoPopupAttribute(string viewKey)
        {
            ViewKey = viewKey;
        }
    }
}
