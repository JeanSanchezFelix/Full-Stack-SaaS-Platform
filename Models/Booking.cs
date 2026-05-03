namespace SvelteHybridMVC.Models;

public class Booking
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public DateTime RequestedStart { get; set; }
    public DateTime? RequestedEnd { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminNotes { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer? Customer { get; set; }
    public Rental? Rental { get; set; }
}
