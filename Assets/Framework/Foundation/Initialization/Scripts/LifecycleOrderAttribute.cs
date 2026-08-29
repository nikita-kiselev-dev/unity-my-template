using System;

namespace Framework.Foundation.Initialization
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class LifecycleOrderAttribute : Attribute
    {
        public string SceneScopeName { get; }
        public int InitOrder { get; }
        
        public LifecycleOrderAttribute(string sceneScopeName, int initOrder = int.MaxValue)
        {
            SceneScopeName = sceneScopeName;
            InitOrder = initOrder;
        }
    }
}