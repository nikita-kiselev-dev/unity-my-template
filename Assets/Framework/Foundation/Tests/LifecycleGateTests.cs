using Framework.Foundation.Configs;
using Framework.Foundation.Initialization;
using Framework.Foundation.Tests.Fakes;
using NUnit.Framework;
using VContainer;

namespace Framework.Foundation.Tests
{
    public class LifecycleGateTests
    {
        [Test]
        public void Apply_SetsEnabledTrue_WhenAllConfigsEnabled()
        {
            var entity = new TwoConfigHostEntity();
            entity.SetConfigs(new TestConfig { IsEnabled = true }, new TestConfig { IsEnabled = true });

            LifecycleGate.Apply(entity);

            Assert.IsTrue(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_SetsEnabledFalse_WhenAnyConfigDisabled()
        {
            var entity = new TwoConfigHostEntity();
            entity.SetConfigs(new TestConfig { IsEnabled = true }, new TestConfig { IsEnabled = false });

            LifecycleGate.Apply(entity);

            Assert.IsFalse(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_LeavesStatusUntouched_WhenEntityHasNoGate()
        {
            var entity = new PlainEntity();
            entity.Status.SetEnabled(true);

            LifecycleGate.Apply(entity);

            Assert.IsTrue(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_SetsEnabledFalse_WhenConditionRejects()
        {
            var entity = new ConditionalEntity { Result = false };

            LifecycleGate.Apply(entity);

            Assert.IsFalse(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_SetsEnabledTrue_WhenConditionAccepts()
        {
            var entity = new ConditionalEntity { Result = true };

            LifecycleGate.Apply(entity);

            Assert.IsTrue(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_AsksCondition_Once()
        {
            var entity = new ConditionalEntity { Result = true };

            LifecycleGate.Apply(entity);

            Assert.AreEqual(1, entity.Calls);
        }

        [Test]
        public void Apply_SkipsCondition_WhenConfigAlreadyDisabled()
        {
            var entity = new ConditionalConfigHostEntity { Result = true };
            entity.SetConfig(new TestConfig { IsEnabled = false });

            LifecycleGate.Apply(entity);

            Assert.IsFalse(entity.Status.IsEnabled);
            Assert.AreEqual(0, entity.Calls);
        }

        [Test]
        public void Apply_SetsEnabledFalse_WhenConfigEnabledButConditionRejects()
        {
            var entity = new ConditionalConfigHostEntity { Result = false };
            entity.SetConfig(new TestConfig { IsEnabled = true });

            LifecycleGate.Apply(entity);

            Assert.IsFalse(entity.Status.IsEnabled);
        }

        [Test]
        public void Apply_ReportsConfigReason_WhenDisabledByConfig()
        {
            var logger = new FakeLogChannel();
            var entity = new TwoConfigHostEntity();
            entity.SetConfigs(new TestConfig { IsEnabled = true }, new TestConfig { IsEnabled = false });

            LifecycleGate.Apply(entity, logger);

            Assert.AreEqual(1, logger.Messages.Count);
            StringAssert.Contains(nameof(TwoConfigHostEntity), logger.Messages[0]);
            StringAssert.Contains($"{nameof(TestConfig)}(IsEnabled=false)", logger.Messages[0]);
        }

        [Test]
        public void Apply_ReportsConditionReason_WhenDisabledByCondition()
        {
            var logger = new FakeLogChannel();
            var entity = new ConditionalEntity { Result = false };

            LifecycleGate.Apply(entity, logger);

            Assert.AreEqual(1, logger.Messages.Count);
            StringAssert.Contains(nameof(ConditionalEntity), logger.Messages[0]);
            StringAssert.Contains($"{nameof(IConditionalEntity)}.{nameof(IConditionalEntity.ShouldRun)}()", logger.Messages[0]);
        }

        [Test]
        public void Apply_LogsNothing_WhenEntityEnabled()
        {
            var logger = new FakeLogChannel();
            var entity = new ConditionalEntity { Result = true };

            LifecycleGate.Apply(entity, logger);

            Assert.IsEmpty(logger.Messages);
        }

        [Test]
        public void Apply_LogsNothing_WhenLogsDisabled()
        {
            var logger = new FakeLogChannel();
            logger.SetLogsStatus(false);
            var entity = new ConditionalEntity { Result = false };

            LifecycleGate.Apply(entity, logger);

            Assert.IsEmpty(logger.Messages);
        }

        [Test]
        public void IsDisabled_ReturnsFalse_WhenEntityHasNoGate()
        {
            Assert.IsFalse(LifecycleGate.IsDisabled(new PlainEntity()));
        }

        [Test]
        public void IsDisabled_ReturnsTrue_WhenConfigDisabled()
        {
            var entity = new TwoConfigHostEntity();
            entity.SetConfigs(new TestConfig { IsEnabled = true }, new TestConfig { IsEnabled = false });
            LifecycleGate.Apply(entity);

            Assert.IsTrue(LifecycleGate.IsDisabled(entity));
        }

        [Test]
        public void IsDisabled_ReturnsTrue_WhenConditionRejects()
        {
            var entity = new ConditionalEntity { Result = false };
            LifecycleGate.Apply(entity);

            Assert.IsTrue(LifecycleGate.IsDisabled(entity));
        }

        [Test]
        public void IsDisabled_ReturnsFalse_WhenConditionAccepts()
        {
            var entity = new ConditionalEntity { Result = true };
            LifecycleGate.Apply(entity);

            Assert.IsFalse(LifecycleGate.IsDisabled(entity));
        }

        [Test]
        public void IsDisabled_FindsConfigField_DeclaredInBaseClass()
        {
            var entity = new InheritedConfigHostEntity();
            LifecycleGate.Apply(entity);

            Assert.IsTrue(LifecycleGate.IsDisabled(entity));
        }

        [Test]
        public void IsDisabled_ReturnsFalse_WhenConfigFieldIsNotInjected()
        {
            Assert.IsFalse(LifecycleGate.IsDisabled(new LocalConfigEntity()));
        }

        private sealed class PlainEntity : LifecycleEntity
        {
        }

        private sealed class LocalConfigEntity : LifecycleEntity
        {
            private readonly TestConfig _config = new();
        }

        private class BaseConfigHostEntity : LifecycleEntity
        {
            [Inject] private readonly TestConfig _config = new() { IsEnabled = false };
        }

        private sealed class InheritedConfigHostEntity : BaseConfigHostEntity
        {
        }

        private sealed class TwoConfigHostEntity : LifecycleEntity
        {
            [Inject] private TestConfig _first;
            [Inject] private TestConfig _second;

            public void SetConfigs(TestConfig first, TestConfig second)
            {
                _first = first;
                _second = second;
            }
        }

        private sealed class ConditionalEntity : LifecycleEntity, IConditionalEntity
        {
            public bool Result { get; set; }
            public int Calls { get; private set; }

            public bool ShouldRun()
            {
                Calls++;
                return Result;
            }
        }

        private sealed class ConditionalConfigHostEntity : LifecycleEntity, IConditionalEntity
        {
            [Inject] private TestConfig _config;

            public bool Result { get; set; }
            public int Calls { get; private set; }

            public void SetConfig(TestConfig config) => _config = config;

            public bool ShouldRun()
            {
                Calls++;
                return Result;
            }
        }

        private sealed class TestConfig : IConfig
        {
            public bool IsEnabled { get; set; }
        }
    }
}
