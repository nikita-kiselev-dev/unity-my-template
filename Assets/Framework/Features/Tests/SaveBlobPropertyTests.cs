using System.Collections.Generic;
using System.Numerics;
using Framework.Features.Items.Data;
using Framework.Foundation.SaveLoad;
using Framework.Foundation.SaveLoad.Serialization;
using Framework.Foundation.Tests;
using MemoryPack;
using NUnit.Framework;

namespace Framework.Features.Tests
{
    // Roundtrip сейва на сгенерированных значениях — усиление SaveBlobContractTests, где
    // значения подобраны руками. Формат BigInteger идёт через собственный форматтер, и его
    // границы (машинное слово, длинные числа) обязаны переживать сериализацию любыми.
    public class SaveBlobPropertyTests
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // RuntimeInitializeOnLoadMethod в EditMode не вызывается, а без форматтеров
            // BigInteger не сериализуется.
            SaveLoadBootstrap.Init();
        }

        private static TData Roundtrip<TData>(TData source)
            where TData : SaveBlob, new()
        {
            var bytes = MemoryPackSerializer.Serialize(typeof(TData), source);
            var target = new TData();
            object refValue = target;
            MemoryPackSerializer.Deserialize(typeof(TData), bytes, ref refValue);

            Assert.AreSame(target, refValue, "MemoryPack должен заполнять существующий инстанс, а не создавать новый.");
            return (TData)refValue;
        }

        [Test]
        public void ItemsData_Roundtrip_PreservesEveryAmount_ForAnyValues()
        {
            PropertyCheck.ForAll(
                random => PropertyCheck.Sequence(random, r => PropertyCheck.BigIntegerValue(r), maxLength: 5),
                amounts =>
                {
                    var source = new ItemsData();
                    source.PrepareNewData();

                    var expected = new Dictionary<string, BigInteger>();

                    for (var i = 0; i < amounts.Count; i++)
                    {
                        var key = $"item_{i}";
                        source.AddNewItem(key);
                        Assert.IsTrue(source.AddItem(key, amounts[i]));
                        expected[key] = amounts[i];
                    }

                    var restored = Roundtrip(source);

                    foreach (var pair in expected)
                    {
                        Assert.IsTrue(restored.GetValue(pair.Key).TryGet(out var value), $"ключ {pair.Key} потерян");
                        Assert.AreEqual(pair.Value, value);
                    }
                },
                cases: 20,
                shrink: PropertyCheck.DropLast,
                describe: amounts => $"{amounts.Count} amounts: [{string.Join(", ", amounts)}]");
        }

        [Test]
        public void ItemsData_Roundtrip_KeepsEmptyBlobEmpty()
        {
            PropertyCheck.ForAll(
                random => random.Next(0, 2) == 0,
                _ =>
                {
                    var source = new ItemsData();
                    source.PrepareNewData();

                    var restored = Roundtrip(source);

                    Assert.IsFalse(restored.GetValue("missing").HasValue);
                },
                cases: 5);
        }
    }
}
