using BabyLog.Extensions;
using BabyLog.Middleware;
using BabyLog.Services;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.ResponseCompression;
using Serilog;
using System.IO.Compression;

namespace BabyLog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                // Create the application builder
                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog using appsettings.json
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext());

                // Ensure log directory exists
                var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "log");
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                // Log application startup
                Log.Information("Starting up BabyLog application");

                // Add services to the container.
                
                // Add CORS support
                builder.Services.AddCors(options =>
                {
                    options.AddPolicy("AllowAll", builder =>
                    {
                        builder.AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader();
                    });
                });

                // Add response compression
                builder.Services.AddResponseCompression(options =>
                {
                    options.Providers.Add<GzipCompressionProvider>();
                    options.MimeTypes = new[] {
                        "text/plain",
                        "text/css",
                        "text/html",
                        "application/javascript",
                        "application/json",
                        "application/xml",
                        "image/svg+xml"
                    };
                });
                builder.Services.Configure<GzipCompressionProviderOptions>(options =>
                {
                    options.Level = CompressionLevel.Optimal;
                });

                // Configure IIS options for larger file uploads
                builder.Services.Configure<IISServerOptions>(options =>
                {
                    options.MaxRequestBodySize = int.MaxValue; // Unlimited
                });

                // Configure MVC options for larger file uploads
                builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => 
                {
                    options.MaxModelBindingCollectionSize = int.MaxValue;
                });

                // Configure form options for handling large files
                builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
                {
                    options.ValueLengthLimit = int.MaxValue;
                    options.MultipartBodyLengthLimit = long.MaxValue; // Use long.MaxValue instead of int.MaxValue
                    options.MultipartHeadersLengthLimit = int.MaxValue;
                    options.BufferBodyLengthLimit = long.MaxValue; // Use long.MaxValue instead of int.MaxValue
                    options.MemoryBufferThreshold = int.MaxValue;
                });

                builder.Services.AddControllers(options => {
                    options.MaxModelBindingCollectionSize = 10000;
                });

                // Register video transcoding services
                builder.Services.AddSingleton<VideoTranscodingService>();
                builder.Services.AddSingleton<VideoTranscodingBackgroundService>();

                // Configure Hangfire
                builder.Services.AddHangfire(config =>
                {
                    config.UseMemoryStorage();
                });
                builder.Services.AddHangfireServer();

                // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
                builder.Services.AddOpenApi();

                // Configure Kestrel server options to handle large files
                // This explicit configuration ensures it works in Docker environment
                builder.WebHost.ConfigureKestrel(options =>
                {
                    // Set the limits for the server as a whole
                    options.Limits.MaxRequestBodySize = long.MaxValue; // Unlimited
                    options.Limits.MinRequestBodyDataRate = null; // Remove minimum data rate
                    options.Limits.MaxResponseBufferSize = 1024 * 1024 * 100; // 100MB response buffer
                    options.Limits.MaxRequestBufferSize = 1024 * 1024 * 100; // 100MB request buffer
                    
                    // Add longer timeouts for large uploads
                    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(30);
                    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
                    
                    // Ensure idle timeout is long enough for uploads
                    options.Limits.MaxConcurrentConnections = null;
                    options.Limits.MaxConcurrentUpgradedConnections = null;
                });

                var app = builder.Build();

                // Add request logging enrichment
                app.UseRequestLogging();

                // Use global exception handling middleware
                app.UseGlobalExceptionHandling();

                // Configure HTTP request logging with Serilog
                app.UseSerilogRequestLogging(options =>
                {
                    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                    {
                        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
                    };
                });

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.MapOpenApi();
                    
                    // Enable Hangfire dashboard only in development
                    app.UseHangfireDashboard();
                }
                else
                {
                    // Use response compression in production
                    app.UseResponseCompression();
                }

                // Use CORS before authorization and endpoint routing
                app.UseCors("AllowAll");

                // Configure default files and static files
                app.UseDefaultFiles();
                app.UseStaticFiles(new StaticFileOptions
                {
                    // Configure static files to not buffer the request
                    OnPrepareResponse = ctx =>
                    {
                        // Disable caching for video files to support range requests better
                        var path = ctx.File.PhysicalPath.ToLower();
                        if (path.EndsWith(".mp4") || path.EndsWith(".mov") || path.EndsWith(".m3u8") || path.EndsWith(".ts"))
                        {
                            ctx.Context.Response.Headers.Append("Accept-Ranges", "bytes");
                        }
                    }
                });

                app.UseAuthorization();

                app.MapControllers();
                
                // Initialize Hangfire background jobs
                using (var scope = app.Services.CreateScope())
                {
                    var backgroundJobService = scope.ServiceProvider.GetRequiredService<VideoTranscodingBackgroundService>();
                    backgroundJobService.ScheduleRecurringTranscodingTasks();
                }

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application startup failed");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
