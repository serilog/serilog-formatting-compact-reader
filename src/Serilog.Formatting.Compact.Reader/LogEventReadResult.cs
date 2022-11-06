using Serilog.Events;

namespace Serilog.Formatting.Compact.Reader
{
    /// <summary>
    /// Represents the result of a 
    /// </summary>
    public readonly struct LogEventReadResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogEventReadResult"/> struct.
        /// </summary>
        /// <param name="success">The result of the read operation.</param>
        /// <param name="logEvent">The log event read or <see langword="null"/>.</param>
        public LogEventReadResult(bool success, LogEvent logEvent)
        {
            Success = success;
            LogEvent = logEvent;
        }

        /// <summary>
        /// <see langword="true"/> if an event could be read; <see langword="false"/> if the end-of-file was encountered.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The log event read.
        /// </summary>
        /// <value>The log event read.</value>
        /// <remarks>Will be <see langword="null"/> if the end-of-file was encountered.</remarks>
        public LogEvent LogEvent { get; }
    }
}
