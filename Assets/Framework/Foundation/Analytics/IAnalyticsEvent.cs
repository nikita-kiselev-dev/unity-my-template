using System;
using System.Collections.Generic;

namespace Framework.Foundation.Analytics
{
    public interface IAnalyticsEvent
    {
        string Name { get; }
        IReadOnlyDictionary<string, object> Parameters { get; }
        IReadOnlyCollection<Type> Services { get; }
        string ToString();
    }
}