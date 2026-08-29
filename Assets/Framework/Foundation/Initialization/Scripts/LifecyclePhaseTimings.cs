using System.Collections.Generic;
using System.Text;
using ZLinq;

namespace Framework.Foundation.Initialization
{
    public sealed class LifecyclePhaseTimings
    {
        private readonly List<Entry> _entries = new();

        public void Add(string entityName, long milliseconds)
        {
            _entries.Add(new Entry(entityName, milliseconds));
        }

        public string Describe()
        {
            if (_entries.Count == 0)
            {
                return string.Empty;
            }

            var ordered = _entries.AsValueEnumerable().OrderByDescending(entry => entry.Milliseconds).ToArray();
            var builder = new StringBuilder();

            for (var i = 0; i < ordered.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("\n");
                }

                builder.Append(ordered[i].EntityName).Append(": ").Append(ordered[i].Milliseconds).Append("ms");
            }

            return builder.ToString();
        }

        private readonly struct Entry
        {
            public readonly string EntityName;
            public readonly long Milliseconds;

            public Entry(string entityName, long milliseconds)
            {
                EntityName = entityName;
                Milliseconds = milliseconds;
            }
        }
    }
}
