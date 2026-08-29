using System.Collections.Generic;
using System.Numerics;
using Framework.Features.Items.Data;
using Framework.Foundation.Tests;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    // Инварианты экономики на сгенерированных последовательностях операций. Счётчик валюты —
    // ровно тот случай, где пример бесполезен: «не уходит в минус» обязано держаться на любом
    // порядке начислений и трат, а ломается на конкретном.
    public class ItemsDataPropertyTests
    {
        private const string Key = "gem";

        private readonly struct Operation
        {
            public readonly bool IsAdd;
            public readonly BigInteger Value;

            public Operation(bool isAdd, BigInteger value)
            {
                IsAdd = isAdd;
                Value = value;
            }

            public override string ToString() => $"{(IsAdd ? "Add" : "Remove")} {Value}";
        }

        private static ItemsData CreateData()
        {
            var data = new ItemsData();
            data.PrepareNewData();
            data.AddNewItem(Key);
            return data;
        }

        private static List<Operation> Operations(System.Random random)
        {
            return PropertyCheck.Sequence(
                random,
                r => new Operation(r.Next(0, 2) == 0, PropertyCheck.BigIntegerValue(r, allowNonPositive: true)),
                maxLength: 8);
        }

        private static string Describe(List<Operation> operations)
        {
            return $"{operations.Count} operations: [{string.Join(", ", operations)}]";
        }

        [Test]
        public void Value_NeverGoesNegative_ForAnyOperationSequence()
        {
            PropertyCheck.ForAll(
                Operations,
                operations =>
                {
                    var data = CreateData();

                    foreach (var operation in operations)
                    {
                        if (operation.IsAdd)
                        {
                            data.AddItem(Key, operation.Value);
                        }
                        else
                        {
                            data.RemoveItem(Key, operation.Value);
                        }

                        Assert.GreaterOrEqual(data.GetValue(Key).Value, BigInteger.Zero);
                    }
                },
                shrink: PropertyCheck.DropLast,
                describe: Describe);
        }

        [Test]
        public void RejectedOperation_LeavesValueUnchanged_ForAnyOperationSequence()
        {
            PropertyCheck.ForAll(
                Operations,
                operations =>
                {
                    var data = CreateData();

                    foreach (var operation in operations)
                    {
                        var before = data.GetValue(Key).Value;

                        var accepted = operation.IsAdd
                            ? data.AddItem(Key, operation.Value)
                            : data.RemoveItem(Key, operation.Value);

                        // Отказ обязан быть полным: отклонённая операция не имеет права
                        // частично применить себя к состоянию.
                        if (!accepted)
                        {
                            Assert.AreEqual(before, data.GetValue(Key).Value);
                        }
                    }
                },
                shrink: PropertyCheck.DropLast,
                describe: Describe);
        }

        [Test]
        public void NonPositiveAmount_IsAlwaysRejected_ForAnyValue()
        {
            PropertyCheck.ForAll(
                random => PropertyCheck.BigIntegerValue(random),
                value =>
                {
                    var data = CreateData();
                    data.AddItem(Key, value);
                    var before = data.GetValue(Key).Value;

                    Assert.IsFalse(data.AddItem(Key, BigInteger.Zero));
                    Assert.IsFalse(data.AddItem(Key, -value));
                    Assert.IsFalse(data.RemoveItem(Key, BigInteger.Zero));
                    Assert.IsFalse(data.RemoveItem(Key, -value));
                    Assert.AreEqual(before, data.GetValue(Key).Value);
                });
        }

        [Test]
        public void AcceptedAdds_SumUpExactly_ForAnyPositiveSequence()
        {
            PropertyCheck.ForAll(
                random => PropertyCheck.Sequence(random, r => PropertyCheck.BigIntegerValue(r)),
                values =>
                {
                    var data = CreateData();
                    var expected = BigInteger.Zero;

                    foreach (var value in values)
                    {
                        Assert.IsTrue(data.AddItem(Key, value));
                        expected += value;
                    }

                    Assert.AreEqual(expected, data.GetValue(Key).Value);
                },
                shrink: PropertyCheck.DropLast,
                describe: values => $"{values.Count} values: [{string.Join(", ", values)}]");
        }

        [Test]
        public void RemoveAboveBalance_IsRejected_ForAnyBalance()
        {
            PropertyCheck.ForAll(
                random => PropertyCheck.BigIntegerValue(random),
                balance =>
                {
                    var data = CreateData();
                    Assert.IsTrue(data.AddItem(Key, balance));

                    Assert.IsFalse(data.RemoveItem(Key, balance + 1));
                    Assert.AreEqual(balance, data.GetValue(Key).Value);

                    Assert.IsTrue(data.RemoveItem(Key, balance));
                    Assert.AreEqual(BigInteger.Zero, data.GetValue(Key).Value);
                });
        }
    }
}
