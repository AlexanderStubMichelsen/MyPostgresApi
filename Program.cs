using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Collections.Generic;
using System.Linq;
using DotNetEnv;  // Import DotNetEnv
using System.Web;

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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // Show detailed errors
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Database Context
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>().ToTable("items"); // Ensure lowercase table name
    }
}

// Model
public class Item
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// Controller
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
