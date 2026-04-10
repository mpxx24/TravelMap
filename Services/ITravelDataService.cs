using TravelMap.Models;

namespace TravelMap.Services;

public interface ITravelDataService
{
    Task<TravelData> LoadAsync(string email, CancellationToken ct = default);
    Task SaveAsync(TravelData data, CancellationToken ct = default);
    Task<CountryVisit> UpsertVisitAsync(string email, CountryVisit visit, CancellationToken ct = default);
    Task DeleteVisitAsync(string email, string countryCode, CancellationToken ct = default);
    Task<string> GenerateShareTokenAsync(string email, CancellationToken ct = default);
    Task RevokeShareTokenAsync(string email, CancellationToken ct = default);
    Task<TravelData?> LoadByShareTokenAsync(string token, CancellationToken ct = default);
    Task<string> UploadPhotoAsync(string email, string countryCode, Stream photoStream, string contentType, CancellationToken ct = default);
    Task DeletePhotoAsync(string email, string countryCode, string photoId, CancellationToken ct = default);
    Task<(string ContentType, Stream Data)?> GetPhotoAsync(string email, string countryCode, string photoId, CancellationToken ct = default);
}
