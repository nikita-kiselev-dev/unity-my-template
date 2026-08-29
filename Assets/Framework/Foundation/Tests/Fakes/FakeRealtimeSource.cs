using System;
using Framework.Foundation.Time;

namespace Framework.Foundation.Tests.Fakes
{
    public sealed class FakeRealtimeSource : IRealtimeSource
    {
        public TimeSpan Elapsed { get; private set; }

        public void Advance(TimeSpan delta) => Elapsed += delta;
    }
}
