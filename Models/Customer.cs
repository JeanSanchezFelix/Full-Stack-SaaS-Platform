namespace SvelteHybridMVC.Models;

public class Customer
{
    public long Id { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public bool LiabilityWaiverSigned { get; set; }
    public DateTime? LiabilityWaiverSignedAt { get; set; }
    public string? ElectronicSignature { get; set; }
    public bool AuthorizeRecontact { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = [];
    public ICollection<Rental> Rentals { get; set; } = [];
    public ICollection<Review> Reviews { get; set; } = [];
}
