using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.RateLimiting;
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
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    // Emails are stored lower-case; older rows may not be, so compare case-insensitively
    private Task<User?> FindByEmailAsync(string normalizedEmail) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email!.ToLower() == normalizedEmail);

    private async Task<User?> CurrentUserAsync()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? await _context.Users.FindAsync(id) : null;
    }

    [EnableRateLimiting("SignUpPolicy")]
    [HttpPost]
    public async Task<ActionResult<object>> PostUser(User user)
    {
        var email = InputRules.NormalizeEmail(user.Email);
        if (email == null || InputRules.IsGuestEmail(email))
            return BadRequest("A valid email address is required.");

        var problem = InputRules.NameProblem(user.Name) ?? InputRules.PasswordProblem(user.Password);
        if (problem != null)
            return BadRequest(problem);

        if (await FindByEmailAsync(email) != null)
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var newUser = new User
        {
            // Id is assigned by the database; never accept one from the client
            Name = user.Name!.Trim(),
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword(user.Password)
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(newUser);
        var userDto = newUser.ToDto();

        return CreatedAtAction(nameof(GetUser), new { id = newUser.Id }, new
        {
            userDto,
            token
        });
    }

    // Users may only read their own account. Anyone can sign up, so letting any
    // logged-in user read other accounts would expose every user's email.
    // Other ids return 404 (not 403) so ids can't be probed for existence.
    [Authorize]
    [HttpGet("{id}")]
    public async Task<ActionResult<object>> GetUser(int id)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (currentUserId != id.ToString()) return NotFound();

        var user = await _context.Users
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Name, u.Email })
            .FirstOrDefaultAsync();

        if (user == null) return NotFound();
        return Ok(user);
    }

    // There is deliberately no "list all users" endpoint.

    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPut("update")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] User updatedUser)
    {
        var user = await CurrentUserAsync();
        if (user == null)
        {
            return NotFound("User not found.");
        }

        // Require correct password before updating name
        if (string.IsNullOrEmpty(updatedUser.Password))
        {
            return BadRequest("Password is required to update profile.");
        }

        if (!BCrypt.Net.BCrypt.Verify(updatedUser.Password, user.Password))
        {
            return Unauthorized("Incorrect password.");
        }

        if (string.IsNullOrEmpty(updatedUser.Name))
        {
            return BadRequest("Nothing to update.");
        }

        var problem = InputRules.NameProblem(updatedUser.Name);
        if (problem != null)
            return BadRequest(problem);

        user.Name = updatedUser.Name.Trim();
        await _context.SaveChangesAsync();
        return Ok(new { message = "User name updated successfully" });
    }

    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    [HttpPut("changepassword")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        if (user == null)
            return NotFound("User not found");

        if (string.IsNullOrEmpty(request.OldPassword) || !BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
            return Unauthorized("Old password is incorrect");

        var problem = InputRules.PasswordProblem(request.NewPassword);
        if (problem != null)
            return BadRequest(problem);

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully!" });
    }

    // Deletes the logged-in user's own account (identified by the token), after
    // re-checking the password. Saved images are removed by cascade delete.
    [HttpDelete("delete")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
    {
        var user = await CurrentUserAsync();
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (string.IsNullOrEmpty(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            return Unauthorized(new { message = "Invalid credentials" });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted successfully" });
    }

    [EnableRateLimiting("AuthPolicy")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] User loginRequest)
    {
        var email = InputRules.NormalizeEmail(loginRequest.Email);
        var user = email == null ? null : await FindByEmailAsync(email);

        if (user == null || string.IsNullOrEmpty(loginRequest.Password)
            || !BCrypt.Net.BCrypt.Verify(loginRequest.Password, user.Password))
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }

        var token = GenerateJwtToken(user);
        var userDto = user.ToDto();

        return Ok(new
        {
            message = "Login successful!",
            userDto,
            token
        });
    }
}
