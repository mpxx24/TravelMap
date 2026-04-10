namespace TravelMap.Models;

public enum VisitType
{
    Mainland = 1,
    Islands = 2,
    Both = 3
}

public class CountryVisit
{
    public string CountryCode { get; set; } = string.Empty;   // ISO 3166-1 alpha-3
    public string CountryName { get; set; } = string.Empty;
    public VisitType VisitType { get; set; }
    public DateTime? FirstVisited { get; set; }
    public DateTime? LastVisited { get; set; }
    public string? Notes { get; set; }
    public bool IsWishlist { get; set; }
}

public class TravelData
{
    public string UserEmail { get; set; } = string.Empty;
    public List<CountryVisit> Visits { get; set; } = new();
    public DateTime LastModified { get; set; }
    public string? ShareToken { get; set; }
}
