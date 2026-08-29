using MemoryPack;

namespace Framework.Foundation.SaveLoad
{
    [MemoryPackable(GenerateType.NoGenerate)]
    public abstract partial class SaveBlob
    {
        [MemoryPackIgnore]
        public virtual ushort CurrentVersion => 1;

        /// <summary>
        /// Минимальная версия payload-а, которую эта схема ещё умеет читать. Payload старее
        /// рубежа не десериализуется вовсе: блоб получает <see cref="PrepareNewData"/>, остальной
        /// сейв не страдает. Поднимать вместе с <see cref="CurrentVersion"/>, когда схема сузилась
        /// (удалён или переставлен сериализуемый член) — MemoryPack такой payload прочитать не может.
        /// </summary>
        [MemoryPackIgnore]
        public virtual ushort MinReadableVersion => 1;

        public abstract void PrepareNewData();

        // Вызывается после десериализации payload-а более старой версии: привести поля к текущей схеме.
        public virtual void Migrate(ushort fromVersion)
        {
        }
    }
}
