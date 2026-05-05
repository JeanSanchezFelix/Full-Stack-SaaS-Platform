namespace SvelteHybridMVC.Models;

public class BookingListItemViewModel
{
    public long Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public DateTime RequestedStart { get; set; }
    public DateTime? RequestedEnd { get; set; }
    public int ScooterQuantity { get; set; }
    public int EbikeQuantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
}
