using System.Numerics;
using Framework.Foundation.Utilities;
using NUnit.Framework;

namespace Framework.Foundation.Tests
{
    // Инварианты Result<T> на сгенерированных значениях: примеры здесь особенно слабы, потому
    // что весь смысл типа — одинаково вести себя на любом содержимом.
    public class ResultPropertyTests
    {
        [Test]
        public void TryGetAndFallback_AgreeWithHasValue_ForAnyValue()
        {
            PropertyCheck.ForAll(
                random => PropertyCheck.BigIntegerValue(random, allowNonPositive: true),
                value =>
                {
                    var success = Result<BigInteger>.Success(value);
                    var failure = Result<BigInteger>.Failure();

                    Assert.IsTrue(success.TryGet(out var restored));
                    Assert.AreEqual(value, restored);
                    Assert.AreEqual(value, success.GetValueOrDefault(BigInteger.MinusOne));

                    Assert.IsFalse(failure.TryGet(out _));
                    Assert.AreEqual(BigInteger.MinusOne, failure.GetValueOrDefault(BigInteger.MinusOne));
                });
        }
    }
}
