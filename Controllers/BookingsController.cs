using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class BookingsController : Controller
{
    private readonly AppDbContext _dbContext;
    private const string LicenseCookieName = "sb_license";
    private const string ReconfirmPrefix = "[RECONFIRM]";
    private static readonly Dictionary<int, decimal> DurationPricing = new()
    {
        [1] = 20m,
        [2] = 30m,
        [3] = 35m,
        [6] = 45m
    };

    public BookingsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Create(string? licenseNumber = null)
    {
        var savedLicense = Request.Cookies[LicenseCookieName];
        var resolvedLicense = string.IsNullOrWhiteSpace(licenseNumber) ? savedLicense : licenseNumber;

        if (!string.IsNullOrWhiteSpace(resolvedLicense))
        {
            Response.Cookies.Append(LicenseCookieName, resolvedLicense.Trim(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return RedirectToAction("User", "Accounts", new { licenseNumber = resolvedLicense.Trim() });
        }

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
        var customers = await _dbContext.Customers
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
    public async Task<IActionResult> Create(BookingCreateViewModel model)
    {
        var isReservationRequest = Request.Form.ContainsKey(nameof(model.RequestedStart));
        var isFirstTime = string.Equals(Request.Form["IsFirstTime"], "true", StringComparison.OrdinalIgnoreCase);

        if (!isReservationRequest)
        {
            // This post comes from the lookup screen, so booking field validation does not apply yet.
            ModelState.Remove(nameof(model.RequestedStart));
            ModelState.Remove(nameof(model.RequestedEnd));
            ModelState.Remove(nameof(model.ScooterQuantity));
            ModelState.Remove(nameof(model.EbikeQuantity));
            ModelState.Remove(nameof(model.ElectronicSignature));

            if (isFirstTime)
            {
                return RedirectToAction("Create", "Customers", new
                {
                    returnUrl = Url.Action(nameof(Create), "Bookings"),
                    licenseNumber = model.LicenseNumber
                });
            }
        }

        if (string.IsNullOrWhiteSpace(model.LicenseNumber))
        {
            ModelState.AddModelError(nameof(model.LicenseNumber), "Ingresa tu número de licencia para buscar tu cuenta.");
        }

        if (!ModelState.IsValid)
        {
            return View(new UserAuthViewModel
            {
                LicenseNumber = model.LicenseNumber
            });
        }

        var normalizedLicense = model.LicenseNumber!.Trim().ToLowerInvariant();
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower() == normalizedLicense);

        if (customer is null)
        {
            ModelState.AddModelError(nameof(model.LicenseNumber), "No encontramos una cuenta con esa licencia. Si es tu primera visita, registrate primero.");
            return View(new UserAuthViewModel
            {
                LicenseNumber = model.LicenseNumber
            });
        }

        if (!isReservationRequest)
        {
            Response.Cookies.Append(LicenseCookieName, model.LicenseNumber.Trim(), new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return RedirectToAction("User", "Accounts", new { licenseNumber = model.LicenseNumber });
        }

        var totalQuantity = model.ScooterQuantity + model.EbikeQuantity;
        if (totalQuantity <= 0)
        {
            ModelState.AddModelError(nameof(model.ScooterQuantity), "Debes seleccionar al menos una unidad entre scooters y e-bikes.");
        }

        if (!model.RequestedEnd.HasValue)
        {
            ModelState.AddModelError(nameof(model.RequestedEnd), "Selecciona una duracion valida.");
        }

        var durationHours = model.RequestedEnd.HasValue
            ? (int)Math.Round((model.RequestedEnd.Value - model.RequestedStart).TotalHours)
            : 0;

        if (model.RequestedEnd.HasValue && model.RequestedEnd.Value <= model.RequestedStart)
        {
            ModelState.AddModelError(nameof(model.RequestedEnd), "La hora de fin debe ser despues de la hora de inicio.");
        }

        if (!DurationPricing.ContainsKey(durationHours))
        {
            ModelState.AddModelError(nameof(model.RequestedEnd), "La duracion debe ser 1, 2, 3 o 6 horas.");
        }

        if (!model.LiabilityWaiverSigned)
        {
            ModelState.AddModelError(nameof(model.LiabilityWaiverSigned), "Debes aceptar el relevo de responsabilidad para solicitar la reserva.");
        }

        if (string.IsNullOrWhiteSpace(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electrónica es requerida para solicitar la reserva.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["CustomerInfo"] = new
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
                liabilityWaiverSignedAt = customer.LiabilityWaiverSignedAt
            };

            return View("~/Views/Accounts/User.cshtml", model);
        }

        var estimatedTotal = DurationPricing[durationHours] * totalQuantity;
        var requestedStartUtc = ToUtc(model.RequestedStart);
        DateTime? requestedEndUtc = model.RequestedEnd.HasValue ? ToUtc(model.RequestedEnd.Value) : null;
        var signature = NormalizeOptional(model.ElectronicSignature);

        _dbContext.Bookings.Add(new Booking
        {
            CustomerId = customer.Id,
            RequestedStart = requestedStartUtc,
            RequestedEnd = requestedEndUtc,
            ScooterQuantity = model.ScooterQuantity,
            EbikeQuantity = model.EbikeQuantity,
            EstimatedTotal = estimatedTotal,
            LiabilityWaiverSigned = model.LiabilityWaiverSigned,
            LiabilityWaiverSignedAt = model.LiabilityWaiverSigned ? DateTime.UtcNow : null,
            ElectronicSignature = signature,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        if (model.LiabilityWaiverSigned)
        {
            customer.LiabilityWaiverSigned = true;
            customer.LiabilityWaiverSignedAt = customer.LiabilityWaiverSignedAt ?? DateTime.UtcNow;
            customer.ElectronicSignature = signature;
            customer.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        TempData["BookingCreated"] = "Reserva solicitada. Revisaremos los detalles y la aprobaremos pronto.";
        return RedirectToAction("User", "Accounts", new { licenseNumber = model.LicenseNumber });
    }

    [HttpGet]
    public async Task<IActionResult> Pending()
    {
        var bookings = await _dbContext.Bookings
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
                Status = booking.Status,
                AdminNotes = booking.AdminNotes
            })
            .ToListAsync();

        return View(bookings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(long id)
    {
        var booking = await _dbContext.Bookings
            .Include(existingBooking => existingBooking.Rental)
            .FirstOrDefaultAsync(existingBooking => existingBooking.Id == id);

        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = "approved";
        booking.ApprovedBy = "admin";
        booking.ApprovedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        if (booking.Rental is null)
        {
            _dbContext.Rentals.Add(new Rental
            {
                BookingId = booking.Id,
                CustomerId = booking.CustomerId,
                StartTime = booking.RequestedStart,
                EndTime = booking.RequestedEnd,
                ScooterQuantity = booking.ScooterQuantity,
                EbikeQuantity = booking.EbikeQuantity,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        TempData["BookingApproved"] = "Reserva aprobada y movida a rentals.";
        return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, string? adminNotes)
    {
        if (string.IsNullOrWhiteSpace(adminNotes))
        {
            TempData["BookingRejectedError"] = "La nota administrativa es requerida para rechazar.";
            return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
        }

        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(existingBooking => existingBooking.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = "rejected";
        booking.AdminNotes = adminNotes.Trim();
        booking.ApprovedBy = "admin";
        booking.ApprovedAt = DateTime.UtcNow;
        booking.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        TempData["BookingRejected"] = "Reserva rechazada con nota administrativa.";
        return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reapprove(long id, string? adminNotes)
    {
        if (string.IsNullOrWhiteSpace(adminNotes))
        {
            TempData["BookingRejectedError"] = "La nota administrativa es requerida para re-aprobar.";
            return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
        }

        var booking = await _dbContext.Bookings.FirstOrDefaultAsync(existingBooking => existingBooking.Id == id);
        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = "pending";
        booking.AdminNotes = $"{ReconfirmPrefix} {adminNotes.Trim()}";
        booking.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        TempData["BookingApproved"] = "Reserva marcada para re-confirmacion del cliente.";
        return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteOwn(long id, string? licenseNumber = null)
    {
        var booking = await FindOwnedBookingAsync(id, licenseNumber);
        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != "pending" && booking.Status != "rejected")
        {
            TempData["BookingRejectedError"] = "Esa reserva ya no se puede eliminar.";
            return RedirectToAction("User", "Accounts", new { licenseNumber = ResolveLicense(licenseNumber) });
        }

        booking.Status = "cancelled";
        booking.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        TempData["BookingCreated"] = "Tu reserva fue eliminada.";
        return RedirectToAction("User", "Accounts", new { licenseNumber = ResolveLicense(licenseNumber) });
    }

    [HttpGet]
    public async Task<IActionResult> RespondReconfirm(long id, bool accept, string? licenseNumber = null)
    {
        var booking = await FindOwnedBookingAsync(id, licenseNumber);
        if (booking is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(booking.AdminNotes) || !booking.AdminNotes.StartsWith(ReconfirmPrefix))
        {
            TempData["BookingRejectedError"] = "Esta reserva no tiene una re-confirmacion pendiente.";
            return RedirectToAction("User", "Accounts", new { licenseNumber = ResolveLicense(licenseNumber) });
        }

        if (accept)
        {
            booking.Status = "pending";
            booking.AdminNotes = $"{booking.AdminNotes} | Cliente confirmo disponibilidad.";
            TempData["BookingCreated"] = "Confirmaste que deseas ese horario nuevamente.";
        }
        else
        {
            booking.Status = "cancelled";
            booking.AdminNotes = $"{booking.AdminNotes} | Cliente rechazo re-confirmacion.";
            TempData["BookingCreated"] = "Rechazaste la re-confirmacion y se libero el horario.";
        }

        booking.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return RedirectToAction("User", "Accounts", new { licenseNumber = ResolveLicense(licenseNumber) });
    }

    private static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime()
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? ResolveLicense(string? licenseNumber)
    {
        return string.IsNullOrWhiteSpace(licenseNumber) ? Request.Cookies[LicenseCookieName] : licenseNumber;
    }

    private async Task<Booking?> FindOwnedBookingAsync(long id, string? licenseNumber)
    {
        var resolvedLicense = ResolveLicense(licenseNumber);
        if (string.IsNullOrWhiteSpace(resolvedLicense))
        {
            return null;
        }

        var normalized = resolvedLicense.Trim().ToLowerInvariant();
        return await _dbContext.Bookings
            .Include(booking => booking.Customer)
            .FirstOrDefaultAsync(booking => booking.Id == id
                && booking.Customer != null
                && booking.Customer.LicenseNumber != null
                && booking.Customer.LicenseNumber.ToLower() == normalized);
    }
}
