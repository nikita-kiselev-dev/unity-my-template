using UnityEngine;

namespace Framework.Features.UI
{
    public interface IRewardRowLayout
    {
        public Transform GetRewardParent(int rewardIndex, int rewardCount);
        public Transform GetLastRewardParent();
    }
}