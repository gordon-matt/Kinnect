namespace Kinnect.Services.Abstractions;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string category, string fileName);
    void DeleteFile(string relativePath);
    string GetFullPath(string relativePath);
    string GetBaseUploadPath();
}
