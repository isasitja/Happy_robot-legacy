using TMS_API.Models;

namespace TMS_API.Services
{
    /// <summary>
    /// Client for the HappyRobot Legacy TMS line-oriented TCP protocol.
    /// </summary>
    public interface ILegacyTmsClient
    {
        /// <summary>Search the open load board (LOAD_QUERY).</summary>
        Task<IReadOnlyList<Load>> QueryLoadsAsync(LoadQueryParameters parameters, CancellationToken cancellationToken = default);

        /// <summary>Retrieve the full record for a single load (LOAD_GET). Returns null when unknown.</summary>
        Task<Load?> GetLoadAsync(string loadId, CancellationToken cancellationToken = default);

        /// <summary>Commit a booking against a load (LOAD_BOOK).</summary>
        Task<Load> BookLoadAsync(string loadId, LoadBookingRequest request, CancellationToken cancellationToken = default);

        /// <summary>Diagnostic round-trip (DEBUG_ECHO). Bypasses fault injection.</summary>
        Task<string> EchoAsync(string message, CancellationToken cancellationToken = default);
    }
}
