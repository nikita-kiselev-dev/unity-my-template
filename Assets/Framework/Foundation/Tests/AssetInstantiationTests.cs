using System.Collections.Generic;
using Framework.Foundation.Asset;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public sealed class AssetInstantiationTests
    {
        [Test]
        public void MissingComponent_ContainsKeyAndTypeName()
        {
            var exception = AssetInstantiation.MissingComponent<List<int>>("ui/window");

            StringAssert.Contains("ui/window", exception.Message);
            StringAssert.Contains("List", exception.Message);
        }
    }
}
