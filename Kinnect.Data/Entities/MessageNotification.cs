namespace Kinnect.Data.Entities;

public class MessageNotification : BaseEntity<int>
{
    public int ChatMessageId { get; set; }

    public string FromUserId { get; set; } = null!;

    public string ToUserId { get; set; } = null!;

    /// <summary>True once the recipient opens the conversation in the chat UI.</summary>
    public bool IsRead { get; set; }

    /// <summary>True after the hourly email job has sent the unread-messages digest for this notification.</summary>
    public bool EmailSent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual ChatMessage Message { get; set; } = null!;
}

public class MessageNotificationMap : IEntityTypeConfiguration<MessageNotification>
{
    public void Configure(EntityTypeBuilder<MessageNotification> builder)
    {
        builder.ToTable("MessageNotifications", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.FromUserId).IsRequired();
        builder.Property(m => m.ToUserId).IsRequired();

        builder.HasOne(m => m.Message)
            .WithMany()
            .HasForeignKey(m => m.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ToUserId, m.IsRead });
        builder.HasIndex(m => new { m.IsRead, m.EmailSent, m.CreatedAtUtc });
    }
}