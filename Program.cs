using App.Metrics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using System.Text;
using System.Web;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 🌱 Load environment
var isTesting = builder.Environment.EnvironmentName == "Testing";
_ = isTesting ? Env.Load(".env.test") : Env.Load();

// 🔐 Load secret
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT_SECRET_KEY is missing.");

// 🔗 Connection string
string connectionString;
if (isTesting)
{
    connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? throw new InvalidOperationException("TEST_DB_CONNECTION is missing.");
}
else
{
    var password = HttpUtility.UrlDecode(Env.GetString("DB_PASSWORD"));
    connectionString = $"Host={Env.GetString("DB_HOST")};Port={Env.GetString("DB_PORT")};" +
                       $"Database={Env.GetString("DB_NAME")};Username={Env.GetString("DB_USER")};Password={password}";
}

// 📊 App.Metrics setup with HTTP request tracking
var metrics = AppMetrics.CreateDefaultBuilder()
    .OutputMetrics.AsPrometheusPlainText()
    .Build();

builder.Host.ConfigureMetrics(metrics);  // Configure metrics for the host

builder.Services.AddMetrics(metrics);  // Register metrics
builder.Services.AddMetricsTrackingMiddleware(); // Middleware to track HTTP metrics
builder.Services.AddMetricsEndpoints();          // Expose /metrics endpoint

// 🧠 Database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ❤️ Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", failureStatus: HealthStatus.Degraded);

builder.Services.AddHealthChecksUI(options =>
{
    options.SetEvaluationTimeInSeconds(15);
    options.AddHealthCheckEndpoint("API Health", "/health");
}).AddInMemoryStorage();

// 🌍 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "http://localhost:5174",
            "http://localhost:5175",
            "http://172.105.95.18",
            "http://172.105.95.18:80",
            "http://172.105.95.18:3000",
            "http://172.105.95.18:5019")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// 🔐 JWT
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

// 🚦 Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("SignUpPolicy", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Too many sign-up attempts. Try again later.", token);
    };
});

// 🔧 Swagger & Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseStaticFiles();
app.UseRouting();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

// 📊 Expose the /metrics endpoint automatically
// app.UseMetricsTrackingMiddleware(); // logs per-request metrics (removed as it's redundant)
// app.UseMetricsServer(); // Removed as metrics are already handled by UseMetricsAllMiddleware
// 📊 Expose Prometheus metrics in plain text format
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/metrics")
    {
        context.Response.ContentType = "text/plain; version=0.0.4";
        await next();
    }
    else
    {
        await next();
    }
});

// 🩺 Health checks
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-ui-api";
});

// 📈 Metrics middleware and endpoint
app.UseMetricsAllMiddleware();  // Adds middleware for tracking requests etc.
app.UseMetricsAllEndpoints();   // Maps actual endpoints like /metrics

app.MapControllers();

app.Run();

public partial class Program { }
