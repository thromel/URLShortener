using URLShortener.Core.Interfaces;

namespace URLShortener.API.Services;

/// <summary>
/// Service for exporting analytics data in various formats
/// </summary>
public interface IAnalyticsExportService
{
    /// <summary>
    /// Generates CSV content from analytics summary
    /// </summary>
    /// <param name="summary">Analytics summary data</param>
    /// <returns>CSV content as string</returns>
    string GenerateAnalyticsCsv(AnalyticsSummary summary);

    /// <summary>
    /// Generates Excel content from analytics summary
    /// </summary>
    /// <param name="summary">Analytics summary data</param>
    /// <returns>Excel content as byte array</returns>
    byte[] GenerateAnalyticsExcel(AnalyticsSummary summary);
}

public class AnalyticsExportService : IAnalyticsExportService
{
    private readonly ILogger<AnalyticsExportService> _logger;

    public AnalyticsExportService(ILogger<AnalyticsExportService> logger)
    {
        _logger = logger;
    }

    public string GenerateAnalyticsCsv(AnalyticsSummary summary)
    {
        try
        {
            var csv = new System.Text.StringBuilder();
            
            // Header information
            csv.AppendLine("Analytics Summary Report");
            csv.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            csv.AppendLine();
            
            // Basic metrics
            csv.AppendLine("Basic Metrics");
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Short Code,{summary.ShortCode}");
            csv.AppendLine($"Total Accesses,{summary.TotalAccesses}");
            csv.AppendLine($"Unique Visitors,{summary.UniqueVisitors}");
            csv.AppendLine($"Start Date,{summary.StartDate:yyyy-MM-dd}");
            csv.AppendLine($"End Date,{summary.EndDate:yyyy-MM-dd}");
            csv.AppendLine();
            
            // Country breakdown
            csv.AppendLine("Country Breakdown");
            csv.AppendLine("Country,Access Count,Percentage");
            foreach (var country in summary.CountryBreakdown.OrderByDescending(x => x.Value))
            {
                var percentage = summary.TotalAccesses > 0 ? (country.Value * 100.0 / summary.TotalAccesses).ToString("F1") : "0";
                csv.AppendLine($"{country.Key},{country.Value},{percentage}%");
            }
            csv.AppendLine();
            
            // Device breakdown
            csv.AppendLine("Device Breakdown");
            csv.AppendLine("Device Type,Access Count,Percentage");
            foreach (var device in summary.DeviceBreakdown.OrderByDescending(x => x.Value))
            {
                var percentage = summary.TotalAccesses > 0 ? (device.Value * 100.0 / summary.TotalAccesses).ToString("F1") : "0";
                csv.AppendLine($"{device.Key},{device.Value},{percentage}%");
            }
            csv.AppendLine();
            
            // Time series data
            csv.AppendLine("Time Series Data");
            csv.AppendLine("Time Period,Access Count");
            foreach (var timePeriod in summary.TimeSeriesData.OrderBy(x => x.Key))
            {
                csv.AppendLine($"{timePeriod.Key},{timePeriod.Value}");
            }
            
            _logger.LogDebug("Generated CSV export for {ShortCode}", summary.ShortCode);
            return csv.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate CSV for {ShortCode}", summary.ShortCode);
            throw new InvalidOperationException("Failed to generate CSV export", ex);
        }
    }

    public byte[] GenerateAnalyticsExcel(AnalyticsSummary summary)
    {
        // For now, return CSV as bytes
        // In a real implementation, you would use a library like EPPlus or ClosedXML
        var csv = GenerateAnalyticsCsv(summary);
        return System.Text.Encoding.UTF8.GetBytes(csv);
    }
}