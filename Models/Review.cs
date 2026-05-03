namespace SvelteHybridMVC.Models;

public class Review
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long? RentalId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer? Customer { get; set; }
    public Rental? Rental { get; set; }
}
