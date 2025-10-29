using Microsoft.AspNetCore.Builder;

namespace DriftRide.Api.Middleware
{
    /// <summary>
    /// Extension methods for JWT middleware registration
    /// </summary>
    public static class JwtMiddlewareExtensions
    {
        /// <summary>
        /// Adds JWT middleware to the application pipeline
        /// </summary>
        /// <param name="builder">Application builder</param>
        /// <returns>Application builder for chaining</returns>
        public static IApplicationBuilder UseJwtMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtMiddleware>();
        }
    }
}