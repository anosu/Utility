using System.Collections.Generic;
using Utility.Notifications;
using Utility.Notifications.Internal;
using Xunit;

namespace Utility.Tests.Notifications
{
    public sealed class ToastQueueTests
    {
        [Fact]
        public void CapacityIsEnforced()
        {
            var queue = new ToastCommandQueue();

            for (int i = 0; i < ToastRuntime.Capacity; i++)
                Assert.True(queue.EnqueueShow("title", i.ToString(), ToastKind.Info, 1f));

            Assert.False(queue.EnqueueShow("title", "overflow", ToastKind.Info, 1f));
            Assert.Equal(ToastRuntime.Capacity, queue.Count);

            queue.RemoveExpiredItem();
            Assert.Equal(ToastRuntime.Capacity - 1, queue.Count);
        }

        [Fact]
        public void CommandsRetainTheirOrder()
        {
            var queue = new ToastCommandQueue();
            var operations = new List<ToastCommandKind>();

            queue.EnqueueShow("old", "old", ToastKind.Info, 1f);
            queue.EnqueueClear();
            queue.EnqueueShow("new", "new", ToastKind.Info, 1f);
            queue.RunExclusive(commands =>
            {
                while (commands.Count > 0)
                    operations.Add(commands.Dequeue().Operation);
            });

            Assert.Equal(1, queue.Count);
            Assert.Equal(
                new[] { ToastCommandKind.Show, ToastCommandKind.Clear, ToastCommandKind.Show },
                operations
            );
        }

        [Fact]
        public void ConfigurationRetainsItsPosition()
        {
            var queue = new ToastCommandQueue();
            var operations = new List<ToastCommandKind>();

            queue.EnqueueShow("first", "first", ToastKind.Info, 1f);
            queue.EnqueueConfigure(
                new ToastSettingsPatch(null, null, null, null, 0, null, null, null)
            );
            queue.EnqueueShow("second", "second", ToastKind.Info, 1f);
            queue.RunExclusive(commands =>
            {
                while (commands.Count > 0)
                    operations.Add(commands.Dequeue().Operation);
            });

            Assert.Equal(ToastCommandKind.Configure, operations[1]);
        }

        [Fact]
        public void DurationIsClamped()
        {
            var item = new ToastItem("title", "message", ToastKind.Info, -10f);
            Assert.Equal(1f, item.Duration);
        }

        [Fact]
        public void InvalidKindFallsBackToInfo()
        {
            var queue = new ToastCommandQueue();
            ToastKind kind = default;

            queue.EnqueueShow("title", "message", (ToastKind)999, 1f);
            queue.RunExclusive(commands => kind = commands.Dequeue().Kind);

            Assert.Equal(ToastKind.Info, kind);
        }

        [Fact]
        public void RendererIsNoneBeforeInitialization() =>
            Assert.Equal(ToastRendererKind.None, Toast.Renderer);
    }
}
