namespace Kinnect.Data.Entities;

public class ChatRoom : BaseEntity<int>
{
    public string Name { get; set; } = null!;

    public string AdminUserId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<ChatMessage> Messages { get; set; } = [];
}

public class ChatRoomMap : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.ToTable("ChatRooms", "app");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.AdminUserId).IsRequired();

        builder.HasIndex(m => m.Name).IsUnique();
    }
}