using Ardalis.Result;
using Kinnect.Models.Dto.Admin;

namespace Kinnect.Services.Abstractions;

public interface IPersonBackupService
{
    Task ExportToNewFileAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PersonBackupFileDto>>> ListBackupsAsync(CancellationToken cancellationToken = default);

    Task<Result> RestoreFromFileAsync(string fileName, CancellationToken cancellationToken = default);
}
