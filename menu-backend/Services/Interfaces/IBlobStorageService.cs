namespace menu_backend.Services.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string folder);
}
