namespace Coflnet.Ane;

/// <summary>
/// Represents a snapshot of price statistics for a product at a specific point in time
/// Used to track price trends and historical data for products
/// </summary>
public class PricePoint
{
    public string ProductSeoId { get; set; } = ""; // References Product.SeoId
    public DateTime Date { get; set; }
    public double AveragePrice { get; set; }
    public double MedianPrice { get; set; }
    public double MinPrice { get; set; }
    public double MaxPrice { get; set; }
    public int SampleCount { get; set; }
}
