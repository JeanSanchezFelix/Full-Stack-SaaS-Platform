using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class BookingsController(AppDbContext dbContext) : Controller
{
    [HttpGet]
    public IActionResult Create(string? licenseNumber = null)
    {
        return View(new BookingCreateViewModel
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
    public async Task<IActionResult> Create(BookingCreateViewModel model)
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

        if (string.IsNullOrWhiteSpace(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electronica es requerida para reservar.");
        }

        if (model.ScooterQuantity <= 0)
        {
            ModelState.AddModelError(nameof(model.ScooterQuantity), "Selecciona al menos un scooter.");
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

        customer.ElectronicSignature = model.ElectronicSignature!.Trim();
        customer.LiabilityWaiverSigned = true;
        customer.LiabilityWaiverSignedAt = DateTime.Now;
        customer.UpdatedAt = DateTime.Now;

        var booking = new Booking
        {
            CustomerId = customer.Id,
            RequestedStart = model.RequestedStart,
            RequestedEnd = model.RequestedEnd,
            ScooterQuantity = model.ScooterQuantity,
            EbikeQuantity = model.EbikeQuantity,
            Status = "pending",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync();

        TempData["BookingCreated"] = "Tu solicitud de reserva fue enviada. Te contactaremos cuando sea aprobada.";
        return RedirectToAction(nameof(Create));
    }

    [HttpGet]
    public async Task<IActionResult> Pending()
    {
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
        var booking = await dbContext.Bookings
            .Include(existingBooking => existingBooking.Rental)
            .FirstOrDefaultAsync(existingBooking => existingBooking.Id == id);

        if (booking is null)
        {
            return NotFound();
        }

        booking.Status = "approved";
        booking.ApprovedBy = "admin";
        booking.ApprovedAt = DateTime.Now;
        booking.UpdatedAt = DateTime.Now;

        if (booking.Rental is null)
        {
            dbContext.Rentals.Add(new Rental
            {
                BookingId = booking.Id,
                CustomerId = booking.CustomerId,
                StartTime = booking.RequestedStart,
                EndTime = booking.RequestedEnd,
                ScooterQuantity = booking.ScooterQuantity,
                EbikeQuantity = booking.EbikeQuantity,
                CreatedAt = DateTime.Now
            });
        }

        await dbContext.SaveChangesAsync();
        TempData["BookingApproved"] = "Reserva aprobada y movida a rentals.";
        return RedirectToAction(nameof(Pending));
    }
}
