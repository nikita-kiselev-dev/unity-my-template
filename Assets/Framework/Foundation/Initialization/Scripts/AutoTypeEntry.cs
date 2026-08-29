using System;
using VContainer;

namespace Framework.Foundation.Initialization
{
    public enum AutoTypeKind
    {
        Service,
        LifecycleEntity,
        SaveBlob
    }

    public readonly struct AutoTypeEntry
    {
        public readonly Type Type;
        public readonly Lifetime Lifetime;
        public readonly AutoTypeKind Kind;

        public AutoTypeEntry(Type type, Lifetime lifetime, AutoTypeKind kind)
        {
            Type = type;
            Lifetime = lifetime;
            Kind = kind;
        }
    }
}
