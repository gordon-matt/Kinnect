namespace Kinnect.Data.Entities;

public class ChatMessage : BaseEntity<int>
{
    public string Content { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string FromUserId { get; set; } = null!;

    /// <summary>Set for group room messages; null for private messages.</summary>
    public int? ToRoomId { get; set; }

    /// <summary>Set for private messages; null for room messages.</summary>
    public string? ToUserId { get; set; }

    public virtual ApplicationUser FromUser { get; set; } = null!;

    public virtual ChatRoom? ToRoom { get; set; }

    public virtual ApplicationUser? ToUser { get; set; }
}

public class ChatMessageMap : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Content).IsRequired().HasMaxLength(2000);
        builder.Property(m => m.FromUserId).IsRequired();

        builder.HasOne(m => m.FromUser)
            .WithMany()
            .HasForeignKey(m => m.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ToRoom)
            .WithMany(r => r.Messages)
            .HasForeignKey(m => m.ToRoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.ToUser)
            .WithMany()
            .HasForeignKey(m => m.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}