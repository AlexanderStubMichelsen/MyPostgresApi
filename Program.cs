using App.Metrics;
using App.Metrics.Formatters.Prometheus;
using App.Metrics.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using System.Text;
using System.Web;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// 🌱 Load environment variables
var isTesting = builder.Environment.EnvironmentName == "Testing";
_ = isTesting ? Env.Load(".env.test") : Env.Load();

// 🔐 Load secrets from environment
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT_SECRET_KEY is missing.");

// 🔧 Configure Kestrel for HTTP only — Apache handles HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5019); // Kestrel runs on plain HTTP
});

// 🔗 Build connection string
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

// 📊 App.Metrics setup for Prometheus
var metrics = AppMetrics.CreateDefaultBuilder()
    .OutputMetrics.AsPrometheusPlainText()
    .Build();

builder.Host.UseMetrics(options =>
{
    options.EndpointOptions = endpoints =>
    {
        endpoints.MetricsTextEndpointOutputFormatter = new MetricsPrometheusTextOutputFormatter();
        endpoints.MetricsEndpointOutputFormatter = new MetricsPrometheusTextOutputFormatter();
        endpoints.EnvironmentInfoEndpointEnabled = false;
    };
});

builder.Services.AddMetrics(metrics);
builder.Services.AddMetricsTrackingMiddleware();
builder.Services.AddMetricsEndpoints();

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
            "http://localhost:5173",  // Allow local development
            "https://devdisplay.online",  // Allow production URL
            "https://www.devdisplay.online"  // Allow production with www
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// 🔐 JWT Authentication
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

// 🧭 Swagger & Controllers
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

// 📊 App.Metrics middleware
app.UseMetricsAllMiddleware();
app.UseMetricsAllEndpoints();

// 📡 Routing
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-ui-api";
});

app.Run();

public partial class Program { }
