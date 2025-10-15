namespace BabyLog
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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
                options.MultipartBodyLengthLimit = int.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
                options.BufferBodyLengthLimit = int.MaxValue;
                options.MemoryBufferThreshold = int.MaxValue;
            });

            builder.Services.AddControllers(options => {
                options.MaxModelBindingCollectionSize = 10000;
            });

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            // Configure Kestrel server options to handle large files
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Set the limits for the server as a whole
                options.Limits.MaxRequestBodySize = long.MaxValue; // Unlimited
                options.Limits.MinRequestBodyDataRate = null; // Remove minimum data rate
                options.Limits.MaxResponseBufferSize = 1024 * 1024 * 100; // 100MB response buffer
                options.Limits.MaxRequestBufferSize = 1024 * 1024 * 100; // 100MB request buffer
                
                // Add longer timeouts for large uploads
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // Use CORS before authorization and endpoint routing
            app.UseCors("AllowAll");

            // 重要：先配置默认文件支持，再配置静态文件支持
            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
