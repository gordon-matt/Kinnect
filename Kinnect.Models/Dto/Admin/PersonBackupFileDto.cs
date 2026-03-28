namespace Kinnect.Models.Dto.Admin;

public sealed class PersonBackupFileDto
{
    public string FileName { get; set; } = null!;

    public long SizeBytes { get; set; }

    public DateTime CreatedUtc { get; set; }
}
