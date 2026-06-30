namespace TMS_API.Services
{
    /// <summary>
    /// Raised when the Legacy TMS returns an ERR line or the adapter detects a protocol/transport
    /// fault. <see cref="Code"/> carries the wire error code (e.g. AUTH_FAILED, UNKNOWN_LOAD).
    /// </summary>
    public class TmsProtocolException : Exception
    {
        public string Code { get; }

        public TmsProtocolException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}
