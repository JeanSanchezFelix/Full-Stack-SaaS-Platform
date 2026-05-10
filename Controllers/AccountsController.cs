using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class AccountsController(AppDbContext dbContext) : Controller
{
    private const string LicenseCookieName = "sb_license";
    private const string ReconfirmPrefix = "[RECONFIRM]";

    public async Task<IActionResult> Admin(string tab = "bookings")
    {
        await RejectExpiredPendingBookingsAsync();

        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Customer)
            .OrderByDescending(booking => booking.CreatedAt)
            .Select(booking => new BookingListItemViewModel
            {
                Id = booking.Id,
                CustomerName = booking.Customer == null ? "Cliente" : $"{booking.Customer.FirstName} {booking.Customer.LastName}",
                LicenseNumber = booking.Customer == null ? null : booking.Customer.LicenseNumber,
                RequestedStart = booking.RequestedStart,
                RequestedEnd = booking.RequestedEnd,
                ScooterQuantity = booking.ScooterQuantity,
                EbikeQuantity = booking.EbikeQuantity,
                Status = NormalizeStatus(booking.Status),
                AdminNotes = NormalizeAdminNotes(booking.AdminNotes)
            })
            .ToListAsync();

        var customers = await dbContext.Customers
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreatedAt)
            .Select(customer => new AdminCustomerListItemViewModel
            {
                Id = customer.Id,
                CustomerCode = customer.CustomerCode,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                LicenseNumber = customer.LicenseNumber,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email,
                City = customer.City,
                Country = customer.Country,
                LiabilityWaiverSigned = customer.LiabilityWaiverSigned,
                ElectronicSignature = customer.ElectronicSignature,
                HowDidYouHear = customer.HowDidYouHear,
                Observations = customer.Observations,
                AuthorizeRecontact = customer.AuthorizeRecontact,
                CreatedAt = customer.CreatedAt
            })
            .ToListAsync();

        return View(new AdminPanelViewModel
        {
            Bookings = bookings,
            Customers = customers,
            ActiveTab = string.Equals(tab, "clients", StringComparison.OrdinalIgnoreCase) ? "clients" : "bookings"
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCustomers()
    {
        var customers = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName)
            .ToListAsync();

        var lines = new List<string>
        {
            "Id,CustomerCode,FirstName,LastName,LicenseNumber,PhoneNumber,Email,City,Country,LiabilityWaiverSigned,ElectronicSignature,HowDidYouHear,Observations,AuthorizeRecontact,CreatedAtUtc"
        };

        lines.AddRange(customers.Select(customer =>
            string.Join(",", new[]
            {
                customer.Id.ToString(),
                EscapeCsv(customer.CustomerCode),
                EscapeCsv(customer.FirstName),
                EscapeCsv(customer.LastName),
                EscapeCsv(customer.LicenseNumber),
                EscapeCsv(customer.PhoneNumber),
                EscapeCsv(customer.Email),
                EscapeCsv(customer.City),
                EscapeCsv(customer.Country),
                customer.LiabilityWaiverSigned ? "true" : "false",
                EscapeCsv(customer.ElectronicSignature),
                EscapeCsv(customer.HowDidYouHear),
                EscapeCsv(customer.Observations),
                customer.AuthorizeRecontact ? "true" : "false",
                customer.CreatedAt.ToUniversalTime().ToString("O")
            })));

        var csv = string.Join(Environment.NewLine, lines);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"clientes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    public new async Task<IActionResult> User(string? licenseNumber = null)
    {
        await RejectExpiredPendingBookingsAsync();

        var cookieLicense = Request.Cookies[LicenseCookieName];
        var resolvedLicense = string.IsNullOrWhiteSpace(licenseNumber) ? cookieLicense : licenseNumber;
        object? customerInfo = null;

        if (!string.IsNullOrWhiteSpace(resolvedLicense))
        {
            var normalizedLicense = resolvedLicense.Trim().ToLowerInvariant();
            customerInfo = await dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower() == normalizedLicense)
                .Select(customer => new
                {
                    customerCode = customer.CustomerCode,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    licenseNumber = customer.LicenseNumber,
                    phoneNumber = customer.PhoneNumber,
                    email = customer.Email,
                    city = customer.City,
                    country = customer.Country,
                    liabilityWaiverSigned = customer.LiabilityWaiverSigned,
                    liabilityWaiverSignedAt = customer.LiabilityWaiverSignedAt,
                    createdAt = customer.CreatedAt,
                    latestBookingStatus = customer.Bookings
                        .OrderByDescending(booking => booking.CreatedAt)
                        .Select(booking => NormalizeStatus(booking.Status))
                        .FirstOrDefault(),
                    bookings = customer.Bookings
                        .OrderByDescending(booking => booking.CreatedAt)
                        .Select(booking => new
                        {
                            id = booking.Id,
                            requestedStart = booking.RequestedStart,
                            requestedEnd = booking.RequestedEnd,
                            status = NormalizeStatus(booking.Status),
                            scooterQuantity = booking.ScooterQuantity,
                            ebikeQuantity = booking.EbikeQuantity,
                            estimatedTotal = booking.EstimatedTotal,
                            adminNotes = NormalizeAdminNotes(booking.AdminNotes),
                            reconfirmRequested = booking.AdminNotes != null && booking.AdminNotes.StartsWith(ReconfirmPrefix),
                            canDelete = booking.Status == "pending" || booking.Status == "rejected"
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (customerInfo is not null)
            {
                Response.Cookies.Append(LicenseCookieName, resolvedLicense.Trim(), new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(30)
                });
            }
            else
            {
                Response.Cookies.Delete(LicenseCookieName);
            }
        }

        ViewData["CustomerInfo"] = customerInfo;

        return View(new BookingCreateViewModel
        {
            LicenseNumber = resolvedLicense
        });
    }

    [HttpGet]
    public IActionResult Create(string? licenseNumber = null)
    {
        return View(new UserAuthViewModel
        {
            LicenseNumber = licenseNumber
        });
    }

    [HttpGet]
    public async Task<IActionResult> Lookup(string licenseNumber)
    {
        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            return Json(Array.Empty<object>());
        }

        var query = licenseNumber.Trim().ToLowerInvariant();
        var customers = await dbContext.Customers
            .AsNoTracking()
            .Where(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower().Contains(query))
            .OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName)
            .Take(5)
            .Select(customer => new
            {
                customer.Id,
                customer.CustomerCode,
                customer.FirstName,
                customer.LastName,
                customer.LicenseNumber,
                customer.City,
                customer.Country
            })
            .ToListAsync();

        return Json(customers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Authenticate(UserAuthViewModel model)
    {

        if (model.IsFirstTime)
        {
            return RedirectToAction("Create", "Customers", new
            {
                returnUrl = Url.Action(nameof(Create), "Bookings"),
                licenseNumber = model.LicenseNumber
            });
        }

        if (string.IsNullOrWhiteSpace(model.LicenseNumber))
        {
            ModelState.AddModelError(nameof(model.LicenseNumber), "Entra tu numero de licencia para buscar tu cuenta.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedLicense = model.LicenseNumber!.Trim().ToLowerInvariant();
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower() == normalizedLicense);

        if (customer is null)
        {
            ModelState.AddModelError(nameof(model.LicenseNumber), "No encontramos una cuenta con esa licencia. Si es tu primera visita, registrate primero.");
            return View(model);
        }

        return RedirectToAction("Create", "Bookings", new { licenseNumber = model.LicenseNumber });
    }

    private static string EscapeCsv(string? value)
    {
        var safe = value ?? string.Empty;
        if (safe.Contains(',') || safe.Contains('"') || safe.Contains('\n') || safe.Contains('\r'))
        {
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }

        return safe;
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
    }

    private static string? NormalizeAdminNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return notes;
        }

        return notes.StartsWith(ReconfirmPrefix) ? notes[ReconfirmPrefix.Length..].Trim() : notes;
    }

    private async Task RejectExpiredPendingBookingsAsync()
    {
        var nowUtc = DateTime.UtcNow;
        var expiredBookings = await dbContext.Bookings
            .Where(booking => booking.Status == "pending" && booking.RequestedStart < nowUtc)
            .ToListAsync();

        if (expiredBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in expiredBookings)
        {
            booking.Status = "rejected";
            booking.AdminNotes = string.IsNullOrWhiteSpace(booking.AdminNotes)
                ? "Rechazada automaticamente por horario vencido."
                : booking.AdminNotes;
            booking.UpdatedAt = nowUtc;
        }

        await dbContext.SaveChangesAsync();
    }
}





