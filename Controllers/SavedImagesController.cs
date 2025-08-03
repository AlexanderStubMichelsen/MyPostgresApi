using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MyPostgresApi.Models;
using MyPostgresApi.DTOs;

namespace MyPostgresApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
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

        [Authorize]
        [HttpPost("save")]
        public async Task<IActionResult> SaveImageForUser([FromBody] SavedImageDto dto)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            Console.WriteLine("🔑 User ID from token: " + userId);

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
                    UserId = userId, // ✅ use authenticated user ID
                    ImageUrl = dto.ImageUrl,
                    Title = dto.Title,
                    Photographer = dto.Photographer,
                    SourceLink = dto.SourceLink,
                    SavedAt = DateTime.UtcNow
                };

                _context.SavedImages.Add(image);
                await _context.SaveChangesAsync();

                return Ok(image);

            }
        }

            [Authorize]
            [HttpGet("mine")]
            public async Task<ActionResult<IEnumerable<SavedImage>>> GetMyImages()
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var images = await _context.SavedImages
                    .Where(i => i.UserId == userId)
                    .ToListAsync();

                return Ok(images);
            }

            [Authorize]
            [HttpDelete("{id}")]
            public async Task<IActionResult> DeleteSavedImage(int id)
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                var image = await _context.SavedImages.FirstOrDefaultAsync(i => i.Id == id && i.UserId == userId);
                if (image == null)
                {
                    return NotFound("Image not found or not owned by user.");
                }

                _context.SavedImages.Remove(image);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Image deleted successfully!" });
            }

        [Authorize]
        [HttpGet("users-with-images-count")]
        public async Task<ActionResult<int>> GetUsersWithImagesCount()        {            var count = await _context.SavedImages                .Select(i => i.UserId)                .Distinct()                .CountAsync();            return Ok(count);        }        [AllowAnonymous]        [HttpGet("image-user-count/{imageUrl}")]        public async Task<ActionResult<int>> GetUsersCountForImage(string imageUrl)
        {
            var decodedUrl = Uri.UnescapeDataString(imageUrl);
            
            var count = await _context.SavedImages
                .Where(i => i.ImageUrl == decodedUrl)
                .Select(i => i.UserId)
                .Distinct()
                .CountAsync();

            return Ok(count);
        }

        }
    }
