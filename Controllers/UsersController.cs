// ✅ UsersController
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
        // Retrieve the secret key from environment variables
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

        // Hash the user's password
        user.Password = BCrypt.Net.BCrypt.HashPassword(user.Password);

        // Add the user to the database
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate a JWT token for the newly created user
        var token = GenerateJwtToken(user);

        var userDto = user.ToDto(); // Convert to DTO if needed

        // Return the user details along with the token
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
    [HttpPut("{email}")]
    public async Task<IActionResult> PutUser(string email, User updatedUser)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Update the Name if provided
        if (!string.IsNullOrEmpty(updatedUser.Name))
        {
            user.Name = updatedUser.Name;
        }

        // Update the Password if provided
        if (!string.IsNullOrEmpty(updatedUser.Password))
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(updatedUser.Password);
        }

        // Exclude email from being updated
        _context.Entry(user).Property(u => u.Email).IsModified = false;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!UserExists(user.Id))
            {
                return NotFound();
            }
            else
            {
                throw;
            }
        }

        return NoContent();
    }

    private bool UserExists(int id)
    {
        return _context.Users.Any(e => e.Id == id);
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

        var userDto = user.ToDto(); // Convert to DTO if needed

        bool passwordMatches = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password);
        if (!passwordMatches)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // You could generate a token here too if needed
        var token = GenerateJwtToken(user); // You’d implement this method

        Console.WriteLine($"login for user email {userDto.Email} user name {userDto.Name}");

        return Ok(new
        {
            message = "Login successful!",
            userDto,
            token
        });

    }

    [Authorize]
    [HttpPut("{email}/changepassword")]
    public async Task<IActionResult> ChangePassword(string email, [FromBody] ChangePasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return NotFound("User not found");

        bool oldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password);
        if (!oldPasswordValid)
            return Unauthorized("Old password is incorrect");

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully!" });
    }
}