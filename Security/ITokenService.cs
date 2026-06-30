namespace TMS_API.Security
{
    /// <summary>
    /// Validates API credentials and issues JWT bearer tokens.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>Returns true when the supplied username/password match the configured credentials.</summary>
        bool ValidateCredentials(string username, string password);

        /// <summary>Issues a signed JWT for the given user and returns it with its UTC expiration.</summary>
        (string Token, DateTime ExpiresAtUtc) CreateToken(string username);
    }
}
