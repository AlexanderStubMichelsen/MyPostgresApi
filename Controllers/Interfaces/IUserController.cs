using Microsoft.AspNetCore.Mvc;
using MyPostgresApi.Models;

public interface IUsersController
{
    Task<ActionResult<object>> PostUser(User user);
    Task<ActionResult<object>> GetUser(int id);
    Task<ActionResult<IEnumerable<object>>> GetUsers();
    Task<IActionResult> PutUser(string email, User updatedUser);
    Task<IActionResult> Login(User loginRequest);
    Task<IActionResult> ChangePassword(string email, [FromBody] ChangePasswordRequest request);
}
