namespace Kinnect.Controllers;

[Authorize]
public class ChatController(IChatService chatService) : Controller
{
    public async Task<IActionResult> Index([FromQuery] string? withUser)
    {
        ViewData["Title"] = "Messages";

        if (!string.IsNullOrEmpty(withUser))
        {
            var target = await chatService.GetPrivateConversationTargetAsync(withUser);
            if (target.IsSuccess && target.Value is not null)
            {
                ViewData["InitialPrivateUserId"] = target.Value.UserId;
                ViewData["InitialPrivateUserName"] = target.Value.DisplayName;
            }
        }

        return View();
    }
}
