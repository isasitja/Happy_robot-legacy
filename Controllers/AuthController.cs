using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TMS_API.Models;
using TMS_API.Security;

namespace TMS_API.Controllers
{
    /// <summary>
    /// Issues JWT bearer tokens in exchange for valid credentials. This endpoint is anonymous;
    /// the returned token authorizes the rest of the API.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ITokenService tokenService, ILogger<AuthController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>Exchange username/password for a bearer token.</summary>
        [AllowAnonymous]
        [HttpPost("token")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Token([FromBody] TokenRequest request)
        {
            if (!_tokenService.ValidateCredentials(request.Username, request.Password))
            {
                _logger.LogWarning("Failed token request for user '{User}'.", request.Username);
                return Unauthorized();
            }

            var (token, expiresAt) = _tokenService.CreateToken(request.Username);

            return Ok(new TokenResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresAtUtc = expiresAt
            });
        }
    }
}
