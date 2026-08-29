using System.Numerics;
using Framework.Foundation.SaveLoad;
using MemoryPack;

namespace Framework.Foundation.Tests.Fakes
{
    /// Блоб с полезной нагрузкой на BigInteger: конверту нужен сосед, который переживает
    /// roundtrip, а BigIntegerFormatter — единственный кастомный форматтер сейва.
    [SaveTag(60004)]
    [MemoryPackable]
    public partial class AmountTestData : SaveBlob
    {
        public BigInteger Amount { get; set; }

        public override void PrepareNewData()
        {
            Amount = BigInteger.Zero;
        }
    }

    // Пара классов с одним тегом эмулирует эволюцию схемы: сейв пишется V1, читается V2.
    public static class VersionedTestData
    {
        public const ushort Tag = 60001;
    }

    // Схема, из которой убрали сериализуемый член: payload «широкой» версии MemoryPack прочитать
    // не может (member count в заголовке больше), поэтому такой блоб обязан сбрасываться один.
    public static class ShrunkTestData
    {
        public const ushort Tag = 60002;
        public const int ResetMarker = -1;
    }

    [SaveTag(ShrunkTestData.Tag)]
    [MemoryPackable]
    public partial class ShrunkTestDataWide : SaveBlob
    {
        public int First { get; set; }
        public int Second { get; set; }

        public override void PrepareNewData()
        {
            First = 0;
            Second = 0;
        }
    }

    /// Член удалён, но рубеж чтения не поднят: MemoryPack бросит на старом payload.
    [SaveTag(ShrunkTestData.Tag)]
    [MemoryPackable]
    public partial class ShrunkTestDataNarrow : SaveBlob
    {
        public int First { get; set; }

        public override ushort CurrentVersion => 2;

        public override void PrepareNewData()
        {
            First = ShrunkTestData.ResetMarker;
        }
    }

    /// Тот же член удалён, но рубеж поднят: старый payload не читается вовсе.
    [SaveTag(ShrunkTestData.Tag)]
    [MemoryPackable]
    public partial class ShrunkTestDataGuarded : SaveBlob
    {
        public int First { get; set; }

        public override ushort CurrentVersion => 2;
        public override ushort MinReadableVersion => 2;

        public override void PrepareNewData()
        {
            First = ShrunkTestData.ResetMarker;
        }
    }

    [SaveTag(VersionedTestData.Tag)]
    [MemoryPackable]
    public partial class VersionedTestDataV1 : SaveBlob
    {
        public int Legacy { get; set; }

        public override void PrepareNewData()
        {
            Legacy = 0;
        }
    }

    [SaveTag(VersionedTestData.Tag)]
    [MemoryPackable]
    public partial class VersionedTestDataV2 : SaveBlob
    {
        public int Legacy { get; set; }
        public int Current { get; set; }

        [MemoryPackIgnore]
        public ushort MigratedFrom { get; private set; }

        public override ushort CurrentVersion => 2;

        public override void PrepareNewData()
        {
            Legacy = 0;
            Current = 0;
            MigratedFrom = 0;
        }

        public override void Migrate(ushort fromVersion)
        {
            MigratedFrom = fromVersion;
            Current = Legacy;
            Legacy = 0;
        }
    }
}
