namespace Kinnect.Models;

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public bool EnableSsl { get; set; } = false;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = null!;

    public string FromName { get; set; } = "Kinnect";
}