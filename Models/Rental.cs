namespace SvelteHybridMVC.Models;

public class Rental
{
    public long Id { get; set; }
    public long? BookingId { get; set; }
    public long CustomerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Quantity { get; set; }
    public decimal? TotalPrice { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Booking? Booking { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<Review> Reviews { get; set; } = [];
}
