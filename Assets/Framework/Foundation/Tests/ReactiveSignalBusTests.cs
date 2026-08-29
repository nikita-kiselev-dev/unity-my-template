using System;
using Framework.Foundation.Signals;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Framework.Foundation.Tests
{
    public class ReactiveSignalBusTests
    {
        private class FooSignal : ISignal
        {
        }

        private class BarSignal : ISignal
        {
        }

        private class ValueSignal : ISignal
        {
            public int Value { get; }

            public ValueSignal(int value)
            {
                Value = value;
            }
        }

        [Test]
        public void Trigger_InvokesSubscribedHandler()
        {
            var bus = new ReactiveSignalBus();
            var count = 0;
            bus.Subscribe<FooSignal>(() => count++);

            bus.Trigger<FooSignal>();

            Assert.AreEqual(1, count);
            bus.Dispose();
        }

        [Test]
        public void Trigger_WithPayload_PassesSignalToHandler()
        {
            var bus = new ReactiveSignalBus();
            var received = 0;
            bus.Subscribe<ValueSignal>(signal => received = signal.Value);

            bus.Trigger(new ValueSignal(99));

            Assert.AreEqual(99, received);
            bus.Dispose();
        }

        [Test]
        public void Trigger_DoesNotInvoke_HandlersOfOtherSignal()
        {
            var bus = new ReactiveSignalBus();
            var fooCount = 0;
            bus.Subscribe<FooSignal>(() => fooCount++);

            bus.Trigger<BarSignal>();

            Assert.AreEqual(0, fooCount);
            bus.Dispose();
        }

        [Test]
        public void DisposingSubscription_StopsDelivery()
        {
            var bus = new ReactiveSignalBus();
            var count = 0;
            var subscription = bus.Subscribe<FooSignal>(() => count++);

            subscription.Dispose();
            bus.Trigger<FooSignal>();

            Assert.AreEqual(0, count);
            bus.Dispose();
        }

        [Test]
        public void HandlerException_IsIsolated_OtherHandlersStillRun()
        {
            var bus = new ReactiveSignalBus();
            var secondRan = false;
            Exception captured = null;

            ObservableSystem.RegisterUnhandledExceptionHandler(ex => captured = ex);
            try
            {
                bus.Subscribe<FooSignal>(() => throw new InvalidOperationException("boom"));
                bus.Subscribe<FooSignal>(() => secondRan = true);

                bus.Trigger<FooSignal>();

                Assert.IsTrue(secondRan);
                Assert.IsInstanceOf<InvalidOperationException>(captured);
                Assert.AreEqual("boom", captured.Message);
            }
            finally
            {
                // Restore Unity R3 default (see UnityProviderInitializer).
                ObservableSystem.RegisterUnhandledExceptionHandler(ex => Debug.LogException(ex));
                bus.Dispose();
            }
        }
    }
}
