using System;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Framework.Foundation.Initialization
{
    public class LifecyclePhase
    {
        public string Name { get; }
        public Func<LifecycleEntity, CancellationToken, UniTask> Function { get; }
        public bool RunInParallel { get; }

        public LifecyclePhase(
            string name,
            Func<LifecycleEntity, CancellationToken, UniTask> function,
            bool runInParallel = false)
        {
            Name = name;
            Function = function;
            RunInParallel = runInParallel;
        }
    }
}
