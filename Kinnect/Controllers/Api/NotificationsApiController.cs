namespace Kinnect.Controllers.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsApiController(
    INotificationService notificationService,
    IUserContextService userContextService) : ControllerBase
{
    /// <summary>Returns unread private-message notifications grouped by sender.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UnreadNotificationDto>>> GetUnread()
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await notificationService.GetUnreadSummaryAsync(userId);
        return Ok(result);
    }

    /// <summary>Marks all notifications from a specific sender as read.</summary>
    [HttpPost("mark-read/{fromUserId}")]
    public async Task<IActionResult> MarkRead(string fromUserId)
    {
        string? userId = userContextService.GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await notificationService.MarkReadAsync(userId, fromUserId);
        return Ok();
    }
}
