using System;
using System.Threading;

namespace Utility.Diagnostics
{
    /// <summary>Specifies the severity of a Utility diagnostic event.</summary>
    public enum LogLevel
    {
        /// <summary>Detailed information about optional paths and recoverable failures.</summary>
        Debug,

        /// <summary>A normal lifecycle or backend-selection event.</summary>
        Information,

        /// <summary>A recoverable failure that caused Utility to use a fallback.</summary>
        Warning,

        /// <summary>A failure that prevented an operation or renderer from continuing.</summary>
        Error,
    }

    /// <summary>Represents one structured diagnostic event emitted by Utility.</summary>
    public readonly struct LogEntry
    {
        internal LogEntry(LogLevel level, string category, string message, Exception? exception)
        {
            Level = level;
            Category = category;
            Message = message;
            Exception = exception;
        }

        /// <summary>Gets the event severity.</summary>
        public LogLevel Level { get; }

        /// <summary>Gets the subsystem that emitted the event.</summary>
        public string Category { get; }

        /// <summary>Gets the human-readable event message.</summary>
        public string Message { get; }

        /// <summary>Gets the associated exception, when one is available.</summary>
        public Exception? Exception { get; }
    }

    /// <summary>Receives one structured Utility diagnostic event.</summary>
    /// <param name="logEvent">The event to forward to the host loader.</param>
    public delegate void LogSink(LogEntry logEvent);

    /// <summary>Provides loader-neutral diagnostic forwarding for Utility.</summary>
    public static class Logging
    {
        private const int MaximumSummaryLength = 240;

        private static SinkRegistration? _registration;

        /// <summary>Gets a value indicating whether a host log sink is configured.</summary>
        public static bool IsConfigured => Volatile.Read(ref _registration) != null;

        /// <summary>Gets the last exception thrown by the configured sink.</summary>
        /// <remarks>Sink exceptions are retained for diagnostics and never escape into Utility.</remarks>
        public static Exception? LastSinkError
        {
            get
            {
                SinkRegistration? registration = Volatile.Read(ref _registration);
                return registration == null ? null : Volatile.Read(ref registration.LastError);
            }
        }

        /// <summary>Atomically installs, replaces, or removes the host log sink.</summary>
        /// <param name="sink">The sink to install, or <see langword="null"/> to disable forwarding.</param>
        public static void SetSink(LogSink? sink)
        {
            Interlocked.Exchange(
                ref _registration,
                sink == null ? null : new SinkRegistration(sink)
            );
        }

        internal static void Write(
            LogLevel level,
            string category,
            string message,
            Exception? exception = null
        )
        {
            SinkRegistration? registration = Volatile.Read(ref _registration);
            if (registration == null)
                return;

            try
            {
                registration.Sink(new LogEntry(level, category, message, exception));
            }
            catch (Exception sinkError)
            {
                Interlocked.Exchange(ref registration.LastError, sinkError);
            }
        }

        internal static void WriteRecoverable(
            LogLevel level,
            string category,
            string message,
            Exception exception
        ) => Write(level, category, $"{message} ({Summarize(exception)})");

        private static string Summarize(Exception exception)
        {
            string message = exception.Message;
            int lineBreak = message.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
                message = message.Substring(0, lineBreak);

            message = message.Trim();
            if (message.Length > MaximumSummaryLength)
                message = message.Substring(0, MaximumSummaryLength - 3) + "...";

            return message.Length == 0
                ? exception.GetType().Name
                : $"{exception.GetType().Name}: {message}";
        }

        private sealed class SinkRegistration
        {
            internal SinkRegistration(LogSink sink)
            {
                Sink = sink;
            }

            internal readonly LogSink Sink;
            internal Exception? LastError;
        }
    }
}
