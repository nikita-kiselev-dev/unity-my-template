using Framework.Features.SaveLoad;
using Framework.Foundation.SaveLoad;
using MemoryPack;

namespace Framework.Features.Clicker.Data
{
    [SaveTag(FeaturesSaveTags.ClickerData)]
    [MemoryPackable]
    public partial class ClickerData : Framework.Foundation.SaveLoad.SaveBlob
    {
        public int ClickCount { get; private set; }
        public int Level { get; private set; }

        public override void PrepareNewData()
        {
            ClickCount = 0;
            Level = 0;
        }

        public void OnClick()
        {
            ClickCount++;
        }

        public void Upgrade()
        {
            Level++;
        }
    }
}
