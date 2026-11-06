using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text;

namespace Account.Filters
// Filters hostnames for different services
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireHostConfigAttribute : Attribute, IAsyncActionFilter
    {
        private readonly string _configKey;

        public RequireHostConfigAttribute(string configKey)
        {
            _configKey = configKey;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var allowedHostsString = configuration.GetValue<string>(_configKey);

            if (!string.IsNullOrEmpty(allowedHostsString))
            {
                var allowedHosts = allowedHostsString.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var currentHost = context.HttpContext.Request.Host.Host;

                if (!allowedHosts.Contains(currentHost, StringComparer.OrdinalIgnoreCase))
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    context.HttpContext.Response.ContentType = "application/xml;charset=UTF-8";

                    string xmlResponse = 
                        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                        "<errors>\n" +
                        "    <error>\n" +
                        "        <code>0008</code>\n" +
                        "        <message>Not Found</message>\n" +
                        "    </error>\n" +
                        "</errors>";

                    byte[] responseBytes = Encoding.UTF8.GetBytes(xmlResponse);
                    context.HttpContext.Response.ContentLength = responseBytes.Length;
                    
                    await context.HttpContext.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
                    return;
                }
            }

            await next();
        }
    }
}
