using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using DotNetEnv;
using System.Text;
using System.Web;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 🌱 Determine if running in testing mode
var isTesting = builder.Environment.EnvironmentName == "Testing";

// 🌱 Load environment variables
_ = isTesting ? DotNetEnv.Env.Load(".env.test") : DotNetEnv.Env.Load();

// 🔐 Load JWT secret
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT_SECRET_KEY is not set in the .env file.");

// 🔗 Build connection string
string connectionString;
if (isTesting)
{
    connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? throw new InvalidOperationException("TEST_DB_CONNECTION environment variable is not set.");
}
else
{
    var password = HttpUtility.UrlDecode(Env.GetString("DB_PASSWORD"));
    connectionString = $"Host={Env.GetString("DB_HOST")};" +
                       $"Port={Env.GetString("DB_PORT")};" +
                       $"Database={Env.GetString("DB_NAME")};" +
                       $"Username={Env.GetString("DB_USER")};" +
                       $"Password={password}";
}

// 🧠 Database context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ❤️ Health Checks + UI
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", failureStatus: HealthStatus.Degraded);

builder.Services.AddHealthChecksUI(options =>
{
    options.SetEvaluationTimeInSeconds(15);
    options.AddHealthCheckEndpoint("API Health", "/health");
}).AddInMemoryStorage();

// 🌍 CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        _ = policy.WithOrigins(
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

// 📦 Swagger + Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

var app = builder.Build();

// 🛠️ Dev-only config
if (app.Environment.IsDevelopment())
{
    _ = app.UseDeveloperExceptionPage();
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI();
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

// 🩺 Health Checks JSON and UI
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-ui";
    options.ApiPath = "/health-ui-api";
});

app.MapControllers();
app.Run();

// 🧪 Required for integration testing
public partial class Program { }
