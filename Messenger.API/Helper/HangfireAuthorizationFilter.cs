using Hangfire.Dashboard;
using Messenger.Tools;

namespace Messenger.API.Helper
{
    /// <summary>
    /// فیلتر احراز هویت برای Hangfire Dashboard
    /// فقط کاربران با نقش Manager به Dashboard دسترسی دارند
    /// </summary>
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        /// <summary>
        /// بررسی دسترسی کاربر به Dashboard
        /// </summary>
        public bool Authorize(DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // فقط کاربران احراز هویت شده با نقش Manager
            return httpContext.User.Identity?.IsAuthenticated == true &&
                   httpContext.User.IsInRole(ConstRoles.Manager);
        }
    }
}
