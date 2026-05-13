using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class BookingsController : Controller
{
    private readonly AppDbContext _dbContext;
    private const string CustomerCodeCookieName = "sb_customer_code";
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
        if (!string.IsNullOrWhiteSpace(licenseNumber))
        {
            return RedirectToAction("User", "Accounts", new { licenseNumber = licenseNumber.Trim() });
        }

        var savedCustomerCode = Request.Cookies[CustomerCodeCookieName];
        if (!string.IsNullOrWhiteSpace(savedCustomerCode))
        {
            return RedirectToAction("User", "Accounts");
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
            Response.Cookies.Append(CustomerCodeCookieName, customer.CustomerCode, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return RedirectToAction("User", "Accounts");
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
        var signatureBytes = DecodeSignaturePngOrNull(signature);

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
            if (signatureBytes is { Length: > 0 })
            {
                customer.ElectronicSignature = signatureBytes;
            }
            customer.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync();

        TempData["BookingCreated"] = "Reserva solicitada. ¡Que disfrute de su aventura sobre ruedas!";
        return RedirectToAction("User", "Accounts");
    }

    [HttpGet]
    [Authorize]
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
                EstimatedTotal = booking.EstimatedTotal,
                Status = booking.Status,
                AdminNotes = booking.AdminNotes
            })
            .ToListAsync();

        return View(bookings);
    }

    [HttpPost]
    [Authorize]
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
    [Authorize]
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
    [Authorize]
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
        TempData["BookingApproved"] = "Reserva marcada para re-confirmación del cliente.";
        return RedirectToAction("Admin", "Accounts", new { tab = "bookings" });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteOwn(long id, string? customerCode = null)
    {
        var booking = await FindOwnedBookingAsync(id, customerCode);
        if (booking is null)
        {
            return NotFound();
        }

        if (booking.Status != "pending" && booking.Status != "rejected")
        {
            TempData["BookingRejectedError"] = "Esa reserva ya no se puede eliminar.";
            return RedirectToAction("User", "Accounts");
        }

        booking.Status = "cancelled";
        booking.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        TempData["BookingCreated"] = "Tu reserva fue eliminada.";
        return RedirectToAction("User", "Accounts");
    }

    [HttpGet]
    public async Task<IActionResult> RespondReconfirm(long id, bool accept, string? customerCode = null)
    {
        var booking = await FindOwnedBookingAsync(id, customerCode);
        if (booking is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(booking.AdminNotes) || !booking.AdminNotes.StartsWith(ReconfirmPrefix))
        {
            TempData["BookingRejectedError"] = "Esta reserva no tiene una re-confirmación pendiente.";
            return RedirectToAction("User", "Accounts");
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
            TempData["BookingCreated"] = "Rechazaste la re-confirmación y se libero el horario.";
        }

        booking.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return RedirectToAction("User", "Accounts");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(long id, int rating, string? comment, string? customerCode = null)
    {
        var booking = await FindOwnedBookingAsync(id, customerCode);
        if (booking is null)
        {
            return NotFound(new { message = "No encontramos la reserva." });
        }

        if (rating < 1 || rating > 10)
        {
            return BadRequest(new { message = "La calificación debe estar entre 1 y 10." });
        }

        var normalizedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            return BadRequest(new { message = "El comentario es requerido." });
        }

        if (normalizedComment.Length > 1000)
        {
            return BadRequest(new { message = "El comentario no puede exceder 1000 caracteres." });
        }

        var nowUtc = DateTime.UtcNow;
        var existingReview = await _dbContext.Reviews
            .FirstOrDefaultAsync(review => review.CustomerId == booking.CustomerId && review.BookingId == booking.Id);

        if (existingReview is not null)
        {
            return BadRequest(new { message = "Ya enviaste una reseña para esta reserva." });
        }

        _dbContext.Reviews.Add(new Review
        {
            CustomerId = booking.CustomerId,
            BookingId = booking.Id,
            RentalId = booking.Rental?.Id,
            Rating = rating,
            Comment = normalizedComment,
            CreatedAt = nowUtc
        });

        await _dbContext.SaveChangesAsync();
        return Json(new
        {
            message = "Gracias por compartir tu reseña.",
            reviewRating = rating,
            reviewComment = normalizedComment,
            reviewCreatedAt = nowUtc
        });
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

    private static byte[]? DecodeSignaturePngOrNull(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return null;
        }

        const string prefix = "data:image/png;base64,";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var decoded = Convert.FromBase64String(signature[prefix.Length..]);
            return decoded.Length > 0 ? decoded : null;
        }
        catch
        {
            return null;
        }
    }

    private string? ResolveCustomerCode(string? customerCode)
    {
        return string.IsNullOrWhiteSpace(customerCode) ? Request.Cookies[CustomerCodeCookieName] : customerCode;
    }

    private async Task<Booking?> FindOwnedBookingAsync(long id, string? customerCode)
    {
        var resolvedCustomerCode = ResolveCustomerCode(customerCode);
        if (string.IsNullOrWhiteSpace(resolvedCustomerCode))
        {
            return null;
        }

        var normalized = resolvedCustomerCode.Trim().ToUpperInvariant();
        return await _dbContext.Bookings
            .Include(booking => booking.Customer)
            .Include(booking => booking.Rental)
            .FirstOrDefaultAsync(booking => booking.Id == id
                && booking.Customer != null
                && booking.Customer.CustomerCode.ToUpper() == normalized);
    }
}
