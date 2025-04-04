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

        [Authorize]
        [HttpPost("save")]
        public async Task<IActionResult> SaveImageForUser([FromBody] SavedImageDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var savedImage = new SavedImage
            {
                UserId = userId,
                ImageUrl = dto.ImageUrl,
                Title = dto.Title,
                Photographer = dto.Photographer,
                SourceLink = dto.SourceLink,
                SavedAt = DateTime.UtcNow
            };

            _context.SavedImages.Add(savedImage);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Image saved successfully!" });
        }

        // Optional: Get saved images for the logged-in user
        [Authorize]
        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<SavedImage>>> GetMySavedImages()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

            var images = await _context.SavedImages
                .Where(img => img.UserId == userId)
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

    }
}
