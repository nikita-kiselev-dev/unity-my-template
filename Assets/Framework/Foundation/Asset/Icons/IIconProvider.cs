using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.U2D;

namespace Framework.Foundation.Asset.Icons
{
    public interface IIconProvider
    {
        UniTask<Sprite> GetIcon(string iconName, CancellationToken cancellationToken = default);
        UniTask<Sprite> GetIconFromAtlas(string iconName, string iconTypeName = null, CancellationToken cancellationToken = default);
        UniTask<SpriteAtlas> GetAtlas(string iconTypeName, CancellationToken cancellationToken = default);
    }
}