using System;
using Utility.Notifications;
using Xunit;

namespace Utility.Tests.Compatibility
{
    public sealed class PublicSurfaceTests
    {
        [Fact]
        public void ToastFacadeDoesNotExposeInjectedBehaviour()
        {
            Assert.Equal(typeof(void), typeof(Toast).GetMethod("Initialize")?.ReturnType);
            Assert.DoesNotContain(
                typeof(Toast).Assembly.GetExportedTypes(),
                type => type.Name == "ToastBehaviour"
            );
        }

        [Fact]
        public void ShowUsesTypedKind()
        {
            Type[] parameters =
            {
                typeof(string),
                typeof(string),
                typeof(ToastKind),
                typeof(float),
            };
            Assert.NotNull(typeof(Toast).GetMethod("Show", parameters));
        }
    }
}
