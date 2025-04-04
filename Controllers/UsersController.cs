using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using MyPostgresApi.Models;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase, IUsersController
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    private string GenerateJwtToken(User user)
    {
        var secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY");
        if (string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("JWT_SECRET_KEY is not configured.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email ?? string.Empty)
            }),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [HttpPost]
    public async Task<ActionResult<object>> PostUser(User user)
    {
        if (string.IsNullOrEmpty(user.Password))
            return BadRequest("Password is required.");

        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);
        var userDto = user.ToDto();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new
        {
            userDto,
            token
        });
    }

    [Authorize]
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

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync();

        return Ok(users);
    }

    [Authorize]
    [HttpPut("update")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] User updatedUser)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // ✅ Require correct password before updating name
        if (string.IsNullOrEmpty(updatedUser.Password))
        {
            return BadRequest("Password is required to update profile.");
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(updatedUser.Password, user.Password);
        if (!isPasswordValid)
        {
            return Unauthorized("Incorrect password.");
        }

        // ✅ Update name only if provided
        if (!string.IsNullOrEmpty(updatedUser.Name))
        {
            user.Name = updatedUser.Name;
            await _context.SaveChangesAsync();
            return Ok(new { message = "User name updated successfully" });
        }

        return BadRequest("Nothing to update.");
    }

    [Authorize]
    [HttpPut("changepassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound("User not found");

        bool oldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password);
        if (!oldPasswordValid)
            return Unauthorized("Old password is incorrect");

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully!" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] User loginRequest)
    {
        Console.WriteLine($"🔍 Login attempt for: {loginRequest.Email}");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginRequest.Email);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        bool passwordMatches = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password);
        if (!passwordMatches)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var token = GenerateJwtToken(user);
        var userDto = user.ToDto();

        Console.WriteLine($"✅ Login: {userDto.Id} - {userDto.Email}");

        return Ok(new
        {
            message = "Login successful!",
            userDto,
            token
        });
    }

    private bool UserExists(int id)
    {
        return _context.Users.Any(e => e.Id == id);
    }
}

