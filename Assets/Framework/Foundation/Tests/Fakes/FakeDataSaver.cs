using Framework.Foundation.SaveLoad;

namespace Framework.Foundation.Tests.Fakes
{
    public class FakeDataSaver : IDataSaver
    {
        public int SaveCount { get; private set; }
        public int ImmediateSaveCount { get; private set; }

        public void SaveData() => SaveCount++;
        public void SaveDataImmediate() => ImmediateSaveCount++;
    }
}
