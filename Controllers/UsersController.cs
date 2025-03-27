// ✅ UsersController
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }
        private static string GenerateJwtToken(User user)
        {
            // Example implementation for generating a JWT token
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key = System.Text.Encoding.ASCII.GetBytes("YourSecretKeyHere"); // Replace with your secret key
            var tokenDescriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Subject = new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("id", user.Id.ToString()),
                    new System.Security.Claims.Claim("email", user.Email ?? string.Empty)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                    new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                    Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetUsers()
    {
        var users = await _context.Users
            .Select(u => new { u.Id, u.Name, u.Email })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{email}")]
    public async Task<IActionResult> PutUser(string email, User updatedUser)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound("User not found.");
        }

        user.Name = updatedUser.Name;
        user.Email = updatedUser.Email;

        if (!string.IsNullOrEmpty(updatedUser.Password))
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(updatedUser.Password);
        }

        _context.Entry(user).State = EntityState.Modified;

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

        bool passwordMatches = BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password);
        if (!passwordMatches)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        // You could generate a token here too if needed
        // var token = GenerateJwtToken(user); // You’d implement this method
        return Ok(new
        {
            message = "Login successful!",
            name = user.Name,
            email = user.Email,
            // You can return more info like role, id, etc.
            // token

        });

    }

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
