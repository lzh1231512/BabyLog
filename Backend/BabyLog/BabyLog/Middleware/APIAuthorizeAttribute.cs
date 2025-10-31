using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BabyLog.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class APIAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var hasAuthHeader = context.HttpContext.Request.Headers.TryGetValue("APIKEY", out var token);
            if (!hasAuthHeader || string.IsNullOrEmpty(token))
            {
                context.Result = new JsonResult(new { message = "未授权" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
                return;
            }

            var config = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
            var apiKey = config?["APIKEY"];
            if (string.IsNullOrEmpty(apiKey) || token != apiKey)
            {
                context.Result = new JsonResult(new { message = "未授权" })
                {
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
        }
    }
}