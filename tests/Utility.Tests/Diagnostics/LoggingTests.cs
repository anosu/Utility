using System;
using Utility.Diagnostics;
using Xunit;

namespace Utility.Tests.Diagnostics
{
    public sealed class LoggingTests
    {
        [Fact]
        public void StructuredEventsAreForwarded()
        {
            LogEntry received = default;
            Logging.SetSink(logEvent => received = logEvent);

            try
            {
                Logging.Write(
                    LogLevel.Warning,
                    "Test",
                    "fallback",
                    new InvalidOperationException("failure")
                );

                Assert.Equal(LogLevel.Warning, received.Level);
                Assert.Equal("Test", received.Category);
                Assert.Equal("fallback", received.Message);
                Assert.Equal("failure", received.Exception?.Message);
            }
            finally
            {
                Logging.SetSink(null);
            }
        }

        [Fact]
        public void SinkFailuresDoNotEscape()
        {
            var expected = new InvalidOperationException("sink failure");
            Logging.SetSink(_ => throw expected);

            try
            {
                Exception? escaped = Record.Exception(() =>
                    Logging.Write(LogLevel.Error, "Test", "message")
                );

                Assert.Null(escaped);
                Assert.Same(expected, Logging.LastSinkError);
            }
            finally
            {
                Logging.SetSink(null);
            }
        }

        [Fact]
        public void RecoverableFailuresOnlyForwardOneLineSummary()
        {
            LogEntry received = default;
            Logging.SetSink(logEvent => received = logEvent);

            try
            {
                Logging.WriteRecoverable(
                    LogLevel.Warning,
                    "Test",
                    "Trying fallback.",
                    new MissingMethodException("Method is unavailable.\nNative stack trace")
                );

                Assert.Null(received.Exception);
                Assert.Equal(
                    "Trying fallback. (MissingMethodException: Method is unavailable.)",
                    received.Message
                );
                Assert.DoesNotContain("Native stack trace", received.Message);
            }
            finally
            {
                Logging.SetSink(null);
            }
        }
    }
}
