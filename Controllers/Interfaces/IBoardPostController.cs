using Microsoft.AspNetCore.Mvc;
using MyPostgresApi.Models;

public interface IBoardPostController
{
    Task<ActionResult<object>> PostBoardPost(BoardPost boardpost);
    Task<ActionResult<IEnumerable<object>>> GetBoardPosts();
    Task<IActionResult> UpdateCurrentBoardPost([FromBody] BoardPost updatedBoardPost);
}
