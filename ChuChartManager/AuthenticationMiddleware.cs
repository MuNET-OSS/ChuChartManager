namespace ChuChartManager;

public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public AuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        if (!context.User.Identity?.IsAuthenticated == true && context.Connection.LocalPort == 5001)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.Append("WWW-Authenticate", "Basic");
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await _next(context);
    }
}
