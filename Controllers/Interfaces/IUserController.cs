using Microsoft.AspNetCore.Mvc;
using MyPostgresApi.Models;

public interface IUsersController
{
    Task<ActionResult<object>> PostUser(User user);
    Task<ActionResult<object>> GetUser(int id);
    Task<ActionResult<IEnumerable<object>>> GetUsers();
    Task<IActionResult> UpdateCurrentUser([FromBody] User updatedUser);
    Task<IActionResult> Login(User loginRequest);
    Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request);
}
