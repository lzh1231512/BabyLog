using Serilog.Context;

namespace BabyLog.Extensions
{
    public static class LoggingExtensions
    {
        /// <summary>
        /// Enriches the log context with the request information.
        /// </summary>
        public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        {
            return app.Use(async (context, next) =>
            {
                var requestMethod = context.Request.Method;
                var requestPath = context.Request.Path;
                var requestIp = context.Connection.RemoteIpAddress?.ToString();

                using (LogContext.PushProperty("RequestMethod", requestMethod))
                using (LogContext.PushProperty("RequestPath", requestPath))
                using (LogContext.PushProperty("ClientIP", requestIp))
                {
                    await next();

                    var statusCode = context.Response.StatusCode;
                    LogContext.PushProperty("StatusCode", statusCode);
                }
            });
        }
    }
}