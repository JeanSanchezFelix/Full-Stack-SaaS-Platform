namespace SvelteHybridMVC.Models;

// DTO for the admin panel view, which aggregates data from multiple sources to provide a comprehensive view of the system for administrators.
// Contains all the data needed to render the admin panel, including lists of bookings and customers, as well as the active tab and admin user name.
public class AdminPanelViewModel
{
    public IReadOnlyList<BookingListItemViewModel> Bookings { get; set; } = [];
    public IReadOnlyList<AdminCustomerListItemViewModel> Customers { get; set; } = [];
    public string ActiveTab { get; set; } = "bookings";
    public string AdminUserName { get; set; } = "admin";
}

