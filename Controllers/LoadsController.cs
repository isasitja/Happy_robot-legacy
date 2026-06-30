using Microsoft.AspNetCore.Mvc;
using TMS_API.Models;
using TMS_API.Services;

namespace TMS_API.Controllers
{
    /// <summary>
    /// REST facade over the Legacy TMS load board (LOAD_QUERY / LOAD_GET / LOAD_BOOK).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class LoadsController : ControllerBase
    {
        private readonly ILegacyTmsClient _client;
        private readonly ILogger<LoadsController> _logger;

        public LoadsController(ILegacyTmsClient client, ILogger<LoadsController> logger)
        {
            _client = client;
            _logger = logger;
        }

        /// <summary>Search the open load board. At least one filter is required.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Load>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Query([FromQuery] LoadQueryParameters parameters, CancellationToken cancellationToken)
        {
            try
            {
                var loads = await _client.QueryLoadsAsync(parameters, cancellationToken);
                return Ok(loads);
            }
            catch (TmsProtocolException ex)
            {
                return MapError(ex);
            }
        }

        /// <summary>Retrieve the full record for a single load.</summary>
        [HttpGet("{loadId}")]
        [ProducesResponseType(typeof(Load), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string loadId, CancellationToken cancellationToken)
        {
            try
            {
                var load = await _client.GetLoadAsync(loadId, cancellationToken);
                return load is null ? NotFound() : Ok(load);
            }
            catch (TmsProtocolException ex)
            {
                return MapError(ex);
            }
        }

        /// <summary>Commit a booking against a load.</summary>
        [HttpPost("{loadId}/book")]
        [ProducesResponseType(typeof(Load), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Book(string loadId, [FromBody] LoadBookingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var load = await _client.BookLoadAsync(loadId, request ?? new LoadBookingRequest(), cancellationToken);
                return Ok(load);
            }
            catch (TmsProtocolException ex)
            {
                return MapError(ex);
            }
        }

        private IActionResult MapError(TmsProtocolException ex)
        {
            _logger.LogWarning("LTMS returned {Code}: {Message}", ex.Code, ex.Message);

            var status = ex.Code switch
            {
                "AUTH_FAILED" => StatusCodes.Status502BadGateway,
                "UNKNOWN_CMD" => StatusCodes.Status500InternalServerError,
                "MISSING_FIELD" => StatusCodes.Status400BadRequest,
                "UNKNOWN_LOAD" => StatusCodes.Status404NotFound,
                "ALREADY_BOOKED" => StatusCodes.Status409Conflict,
                "INVALID_RATE" => StatusCodes.Status400BadRequest,
                "MALFORMED" => StatusCodes.Status400BadRequest,
                "SERVER_ERROR" => StatusCodes.Status502BadGateway,
                _ => StatusCodes.Status502BadGateway
            };

            return Problem(detail: ex.Message, statusCode: status, title: ex.Code);
        }
    }
}
