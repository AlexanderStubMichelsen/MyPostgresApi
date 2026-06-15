using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 🌱 Load environment variables for local development only
var isTesting = builder.Environment.EnvironmentName == "Testing";
if (!builder.Environment.IsProduction())
{
    _ = isTesting ? Env.Load(".env.test") : Env.Load();
}

// 🔍 Debug: Check what environment variables are set
Console.WriteLine($"Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"DATABASE_URL env var: {Environment.GetEnvironmentVariable("DATABASE_URL") ?? "Not set"}");
Console.WriteLine($"DefaultConnection from config: {builder.Configuration.GetConnectionString("DefaultConnection") ?? "Not set"}");

// Configure SQLite database path based on environment
string connectionString;
if (builder.Environment.IsProduction())
{
    // Use Azure App Service local storage for SQLite
    var dataPath = Environment.GetEnvironmentVariable("HOME") ?? "/tmp";
    var dbPath = Path.Combine(dataPath, "data", "app.db");
    
    // Ensure directory exists
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
    
    connectionString = $"Data Source={dbPath}";
}
else if (builder.Environment.EnvironmentName == "Testing")
{
    // Use test-specific SQLite database
    connectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_URL") 
                      ?? "Data Source=:memory:";
}
else
{
    // Use local SQLite for development - FORCE SQLite format
    connectionString = "Data Source=app.db";
    
    // Override any PostgreSQL connection strings that might be loaded
    var configConnection = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(configConnection) && !configConnection.StartsWith("Data Source="))
    {
        Console.WriteLine($"Warning: Ignoring non-SQLite connection string: {configConnection}");
        connectionString = "Data Source=app.db";
    }
    else if (!string.IsNullOrEmpty(configConnection))
    {
        connectionString = configConnection;
    }
}

// 🔍 Debug: Show final connection string
Console.WriteLine($"Final connection string: {connectionString}");

// 🔐 Load JWT secret from environment
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException("JWT_SECRET_KEY environment variable is missing.");

// 🔧 Configure Kestrel for Azure App Service
builder.WebHost.ConfigureKestrel(options =>
{
    var port = Environment.GetEnvironmentVariable("PORT") 
              ?? Environment.GetEnvironmentVariable("WEBSITES_PORT") 
              ?? "8080";
    options.ListenAnyIP(int.Parse(port));
});

// 🧠 Database context - Force SQLite usage
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// ❤️ Health Checks
builder.Services.AddHealthChecks()
    .AddSqlite(connectionString, name: "sqlite", failureStatus: HealthStatus.Degraded);

// 🌍 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "https://machinemal.eu/",
            "https://www.machinemal.eu/",
            "https://witty-sand-0aef9a403.2.azurestaticapps.net"
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

// 🧭 Controllers and API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 🧪 Dev tools
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

// 📡 Routing
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
});

// Database initialization
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        Console.WriteLine("Attempting database initialization...");
        if (app.Environment.IsProduction())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }
        Console.WriteLine("Database initialization successful!");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating the database.");
        Console.WriteLine($"Database error: {ex.Message}");
        throw;
    }
}

app.Run();

public partial class Program { }
