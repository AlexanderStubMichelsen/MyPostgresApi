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
    public class BoardPostsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BoardPostsController(AppDbContext context)
        {
            _context = context;
        }

        // 🔓 Get all posts (anonymous access)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _context.BoardPosts
                .Include(p => p.User)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(posts.Select(p => p.ToDto()));
        }

        // 🔓 Get a specific post by ID
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var post = await _context.BoardPosts
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
                return NotFound();

            return Ok(post.ToDto());
        }

        // 🔐 Create a post (requires login)
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BoardPostDto dto)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var post = new BoardPost
            {
                Name = dto.Name,
                Message = dto.Message,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            };

            _context.BoardPosts.Add(post);
            await _context.SaveChangesAsync();

            // Fetch user for DTO after save
            post.User = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

            return CreatedAtAction(nameof(GetById), new { id = post.Id }, post.ToDto());
        }

        // 🔐 Get current user's posts
        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMine()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var posts = await _context.BoardPosts
                .Include(p => p.User)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(posts.Select(p => p.ToDto()));
        }

        // 🔐 Update a post (user must own it)
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BoardPostDto dto)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var post = await _context.BoardPosts.Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post == null)
                return NotFound("Post not found or not owned by user.");

            post.Name = dto.Name;
            post.Message = dto.Message;

            await _context.SaveChangesAsync();
            return Ok(post.ToDto());
        }

        // 🔐 Delete a post (user must own it)
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var post = await _context.BoardPosts
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);

            if (post == null)
                return NotFound("Post not found or not owned by user.");

            _context.BoardPosts.Remove(post);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Post deleted successfully!" });
        }
    }
}
