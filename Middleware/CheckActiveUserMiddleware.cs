using Microsoft.AspNetCore.Identity;
using MyWeb.Models;
using System.Threading.Tasks;

namespace MyWeb.Middleware
{
    public class CheckActiveUserMiddleware
    {
        private readonly RequestDelegate _next;

        public CheckActiveUserMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var user = await userManager.GetUserAsync(context.User);

                if (user != null && !user.IsActive)
                {
                    // User is logged in but account is deactivated
                    await signInManager.SignOutAsync();
                    context.Response.Redirect("/Identity/Account/Login?deactivated=true");
                    return;
                }
            }

            await _next(context);
        }
    }

    // Extension method to add the middleware to the pipeline
    public static class CheckActiveUserMiddlewareExtensions
    {
        public static IApplicationBuilder UseCheckActiveUser(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CheckActiveUserMiddleware>();
        }
    }
}