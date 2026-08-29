using System.Collections.Generic;
using Framework.Foundation.Utilities.Extensions;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class EnumerableExtensionsTests
    {
        [Test]
        public void IsEmpty_True_ForEmptyCollection()
        {
            Assert.IsTrue(new List<int>().IsEmpty());
        }

        [Test]
        public void IsEmpty_False_ForNonEmptyCollection()
        {
            Assert.IsFalse(new List<int> { 1 }.IsEmpty());
        }

        [Test]
        public void IsEmpty_True_ForEmptyLazySequence()
        {
            Assert.IsTrue(EmptySequence().IsEmpty());
        }

        [Test]
        public void IsEmpty_False_ForNonEmptyLazySequence()
        {
            Assert.IsFalse(SingleSequence().IsEmpty());
        }

        private static IEnumerable<int> EmptySequence()
        {
            yield break;
        }

        private static IEnumerable<int> SingleSequence()
        {
            yield return 1;
        }
    }
}
