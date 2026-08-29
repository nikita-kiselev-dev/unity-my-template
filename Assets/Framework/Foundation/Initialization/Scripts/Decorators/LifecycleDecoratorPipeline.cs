using System.Collections.Generic;
using VContainer;

namespace Framework.Foundation.Initialization.Decorators
{
    [AutoRegistration]
    public class LifecycleDecoratorPipeline : ILifecycleDecoratorPipeline
    {
        [Inject] private readonly IReadOnlyList<ILifecycleDecorator> _decorators;

        [Inject]
        public LifecycleDecoratorPipeline()
        {
        }

        internal LifecycleDecoratorPipeline(IReadOnlyList<ILifecycleDecorator> decorators)
        {
            _decorators = decorators;
        }

        public void TryDecorate(LifecycleEntity[] lifecycleEntities)
        {
            foreach (var lifecycleEntity in lifecycleEntities)
            {
                // Singleton-entity с ордерами на нескольких сценах приходит сюда от каждого SceneStarter-а;
                // повторное декорирование дало бы дубли wrapper-ов (двойная загрузка config/view).
                if (lifecycleEntity.Wrappers.Count > 0)
                {
                    continue;
                }

                foreach (var decorator in _decorators)
                {
                    if (!decorator.IsDecoratable(lifecycleEntity))
                    {
                        continue;
                    }
                    
                    var decoratedEntity = decorator.Decorate(lifecycleEntity);
                    lifecycleEntity.AddWrapper(decoratedEntity);
                }
            }
        }
    }
}