namespace URLShortener.API.DTOs.Dashboard;

/// <summary>
/// Geographic data for heatmap visualization.
/// </summary>
public class GeographicHeatmapDto
{
    public string? ShortCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public long TotalAccesses { get; set; }
    public int CountriesReached { get; set; }
    public List<CountryDataDto> Countries { get; set; } = new();
    public List<RegionDataDto> TopRegions { get; set; } = new();
    public List<CityDataDto> TopCities { get; set; } = new();
}

/// <summary>
/// Country-level geographic data.
/// </summary>
public class CountryDataDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public double Percentage { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>
/// Region-level geographic data.
/// </summary>
public class RegionDataDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public double Percentage { get; set; }
}

/// <summary>
/// City-level geographic data.
/// </summary>
public class CityDataDto
{
    public string CountryCode { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public long Clicks { get; set; }
    public double Percentage { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
