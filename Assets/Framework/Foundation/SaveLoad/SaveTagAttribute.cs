using System;

namespace Framework.Foundation.SaveLoad
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class SaveTagAttribute : Attribute
    {
        public ushort Tag { get; }

        public SaveTagAttribute(ushort tag) => Tag = tag;
    }
}
