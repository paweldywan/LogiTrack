using System.Threading.RateLimiting;

using Asp.Versioning;

using FluentValidation;

using LogiTrack.Data;
using LogiTrack.Data.Repositories;
using LogiTrack.Web.Middleware;
using LogiTrack.Web.Validators;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "LogiTrack")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

var services = builder.Services;

var configuration = builder.Configuration;

// Add exception handling
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddProblemDetails();

// Add controllers with API versioning
services.AddControllers();

// API Versioning
services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

services.AddEndpointsApiExplorer();

// Swagger with JWT support
services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LogiTrack API",
        Version = "v1",
        Description = "Logistics tracking and inventory management API"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = configuration.GetConnectionString("LogiTrack");

services.AddSqlite<LogiTrackContext>(connectionString);

services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<LogiTrackContext>();

services.AddAuthorizationBuilder()
    .AddPolicy("ApiAccess", policy => policy.RequireAuthenticatedUser());

// FluentValidation
services.AddValidatorsFromAssemblyContaining<InventoryItemValidator>();

// Memory cache
services.AddMemoryCache();

// Rate limiting
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    options.AddSlidingWindowLimiter("sliding", limiterOptions =>
    {
        limiterOptions.PermitLimit = 50;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 6;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Too Many Requests",
            status = 429,
            detail = "Rate limit exceeded. Please try again later."
        }, cancellationToken);
    };
});

// CORS
services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"])
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });

    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Health checks
services.AddHealthChecks()
    .AddDbContextCheck<LogiTrackContext>("database");

services.AddScoped<DbSeeder>();

services.AddScoped<IInventoryRepository, InventoryRepository>();

services.AddScoped<IOrderRepository, OrderRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
    };
});

app.UseCorrelationId();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "LogiTrack API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseResponseCompression();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers().RequireRateLimiting("fixed");

app.MapIdentityApi<ApplicationUser>();

app.MapHealthChecks("/health");

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

await app.RunAsync();

// Make Program accessible for integration tests
public partial class Program { }
