namespace TMS_API.Models
{
    /// <summary>
    /// Payload for a LOAD_BOOK request. The exact field set for booking is implementation-defined
    /// in the manual, so the adapter sends the agreed rate (when provided) plus any extra fields
    /// the caller supplies verbatim.
    /// </summary>
    public class LoadBookingRequest
    {
        /// <summary>Agreed rate to commit. Sent zero-padded as the RATE field when present.</summary>
        public int? Rate { get; set; }

        /// <summary>
        /// Additional booking fields (e.g. CARRIER, MC) forwarded as-is. Keys are upper-cased
        /// to match the protocol convention.
        /// </summary>
        public IDictionary<string, string>? AdditionalFields { get; set; }
    }
}
