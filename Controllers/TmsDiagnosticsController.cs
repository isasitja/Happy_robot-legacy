using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMS_API.Services;

namespace TMS_API.Controllers
{
    /// <summary>
    /// Diagnostics for the Legacy TMS connection. DEBUG_ECHO bypasses fault injection,
    /// so it is a safe connectivity/auth probe.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TmsDiagnosticsController : ControllerBase
    {
        private readonly ILegacyTmsClient _client;

        public TmsDiagnosticsController(ILegacyTmsClient client)
        {
            _client = client;
        }

        /// <summary>Round-trip a message through DEBUG_ECHO to verify connectivity and auth.</summary>
        [AllowAnonymous]
        [HttpGet("echo")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Echo([FromQuery] string message = "ping", CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _client.EchoAsync(message, cancellationToken);
                return Ok(new { sent = message, received = response });
            }
            catch (TmsProtocolException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: ex.Code);
            }
        }
    }
}
