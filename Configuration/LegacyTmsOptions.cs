namespace TMS_API.Configuration
{
    /// <summary>
    /// Connection settings for the HappyRobot Legacy TMS (LTMS) line-oriented TCP protocol.
    /// </summary>
    public class LegacyTmsOptions
    {
        public const string SectionName = "LegacyTms";

        /// <summary>TCP host of the LTMS endpoint.</summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>TCP port of the LTMS endpoint.</summary>
        public int Port { get; set; }

        /// <summary>Auth token sent as the AUTH field on every request.</summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>Seconds to wait while establishing the TCP connection.</summary>
        public int ConnectTimeoutSeconds { get; set; } = 10;

        /// <summary>Seconds to wait for the server response (server idle timeout is 30s).</summary>
        public int ReadTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Maximum number of retries for idempotent reads when the server injects a
        /// transient fault (e.g. SERVER_ERROR) or a transport error occurs.
        /// </summary>
        public int MaxRetries { get; set; } = 2;
    }
}
