using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using MyPostgresApi.Models;
using MyPostgresApi.DTOs;

namespace MyPostgresApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private const string GuestUserCookieName = "guest_user_token";
        private const string GuestUserEmailDomain = InputRules.GuestEmailDomain;
        private const string GuestUserName = "Guest";

        private readonly AppDbContext _context;

        public ImagesController(AppDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpOptions("save")]
        public IActionResult Preflight()
        {
            return NoContent();
        }

        // Anonymous callers get a guest account, so this is rate limited per IP
        // to stop anyone from mass-creating accounts and rows.
        [AllowAnonymous]
        [EnableRateLimiting("ImageSavePolicy")]
        [HttpPost("save")]
        public async Task<IActionResult> SaveImageForUser([FromBody] SavedImageDto dto)
        {
            if (!InputRules.IsHttpUrl(dto.ImageUrl))
                return BadRequest("ImageUrl must be an http(s) URL.");
            if (!string.IsNullOrEmpty(dto.SourceLink) && !InputRules.IsHttpUrl(dto.SourceLink))
                return BadRequest("SourceLink must be an http(s) URL.");
            if ((dto.Title?.Length ?? 0) > InputRules.MaxTextLength || (dto.Photographer?.Length ?? 0) > InputRules.MaxTextLength)
                return BadRequest($"Title and photographer must be at most {InputRules.MaxTextLength} characters.");

            int userId = await GetImageOwnerUserIdAsync(createGuestUser: true)
                ?? throw new InvalidOperationException("Could not resolve image owner.");

            // Check if the user allready has this image saved
            var existingImage = await _context.SavedImages
                .FirstOrDefaultAsync(i => i.UserId == userId && i.ImageUrl == dto.ImageUrl);

            if (existingImage != null)
            {
                return BadRequest("Image already saved for this user.");
            }
            else
            {
                var image = new SavedImage
                {
                    UserId = userId,
                    ImageUrl = dto.ImageUrl,
                    Title = dto.Title ?? "",
                    Photographer = dto.Photographer ?? "",
                    SourceLink = dto.SourceLink ?? "",
                    SavedAt = DateTime.UtcNow
                };

                _context.SavedImages.Add(image);
                await _context.SaveChangesAsync();

                return Ok(image);

            }
        }

        private async Task<int?> GetImageOwnerUserIdAsync(bool createGuestUser)
        {
            var authenticatedUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(authenticatedUserId, out var userId) && userId > 0)
            {
                return userId;
            }

            var guestToken = Request.Cookies[GuestUserCookieName];
            if (!IsValidGuestToken(guestToken))
            {
                if (!createGuestUser)
                {
                    return null;
                }

                guestToken = Guid.NewGuid().ToString("N");
            }

            guestToken ??= Guid.NewGuid().ToString("N");
            var guestUserEmail = GetGuestUserEmail(guestToken);
            var guestUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == guestUserEmail);
            if (guestUser != null)
            {
                AppendGuestUserCookie(guestToken);
                return guestUser.Id;
            }

            if (!createGuestUser)
            {
                return null;
            }

            guestUser = new User
            {
                Name = GuestUserName,
                Email = guestUserEmail,
                // Guests never log in with a password; a low work factor keeps
                // creating them cheap (default BCrypt cost is ~100 ms of CPU each)
                Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), workFactor: 4)
            };

            _context.Users.Add(guestUser);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(guestUser).State = EntityState.Detached;
                guestUser = await _context.Users.FirstAsync(u => u.Email == guestUserEmail);
            }

            AppendGuestUserCookie(guestToken);
            return guestUser.Id;
        }

        private static bool IsValidGuestToken(string? guestToken)
        {
            return Guid.TryParseExact(guestToken, "N", out _);
        }

        private static string GetGuestUserEmail(string guestToken)
        {
            return $"guest-{guestToken}@{GuestUserEmailDomain}";
        }

        private void AppendGuestUserCookie(string guestToken)
        {
            Response.Cookies.Append(GuestUserCookieName, guestToken, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                HttpOnly = true,
                IsEssential = true,
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = Request.IsHttps
            });
        }

        [AllowAnonymous]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<SavedImage>>> GetMyImages()
        {
            var userId = await GetImageOwnerUserIdAsync(createGuestUser: false);
            if (userId == null)
            {
                return Ok(Array.Empty<SavedImage>());
            }

            var images = await _context.SavedImages
                .Where(i => i.UserId == userId)
                .ToListAsync();

            return Ok(images);
        }

        [AllowAnonymous]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSavedImage(int id)
        {
            var userId = await GetImageOwnerUserIdAsync(createGuestUser: false);
            if (userId == null)
            {
                return NotFound("Image not found or not owned by user.");
            }

            var image = await _context.SavedImages.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
            if (image == null)
            {
                return NotFound("Image not found or not owned by user.");
            }

            _context.SavedImages.Remove(image);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Image deleted successfully!" });
        }

        [AllowAnonymous]
        [EnableRateLimiting("ImageSavePolicy")]
        [HttpPost("image-user-count")]
        public async Task<ActionResult<int>> GetUsersCountForImage([FromBody] ImageUrlRequest request)
        {
            var count = await _context.SavedImages
                .Where(i => i.ImageUrl == request.ImageUrl)
                .Select(i => i.UserId)
                .Distinct()
                .CountAsync();

            return Ok(count);
        }

        public class ImageUrlRequest
        {
            public string ImageUrl { get; set; } = string.Empty;
        }
    }
}
