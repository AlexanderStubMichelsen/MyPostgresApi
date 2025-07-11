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
    options.AddHealthCheckEndpoint("API Health", "http://localhost:5019/health");
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

// 🗄️ Ensure specific tables are created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        // Get the schema name from configuration and validate it (only allow letters, numbers, and underscores)
        var schema = scope.ServiceProvider.GetRequiredService<IConfiguration>()["DB_SCHEMA"] ?? "maskinen";
        if (string.IsNullOrWhiteSpace(schema) || !System.Text.RegularExpressions.Regex.IsMatch(schema, @"^[a-zA-Z0-9_]+$"))
        {
            throw new InvalidOperationException("Invalid schema name.");
        }

        // Create only the tables we need (schema is validated above)
        dbContext.Database.ExecuteSqlRaw($@"
            CREATE TABLE IF NOT EXISTS ""{schema}"".""users"" (
                id SERIAL PRIMARY KEY,
                name TEXT,
                email TEXT UNIQUE NOT NULL,
                password TEXT NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS ""{schema}"".""saved_images"" (
                id SERIAL PRIMARY KEY,
                user_id INTEGER NOT NULL,
                image_url TEXT NOT NULL,
                title TEXT,
                photographer TEXT,
                source_link TEXT,
                saved_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW(),
                CONSTRAINT fk_user FOREIGN KEY (user_id)
                    REFERENCES ""{schema}"".""users"" (id)
                    ON DELETE CASCADE
            );
            
            CREATE TABLE IF NOT EXISTS ""{schema}"".""board_posts"" (
                id SERIAL PRIMARY KEY,
                name VARCHAR(255),
                message TEXT,
                created_at TIMESTAMP WITHOUT TIME ZONE DEFAULT NOW()
            );
        ");
        
        Console.WriteLine("✅ Required tables ensured");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Table creation failed: {ex.Message}");
    }
}

app.UseDeveloperExceptionPage();
app.UseSwagger();
app.UseSwaggerUI();

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
