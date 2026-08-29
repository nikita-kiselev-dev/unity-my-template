using Framework.Foundation.Utilities;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    public class ResultTests
    {
        [Test]
        public void Success_PreservesValue_AndHasValue()
        {
            var result = Result<int>.Success(42);

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(42, result.Value);
        }

        [Test]
        public void Failure_HasNoValue()
        {
            var result = Result<int>.Failure();

            Assert.IsFalse(result.HasValue);
        }

        [Test]
        public void TryGet_ReturnsTrue_AndOutValue_WhenExists()
        {
            var result = Result<string>.Success("ok");

            Assert.IsTrue(result.TryGet(out var value));
            Assert.AreEqual("ok", value);
        }

        [Test]
        public void TryGet_ReturnsFalse_WhenMissing()
        {
            var result = Result<string>.Failure();

            Assert.IsFalse(result.TryGet(out _));
        }

        [Test]
        public void GetValueOrDefault_ReturnsValue_WhenExists()
        {
            var result = Result<int>.Success(5);

            Assert.AreEqual(5, result.GetValueOrDefault(99));
        }

        [Test]
        public void GetValueOrDefault_ReturnsFallback_WhenMissing()
        {
            var result = Result<int>.Failure();

            Assert.AreEqual(99, result.GetValueOrDefault(99));
        }
    }
}
