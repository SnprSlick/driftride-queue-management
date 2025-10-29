using System.Security.Claims;

namespace DriftRide.Api.Middleware
{
    /// <summary>
    /// Custom authorization attribute for role-based access control
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DriftRideAuthorizeAttribute : Attribute
    {
        /// <summary>
        /// Required roles for access
        /// </summary>
        public string[]? Roles { get; set; }

        /// <summary>
        /// Initializes authorization attribute
        /// </summary>
        /// <param name="roles">Required roles for access</param>
        public DriftRideAuthorizeAttribute(params string[] roles)
        {
            Roles = roles;
        }
    }

    /// <summary>
    /// Authorization filter for DriftRide role-based access control
    /// </summary>
    public class DriftRideAuthorizationFilter : Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter
    {
        private readonly string[] _requiredRoles;

        /// <summary>
        /// Initializes authorization filter
        /// </summary>
        /// <param name="requiredRoles">Required roles for access</param>
        public DriftRideAuthorizationFilter(string[] requiredRoles)
        {
            _requiredRoles = requiredRoles;
        }

        /// <summary>
        /// Performs authorization check
        /// </summary>
        /// <param name="context">Authorization filter context</param>
        public void OnAuthorization(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            // Check if user is authenticated
            if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
                return;
            }

            // Check if user has required role
            if (_requiredRoles.Length > 0)
            {
                var userRole = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

                if (string.IsNullOrEmpty(userRole) || !_requiredRoles.Contains(userRole))
                {
                    context.Result = new Microsoft.AspNetCore.Mvc.ForbidResult();
                    return;
                }
            }
        }
    }
}