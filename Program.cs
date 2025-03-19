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

// Load .env variables
Env.Load();

// Build the connection string from .env variables
var password = HttpUtility.UrlDecode(Env.GetString("DB_PASSWORD"));
var connectionString = $"Host={Env.GetString("DB_HOST")};" +
                       $"Port={Env.GetString("DB_PORT")};" +
                       $"Database={Env.GetString("DB_NAME")};" +
                       $"Username={Env.GetString("DB_USER")};" +
                       $"Password={password}";

// Add services to the container
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)); // PostgreSQL

// ✅ Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:5174") // ✅ Allow requests from React frontend
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

// ✅ Apply CORS Policy
app.UseCors("AllowReactApp");

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();


// ✅ Database Context
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items { get; set; }
    public DbSet<User> Users { get; set; } // ✅ Add Users table

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>().ToTable("items"); // Ensure lowercase table name
        modelBuilder.Entity<User>().ToTable("users"); // Ensure lowercase table name
    }
}

// ✅ Models
public class Item
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

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
        set
        {
            {
                _password = value; // ✅ Store already hashed passwords
            }
        }
    }
}

// ✅ Items Controller
[ApiController]
[Route("api/[controller]")]
public class ItemsController : ControllerBase
{
    private readonly AppDbContext _context;
    public ItemsController(AppDbContext context) => _context = context;

    [HttpGet]
    public ActionResult<IEnumerable<Item>> Get() => _context.Items.ToList();

    [HttpGet("{id}")]
    public ActionResult<Item> Get(int id)
    {
        var item = _context.Items.Find(id);
        if (item == null) return NotFound();
        return item;
    }

    [HttpPost]
    public IActionResult Post(Item item)
    {
        _context.Items.Add(item);
        _context.SaveChanges();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }

    [HttpPut("{id}")]
    public IActionResult Put(int id, Item item)
    {
        if (id != item.Id) return BadRequest();
        _context.Entry(item).State = EntityState.Modified;
        _context.SaveChanges();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        var item = _context.Items.Find(id);
        if (item == null) return NotFound();
        _context.Items.Remove(item);
        _context.SaveChanges();
        return NoContent();
    }
}

// ✅ Users Controller
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/users
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new { u.Id, u.Name, u.Email }) // ✅ Hide Password
            .ToListAsync();

        return Ok(users);
    }

    // GET: api/users/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Name, u.Email }) // ✅ Hide Password
            .FirstOrDefaultAsync();

        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    // POST: api/users
[HttpPost]
public async Task<ActionResult<User>> PostUser(User user)
{
    if (string.IsNullOrEmpty(user.Password))
    {
        return BadRequest("Password is required.");
    }

    // ✅ Explicitly hash password before storing
    user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

    _context.Users.Add(user);
    await _context.SaveChangesAsync();

    return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new { user.Id, user.Name, user.Email }); // ✅ Hide password in response
}


    // PUT: api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> PutUser(int id, User user)
    {
        if (id != user.Id)
        {
            return BadRequest();
        }

        var existingUser = await _context.Users.FindAsync(id);
        if (existingUser == null)
        {
            return NotFound();
        }

        existingUser.Name = user.Name;
        existingUser.Email = user.Email;

        // ✅ Only hash the password if it's a new password
        if (!string.IsNullOrEmpty(user.Password))
        {
            bool isSamePassword = BCrypt.Net.BCrypt.Verify(user.Password, existingUser.Password);
            if (!isSamePassword)
            {
                existingUser.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // POST: api/users/login
    [HttpPost("login")]
public async Task<IActionResult> Login([FromBody] User loginRequest)
{
    Console.WriteLine($"🔍 Login attempt for: {loginRequest.Email} password: {loginRequest.Password}");

    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email);

    if (user == null)
    {
        Console.WriteLine("❌ User not found in database.");
        return Unauthorized("Invalid email or password.");
    }

    Console.WriteLine($"✅ Found user: {user.Email}");
    Console.WriteLine($"🔐 Stored hashed password: {user.Password}");
    Console.WriteLine($"🔑 Entered password (plaintext): {loginRequest.Password}");

    // 🔹 Debug: Check if entered password is already hashed
    if (loginRequest.Password != null && loginRequest.Password.StartsWith("$2a$"))
    {
        Console.WriteLine("❌ ERROR: Entered password is already hashed! Expected plain text.");
    }

    bool passwordMatches = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password);

    if (!passwordMatches)
    {
        Console.WriteLine("❌ Password does not match.");
        return Unauthorized("Invalid email or password.");
    }

    Console.WriteLine("✅ Password matched! Login successful.");
    return Ok(new { message = "Login successful!" });
}

}
