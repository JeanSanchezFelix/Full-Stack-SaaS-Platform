namespace SvelteHybridMVC.Models;

public class AdminCustomerListItemViewModel
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
    public byte[]? ElectronicSignature { get; set; }
    public string? HowDidYouHear { get; set; }
    public bool AuthorizeRecontact { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<AdminCustomerBookingHistoryItemViewModel> Bookings { get; set; } = [];
}

public class AdminCustomerBookingHistoryItemViewModel
{
    public long Id { get; set; }
    public DateTime RequestedStart { get; set; }
    public DateTime? RequestedEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ScooterQuantity { get; set; }
    public int EbikeQuantity { get; set; }
    public decimal? EstimatedTotal { get; set; }
    public string? AdminNotes { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime? ReviewCreatedAt { get; set; }
}
