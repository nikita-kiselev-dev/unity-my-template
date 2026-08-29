using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Framework.Foundation.Initialization;
using Framework.Foundation.Logger;
using MemoryPack;
using VContainer;

namespace Framework.Foundation.SaveLoad
{
    [AutoRegistration(Lifetime.Singleton)]
    public sealed class SaveEnvelope : ISaveEnvelope
    {
        [Inject] private readonly IEnumerable<SaveBlob> _injectedBlobs;
        [Inject] private readonly ILogChannelFactory _logChannelFactory;

        private readonly Dictionary<ushort, SaveBlob> _byTag = new();

        private ILogChannel _logger;

        // [Inject] на пустом ctor обязателен: рядом есть internal-шов с параметром, а VContainer
        // без явной пометки выбрал бы конструктор с наибольшим числом параметров (TypeAnalyzer).
        [Inject]
        public SaveEnvelope()
        {
        }

        // Тестовый шов: в проде поля и Init заполняет VContainer.
        internal SaveEnvelope(IEnumerable<SaveBlob> injectedBlobs, ILogChannel logger)
        {
            _injectedBlobs = injectedBlobs;
            _logger = logger;
            IndexByTag();
        }

        // Логгер берётся здесь, а не через [AutoLogger]: у класса уже есть свой [Inject]-метод,
        // а порядок вызова нескольких [Inject]-методов VContainer не определяет.
        [Inject]
        private void Init()
        {
            _logger = _logChannelFactory.Get(nameof(ISaveEnvelope));
            IndexByTag();
        }

        private void IndexByTag()
        {
            foreach (var data in _injectedBlobs)
            {
                var type = data.GetType();
                var attribute = type.GetCustomAttribute<SaveTagAttribute>(inherit: false);

                if (attribute == null)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} is missing [SaveTag]. Every {nameof(SaveBlob)} subclass must declare a stable tag.");
                }

                if (_byTag.TryGetValue(attribute.Tag, out var conflicting))
                {
                    throw new InvalidOperationException(
                        $"Duplicate save tag {attribute.Tag} on {type.FullName} and {conflicting.GetType().FullName}.");
                }

                // Рубеж выше текущей версии означает, что схема не читает даже собственный payload.
                if (data.MinReadableVersion > data.CurrentVersion)
                {
                    throw new InvalidOperationException(
                        $"{type.FullName} declares {nameof(SaveBlob.MinReadableVersion)} {data.MinReadableVersion} " +
                        $"above {nameof(SaveBlob.CurrentVersion)} {data.CurrentVersion}.");
                }

                _byTag.Add(attribute.Tag, data);
            }
        }

        public void PrepareNewData()
        {
            foreach (var data in _byTag.Values)
            {
                data.PrepareNewData();
            }
        }

        public byte[] Serialize()
        {
            var output = new ArrayBufferWriter<byte>();
            var payload = new ArrayBufferWriter<byte>();

            WriteInt32(output, _byTag.Count);

            foreach (var pair in _byTag)
            {
                payload.Clear();
                MemoryPackSerializer.Serialize(pair.Value.GetType(), payload, pair.Value);

                WriteUInt16(output, pair.Key);
                WriteUInt16(output, pair.Value.CurrentVersion);
                WriteInt32(output, payload.WrittenCount);
                output.Write(payload.WrittenSpan);
            }

            return output.WrittenSpan.ToArray();
        }

        public void Deserialize(ReadOnlySpan<byte> bytes)
        {
            PrepareNewData();

            if (bytes.IsEmpty)
            {
                return;
            }

            var loadedSummary = new StringBuilder();

            var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            var offset = sizeof(int);

            for (var index = 0; index < count; index++)
            {
                var tag = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset));
                offset += sizeof(ushort);

                var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset));
                offset += sizeof(ushort);

                var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset));
                offset += sizeof(int);

                var payload = bytes.Slice(offset, payloadLength);
                offset += payloadLength;

                if (!_byTag.TryGetValue(tag, out var instance))
                {
                    _logger.Log($"Unknown save tag {tag}, skipped {payloadLength} bytes.");
                    continue;
                }

                var type = instance.GetType();

                // Сейв из будущей сборки — это даунгрейд всей сборки, а не проблема одной фичи:
                // карантин переименовывает файл и сохраняет его, сброс блоба записал бы поверх.
                if (version > instance.CurrentVersion)
                {
                    throw new InvalidOperationException(
                        $"Save payload for {type.FullName} has version {version}, which is newer than the supported {instance.CurrentVersion}.");
                }

                // Схема сузилась осознанно: payload старее рубежа не читаем и не считаем ошибкой.
                if (version < instance.MinReadableVersion)
                {
                    instance.PrepareNewData();
                    _logger.Log(
                        $"{type.Name} payload version {version} is below the readable minimum " +
                        $"{instance.MinReadableVersion}, data reset.");
                    continue;
                }

                if (!TryLoadBlob(instance, type, version, payload))
                {
                    continue;
                }

                loadedSummary.Append(type.Name).Append(": tag=").Append(tag).Append(", v=").Append(version)
                    .Append(", bytes=").Append(payloadLength).Append('\n');
            }

            _logger.Log($"Loaded save file:\n{loadedSummary}");
        }

        /// <summary>
        /// Блоб читается изолированно: длина payload-а уже известна из конверта, поэтому сбой
        /// одной схемы стоит только её данных, а не всего сейва. Молчать нельзя — это потеря
        /// прогресса фичи, поэтому <c>LogError</c>.
        /// </summary>
        private bool TryLoadBlob(SaveBlob instance, Type type, ushort version, ReadOnlySpan<byte> payload)
        {
            try
            {
                object refValue = instance;
                MemoryPackSerializer.Deserialize(type, payload, ref refValue);

                if (!ReferenceEquals(refValue, instance))
                {
                    throw new InvalidOperationException(
                        $"Save deserialization for {type.FullName} did not reuse the existing instance. " +
                        $"Ensure the type has a parameterless constructor and writable members so MemoryPack can populate it in place.");
                }

                if (version < instance.CurrentVersion)
                {
                    instance.Migrate(version);
                    _logger.Log($"Migrated {type.Name} from version {version} to {instance.CurrentVersion}.");
                }

                return true;
            }
            catch (Exception exception)
            {
                instance.PrepareNewData();
                _logger.LogError($"Failed to load {type.Name} from save, data reset. {exception}");
                return false;
            }
        }

        private static void WriteUInt16(ArrayBufferWriter<byte> writer, ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(writer.GetSpan(sizeof(ushort)), value);
            writer.Advance(sizeof(ushort));
        }

        private static void WriteInt32(ArrayBufferWriter<byte> writer, int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(sizeof(int)), value);
            writer.Advance(sizeof(int));
        }
    }
}
