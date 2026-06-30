using System.ComponentModel.DataAnnotations;

namespace TMS_API.Models
{
    /// <summary>Credentials submitted to the token endpoint.</summary>
    public class TokenRequest
    {
        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Token issued in exchange for valid credentials.</summary>
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string TokenType { get; set; } = "Bearer";

        public DateTime ExpiresAtUtc { get; set; }
    }
}
