namespace TMS_API.Models
{
    /// <summary>
    /// Filters for a LOAD_QUERY request. At least one filter beyond CMD/AUTH is required;
    /// origin and destination can be narrowed by city, state or ZIP.
    /// </summary>
    public class LoadQueryParameters
    {
        public string? OriginCity { get; set; }
        public string? OriginState { get; set; }
        public string? OriginZip { get; set; }

        public string? DestinationCity { get; set; }
        public string? DestinationState { get; set; }
        public string? DestinationZip { get; set; }

        /// <summary>Equipment type, e.g. DRY_VAN or REEFER (maps to the EQTYPE field).</summary>
        public string? Equipment { get; set; }

        /// <summary>Pickup date filter in the wire format (yyyyMMddHHmmss) or yyyyMMdd.</summary>
        public string? PickupDate { get; set; }

        /// <summary>Caps the number of returned records (MAX_RESULTS). The server applies its own ceiling.</summary>
        public int? MaxResults { get; set; }
    }
}
