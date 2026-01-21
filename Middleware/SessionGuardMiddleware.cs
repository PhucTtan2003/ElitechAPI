using Elitech.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Elitech.Middleware
{
    public class SessionGuardMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            LoginSessionService sessions)
        {
            // Chỉ check khi đã đăng nhập
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                var sid = context.User.Claims
                    .FirstOrDefault(x => x.Type == "sid")
                    ?.Value;

                if (string.IsNullOrWhiteSpace(sid))
                {
                    await ForceLogout(context);
                    return;
                }

                // ❗ Validate KHÔNG TOUCH
                var ok = await sessions.ValidateAsync(sid);

                if (!ok)
                {
                    await ForceLogout(context);
                    return;
                }
            }

            await _next(context);
        }

        private static async Task ForceLogout(HttpContext context)
        {
            await context.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            // tránh loop
            if (!context.Response.HasStarted)
            {
                context.Response.Redirect("/Login?reason=session_expired");
            }
        }
    }
}
