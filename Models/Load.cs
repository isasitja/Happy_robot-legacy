namespace TMS_API.Models
{
    /// <summary>
    /// A load record returned by the Legacy TMS board (LOAD_QUERY / LOAD_GET / LOAD_BOOK).
    /// Fixed-width fields are already trimmed of their space padding.
    /// </summary>
    public class Load
    {
        public string LoadId { get; set; } = string.Empty;

        public string OriginCity { get; set; } = string.Empty;
        public string OriginState { get; set; } = string.Empty;
        public string OriginZip { get; set; } = string.Empty;

        public string DestinationCity { get; set; } = string.Empty;
        public string DestinationState { get; set; } = string.Empty;
        public string DestinationZip { get; set; } = string.Empty;

        /// <summary>Pickup date/time parsed from the PICKUP_DT (yyyyMMddHHmmss) field.</summary>
        public DateTime? PickupDate { get; set; }

        public string EquipmentType { get; set; } = string.Empty;

        /// <summary>Line-haul rate parsed from the zero-padded RATE field.</summary>
        public int? Rate { get; set; }

        /// <summary>Distance parsed from the zero-padded MILES field.</summary>
        public int? Miles { get; set; }

        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Any additional/undocumented fields the server returns, preserved verbatim so the
        /// adapter does not silently drop data the manual does not enumerate.
        /// </summary>
        public IDictionary<string, string> AdditionalFields { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
