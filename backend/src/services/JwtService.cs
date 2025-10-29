using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DriftRide.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace DriftRide.Services
{
    /// <summary>
    /// Service for JWT token operations including generation, validation, and refresh
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Generates JWT access token for authenticated user
        /// </summary>
        /// <param name="user">User to generate token for</param>
        /// <returns>Login response with tokens</returns>
        LoginResponse GenerateToken(User user);

        /// <summary>
        /// Validates JWT token and extracts user information
        /// </summary>
        /// <param name="token">JWT token to validate</param>
        /// <returns>Token validation result</returns>
        Models.TokenValidationResult ValidateToken(string token);

        /// <summary>
        /// Refreshes access token using refresh token
        /// </summary>
        /// <param name="refreshToken">Refresh token</param>
        /// <param name="user">User for token refresh</param>
        /// <returns>New login response with refreshed tokens</returns>
        LoginResponse? RefreshToken(string refreshToken, User user);

        /// <summary>
        /// Generates refresh token
        /// </summary>
        /// <returns>Refresh token string</returns>
        string GenerateRefreshToken();
    }

    /// <summary>
    /// Implementation of JWT service for token operations
    /// </summary>
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _expirationHours;
        private readonly int _refreshTokenExpirationDays;

        /// <summary>
        /// Initializes JWT service with configuration
        /// </summary>
        /// <param name="configuration">Application configuration</param>
        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
            _secretKey = _configuration["JwtSettings:SecretKey"]
                ?? throw new InvalidOperationException("JWT SecretKey not configured");
            _issuer = _configuration["JwtSettings:Issuer"]
                ?? throw new InvalidOperationException("JWT Issuer not configured");
            _audience = _configuration["JwtSettings:Audience"]
                ?? throw new InvalidOperationException("JWT Audience not configured");
            _expirationHours = _configuration.GetValue<int>("JwtSettings:ExpirationHours", 24);
            _refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7);
        }

        /// <inheritdoc/>
        public LoginResponse GenerateToken(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);
            var expiresAt = DateTime.UtcNow.AddHours(_expirationHours);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.GivenName, user.DisplayName),
                new(ClaimTypes.Role, user.Role.ToString()),
                new("role", user.Role.ToString()), // Additional role claim for compatibility
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiresAt,
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(token);
            var refreshToken = GenerateRefreshToken();

            return new LoginResponse
            {
                AccessToken = tokenString,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt,
                User = new UserInfo
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    Role = user.Role
                }
            };
        }

        /// <inheritdoc/>
        public Models.TokenValidationResult ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero // No tolerance for token expiration
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is not JwtSecurityToken jwtToken ||
                    !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
                {
                    return Models.TokenValidationResult.Failure("Invalid token algorithm");
                }

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = principal.FindFirst(ClaimTypes.Name)?.Value;
                var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) ||
                    string.IsNullOrEmpty(username) ||
                    string.IsNullOrEmpty(roleClaim))
                {
                    return Models.TokenValidationResult.Failure("Missing required claims");
                }

                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Models.TokenValidationResult.Failure("Invalid user ID format");
                }

                if (!Enum.TryParse<UserRole>(roleClaim, out var role))
                {
                    return Models.TokenValidationResult.Failure("Invalid role format");
                }

                var expiresAt = DateTime.FromBinary(jwtToken.ValidTo.ToBinary());

                return Models.TokenValidationResult.Success(userId, username, role, principal, expiresAt);
            }
            catch (SecurityTokenExpiredException)
            {
                return Models.TokenValidationResult.Failure("Token has expired");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                return Models.TokenValidationResult.Failure("Invalid token signature");
            }
            catch (SecurityTokenValidationException ex)
            {
                return Models.TokenValidationResult.Failure($"Token validation failed: {ex.Message}");
            }
            catch (SecurityTokenException ex)
            {
                return Models.TokenValidationResult.Failure($"Token validation error: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return Models.TokenValidationResult.Failure($"Token format error: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public LoginResponse? RefreshToken(string refreshToken, User user)
        {
            // In a production environment, you would typically:
            // 1. Validate the refresh token against stored tokens in database
            // 2. Check if the refresh token is still valid and not expired
            // 3. Verify the refresh token belongs to the user
            // For this implementation, we'll generate a new token for the user

            if (string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            // Validate refresh token format (basic validation)
            if (refreshToken.Length < 32)
            {
                return null;
            }

            // Generate new tokens for the user
            return GenerateToken(user);
        }

        /// <inheritdoc/>
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }
    }
}