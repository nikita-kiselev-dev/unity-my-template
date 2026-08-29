using System;
using VContainer;

namespace Framework.Foundation.Initialization
{
    /// <summary>
    /// Саморегистрация типа в root scope через <c>RegisterAutoTypes</c>.
    /// <see cref="LifecycleEntity"/>-наследники регистрируются как <c>LifecycleEntity</c>,
    /// остальные типы — <c>AsSelf</c> + <c>AsImplementedInterfaces</c>.
    /// <c>Lifetime.Scoped</c> означает «инстанс на сценовый scope, умирает со сценой».
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AutoRegistrationAttribute : Attribute
    {
        public Lifetime Lifetime { get; }

        public AutoRegistrationAttribute(Lifetime lifetime = Lifetime.Scoped)
        {
            Lifetime = lifetime;
        }
    }
}
