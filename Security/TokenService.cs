using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TMS_API.Configuration;

namespace TMS_API.Security
{
    /// <summary>
    /// Validates the configured credentials and issues signed JWT bearer tokens.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly ApiAuthOptions _options;

        public TokenService(IOptions<ApiAuthOptions> options)
        {
            _options = options.Value;
        }

        public bool ValidateCredentials(string username, string password)
        {
            if (username is null || password is null)
            {
                return false;
            }

            var userOk = FixedTimeEquals(username, _options.Username);
            var passOk = FixedTimeEquals(password, _options.Password);
            return userOk && passOk;
        }

        public (string Token, DateTime ExpiresAtUtc) CreateToken(string username)
        {
            var expiresAt = DateTime.UtcNow.AddMinutes(_options.JwtExpirationMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, username)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.JwtIssuer,
                audience: _options.JwtAudience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }

        private static bool FixedTimeEquals(string actual, string expected)
        {
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
    }
}
