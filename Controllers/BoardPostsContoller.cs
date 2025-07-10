using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyPostgresApi.Models;

namespace MyPostgresApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BoardPostsController : ControllerBase, IBoardPostController
    {
        private readonly AppDbContext _context;

        public BoardPostsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetBoardPosts()
        {
            var posts = await _context.BoardPosts.ToListAsync();
            return Ok(posts.Cast<object>());
        }

        // Get a specific board post by ID
        [HttpGet("{id}")]
        public async Task<ActionResult<BoardPost>> GetBoardPost(int id)
        {
            var post = await _context.BoardPosts.FindAsync(id);
            
            if (post == null)
            {
                return NotFound();
            }

            return Ok(post);
        }

        [HttpPost]
        public async Task<ActionResult<object>> PostBoardPost(BoardPost boardPost)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Set creation timestamp to pure UTC
            boardPost.CreatedAt = DateTime.UtcNow;

            _context.BoardPosts.Add(boardPost);
            await _context.SaveChangesAsync();
            
            return CreatedAtAction(nameof(GetBoardPost), new { id = boardPost.Id }, boardPost);
        }

        // Add route parameter for PUT
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBoardPost(int id, BoardPost boardPost)
        {
            if (id != boardPost.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if the post exists
            var existingPost = await _context.BoardPosts.FindAsync(id);
            if (existingPost == null)
            {
                return NotFound();
            }

            // Update properties
            existingPost.Name = boardPost.Name;
            existingPost.Message = boardPost.Message;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await BoardPostExistsAsync(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // Add DELETE method
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBoardPost(int id)
        {
            var post = await _context.BoardPosts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            _context.BoardPosts.Remove(post);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private async Task<bool> BoardPostExistsAsync(int id)
        {
            return await _context.BoardPosts.AnyAsync(e => e.Id == id);
        }

        // Implementation of IBoardPostController.UpdateCurrentBoardPost
        public async Task<IActionResult> UpdateCurrentBoardPost(BoardPost boardPost)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingPost = await _context.BoardPosts.FindAsync(boardPost.Id);
            if (existingPost == null)
            {
                return NotFound();
            }

            existingPost.Name = boardPost.Name;
            existingPost.Message = boardPost.Message;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await BoardPostExistsAsync(boardPost.Id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }
    }
}
