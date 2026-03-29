namespace Kinnect.Models.Requests;

public class PersonInviteRequest
{
    public string Email { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}