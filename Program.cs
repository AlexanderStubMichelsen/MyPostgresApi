using Microsoft.EntityFrameworkCore;
using DotNetEnv;  // Load environment variables
using System.Web;

var builder = WebApplication.CreateBuilder(args);

// Determine if running in testing mode early
var isTesting = builder.Environment.EnvironmentName == "Testing";

// Load correct .env file
if (isTesting)
{
    _ = DotNetEnv.Env.Load(".env.test");
}
else
{
    _ = DotNetEnv.Env.Load(); // defaults to `.env`
}

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));


// ✅ Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            _ = policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    _ = app.UseDeveloperExceptionPage();
    _ = app.UseSwagger();
    _ = app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();

// ✅ Required for testing purposes with WebApplicationFactory
public partial class Program { }
