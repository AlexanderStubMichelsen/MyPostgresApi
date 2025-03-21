using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations.Schema;
using DotNetEnv;  // Load environment variables
using System.Web;
using BCrypt.Net;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
DotNetEnv.Env.Load();

var isTesting = builder.Environment.EnvironmentName == "Testing";
string connectionString;

if (isTesting)
{
    connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION");
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
            policy.WithOrigins("http://localhost:5173")
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
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");
app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();

// ✅ AppDbContext
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("maskinen");
        modelBuilder.Entity<User>().ToTable("users");
    }
}

// ✅ User model
public class User
{
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("email")]
    public string? Email { get; set; }

    private string? _password;

    [Column("password")]
    public string? Password
    {
        get => _password;
        set => _password = value;
    }
}

// ✅ UsersController
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Name, u.Email })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> PostUser(User user)
    {
        if (string.IsNullOrEmpty(user.Password))
            return BadRequest("Password is required.");

        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { user.Id, user.Name, user.Email });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(int id, User user)
    {
        if (id != user.Id) return BadRequest();

        var existingUser = await _context.Users.FindAsync(id);
        if (existingUser == null) return NotFound();

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;

        if (!string.IsNullOrEmpty(user.Password) &&
            !BCrypt.Net.BCrypt.Verify(user.Password, existingUser.Password))
        {
            existingUser.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] User loginRequest)
    {
        Console.WriteLine($"🔍 Login attempt for: {loginRequest.Email}");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email);
        if (user == null) return Unauthorized("Invalid email or password.");

        bool passwordMatches = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password);
        if (!passwordMatches) return Unauthorized("Invalid email or password.");

        return Ok(new { message = "Login successful!" });
    }
}

// ✅ Required for testing purposes with WebApplicationFactory
public partial class Program { }
