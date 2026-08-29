using System;
using System.Collections.Generic;
using System.Text;
using Framework.Foundation.Logger;
using Framework.Foundation.Utilities.Extensions;

namespace Framework.Foundation.Analytics
{
    public class AnalyticsEvent : IAnalyticsEvent
    {
        private readonly HashSet<Type> _services = new();

        private Dictionary<string, object> _parameters;

        public string Name { get; }
        public IReadOnlyDictionary<string, object> Parameters => _parameters;
        public IReadOnlyCollection<Type> Services => _services;

        public AnalyticsEvent(string eventName)
        {
            Name = eventName;
        }

        public AnalyticsEvent AddParameter(string key, object value)
        {
            _parameters ??= new Dictionary<string, object>();
            _parameters.Add(key, value);
            return this;
        }

        public AnalyticsEvent To<T>() where T : IAnalyticsService
        {
            _services.Add(typeof(T));
            return this;
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("EventName:".SetSystemColor());
            stringBuilder.AppendLine($"{Name}");
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("EventParameters:".SetSystemColor());

            if (Parameters == null)
            {
                return stringBuilder.ToString();
            }

            var i = 0;
            var count = Parameters.Count;
            foreach (var parameter in Parameters)
            {
                var parameterLog = AnalyticsConstants.Formats.Parameter.UseAsFormat(
                    parameter.Key,
                    parameter.Value);

                if (i == count - 1)
                {
                    stringBuilder.Append(parameterLog);
                }
                else
                {
                    stringBuilder.AppendLine(parameterLog);
                }

                i++;
            }

            return stringBuilder.ToString();
        }
    }
}