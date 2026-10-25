using System.ComponentModel.DataAnnotations;

namespace Kinnect.Data.Entities;

/// <summary>The SMTP server Kinnect sends email through, for example to tell people about unread messages.</summary>
public class EmailSettings : AppSettings
{
    public const int MaxHostLength = 255;

    public const int MaxCredentialLength = 500;

    public override string? ConfigurationSection => "Smtp";

    [Required, StringLength(MaxHostLength)]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 25;

    public bool EnableSsl { get; set; }

    [StringLength(MaxCredentialLength)]
    public string? Username { get; set; }

    /// <summary>Stored as entered. The settings page never shows it again.</summary>
    [StringLength(MaxCredentialLength)]
    public string? Password { get; set; }

    [StringLength(MaxHostLength)]
    public string FromAddress { get; set; } = string.Empty;

    [Required, StringLength(MaxHostLength)]
    public string FromName { get; set; } = "Kinnect";

    /// <summary>Email can only be sent once there is somewhere to send from.</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(FromAddress);
}

public class EmailSettingsMap : IEntityTypeConfiguration<EmailSettings>
{
    public void Configure(EntityTypeBuilder<EmailSettings> builder)
    {
        builder.ToTable("EmailSettings", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Host).IsRequired().HasMaxLength(EmailSettings.MaxHostLength);
        builder.Property(m => m.Username).HasMaxLength(EmailSettings.MaxCredentialLength);
        builder.Property(m => m.Password).HasMaxLength(EmailSettings.MaxCredentialLength);
        builder.Property(m => m.FromAddress).IsRequired().HasMaxLength(EmailSettings.MaxHostLength);
        builder.Property(m => m.FromName).IsRequired().HasMaxLength(EmailSettings.MaxHostLength);
        builder.Ignore(m => m.ConfigurationSection);
        builder.Ignore(m => m.IsConfigured);
    }
}
