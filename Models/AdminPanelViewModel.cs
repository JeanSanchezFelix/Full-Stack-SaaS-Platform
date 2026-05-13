namespace SvelteHybridMVC.Models;

public class AdminPanelViewModel
{
    public IReadOnlyList<BookingListItemViewModel> Bookings { get; set; } = [];
    public IReadOnlyList<AdminCustomerListItemViewModel> Customers { get; set; } = [];
    public string ActiveTab { get; set; } = "bookings";
    public string AdminUserName { get; set; } = "admin";
}

