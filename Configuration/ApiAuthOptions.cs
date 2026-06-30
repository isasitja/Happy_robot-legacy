namespace TMS_API.Configuration
{
    /// <summary>
    /// Credentials required to obtain a token, plus the JWT settings used to sign and validate it.
    /// The username/password are exchanged at the token endpoint for a bearer token that
    /// authorizes the rest of the endpoints.
    /// </summary>
    public class ApiAuthOptions
    {
        public const string SectionName = "ApiAuth";

        /// <summary>Expected username for the token endpoint.</summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>Expected password for the token endpoint.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Symmetric secret used to sign the JWT (HMAC-SHA256). Keep it in user secrets / env vars.</summary>
        public string JwtSigningKey { get; set; } = string.Empty;

        /// <summary>Token issuer (iss claim).</summary>
        public string JwtIssuer { get; set; } = "TMS_API";

        /// <summary>Token audience (aud claim).</summary>
        public string JwtAudience { get; set; } = "TMS_API";

        /// <summary>Lifetime of the issued token, in minutes.</summary>
        public int JwtExpirationMinutes { get; set; } = 60;
    }
}
