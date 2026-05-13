using ClosedXML.Excel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Infrastructure.Security;
using SvelteHybridMVC.Models;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace SvelteHybridMVC.Controllers;

public class AccountsController(AppDbContext dbContext, AdminPasswordHasher passwordHasher) : Controller
{
    private const string LicenseCookieName = "sb_license";
    private const string CustomerCodeCookieName = "sb_customer_code";
    private const string ReconfirmPrefix = "[RECONFIRM]";

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AdminLogin(string? returnUrl = null)
    {
        if (HttpContext.User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new AdminLoginViewModel
        {
            ReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? Url.Action(nameof(Admin)) : returnUrl
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogin(AdminLoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userName = model.UserName.Trim();
        var normalizedUserName = userName.ToLowerInvariant();
        var adminUser = await dbContext.AdminUsers
            .FirstOrDefaultAsync(existingUser => existingUser.UserName.ToLower() == normalizedUserName);

        if (adminUser is null || !passwordHasher.VerifyPassword(model.Password, adminUser.Salt, adminUser.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Usuario o contraseña incorrectas.");
            return View(model);
        }

        await SignInAdminAsync(adminUser);
        return RedirectToLocal(model.ReturnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminLogout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(AdminLogin));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAdminUsername(string userName, string tab = "bookings")
    {
        var adminUser = await FindCurrentAdminUserAsync();
        if (adminUser is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(AdminLogin));
        }

        if (string.IsNullOrWhiteSpace(userName))
        {
            TempData["AdminAccountError"] = "El usuario admin no puede estar vacio.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        var cleanUserName = userName.Trim();
        var normalizedUserName = cleanUserName.ToLowerInvariant();
        var userNameInUse = await dbContext.AdminUsers
            .AsNoTracking()
            .AnyAsync(existingUser => existingUser.Id != adminUser.Id && existingUser.UserName.ToLower() == normalizedUserName);

        if (userNameInUse)
        {
            TempData["AdminAccountError"] = "Ese usuario admin ya existe.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        adminUser.UserName = cleanUserName;
        await dbContext.SaveChangesAsync();
        await SignInAdminAsync(adminUser);

        TempData["AdminAccountUpdated"] = "Usuario admin actualizado.";
        return RedirectToAction(nameof(Admin), new { tab });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAdminPassword(string currentPassword, string newPassword, string confirmPassword, string tab = "bookings")
    {
        var adminUser = await FindCurrentAdminUserAsync();
        if (adminUser is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(AdminLogin));
        }

        if (string.IsNullOrWhiteSpace(currentPassword)
            || string.IsNullOrWhiteSpace(newPassword)
            || string.IsNullOrWhiteSpace(confirmPassword))
        {
            TempData["AdminAccountError"] = "Completa todos los campos de contraseña.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        if (!passwordHasher.VerifyPassword(currentPassword, adminUser.Salt, adminUser.PasswordHash))
        {
            TempData["AdminAccountError"] = "La contraseña actual no coincide.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        if (newPassword.Length < 12)
        {
            TempData["AdminAccountError"] = "La nueva contraseña debe tener al menos 12 caracteres.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            TempData["AdminAccountError"] = "La confirmacion de contraseña no coincide.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        var updatedPassword = passwordHasher.HashPassword(newPassword);
        adminUser.Salt = updatedPassword.Salt;
        adminUser.PasswordHash = updatedPassword.PasswordHash;
        await dbContext.SaveChangesAsync();

        TempData["AdminAccountUpdated"] = "Contraseña admin actualizada.";
        return RedirectToAction(nameof(Admin), new { tab });
    }

    [Authorize]
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
                EstimatedTotal = booking.EstimatedTotal,
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
                AuthorizeRecontact = customer.AuthorizeRecontact,
                CreatedAt = customer.CreatedAt,
                Bookings = customer.Bookings
                    .OrderByDescending(booking => booking.CreatedAt)
                    .Select(booking => new AdminCustomerBookingHistoryItemViewModel
                    {
                        Id = booking.Id,
                        RequestedStart = booking.RequestedStart,
                        RequestedEnd = booking.RequestedEnd,
                        Status = NormalizeStatus(booking.Status),
                        ScooterQuantity = booking.ScooterQuantity,
                        EbikeQuantity = booking.EbikeQuantity,
                        EstimatedTotal = booking.EstimatedTotal,
                        AdminNotes = NormalizeAdminNotes(booking.AdminNotes)
                    })
                    .ToList()
            })
            .ToListAsync();

        return View(new AdminPanelViewModel
        {
            Bookings = bookings,
            Customers = customers,
            ActiveTab = string.Equals(tab, "clients", StringComparison.OrdinalIgnoreCase) ? "clients" : "bookings",
            AdminUserName = HttpContext.User.Identity?.Name ?? "admin"
        });
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ExportCustomers()
    {
        var customers = await dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.LastName)
            .ThenBy(customer => customer.FirstName)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Clientes");
        ws.Cell(1, 1).Value = "Nombre";
        ws.Cell(1, 2).Value = "Número de licencia";
        ws.Cell(1, 3).Value = "Número de teléfono";
        ws.Cell(1, 4).Value = "Email";
        ws.Cell(1, 5).Value = "Pueblo";
        ws.Cell(1, 6).Value = "País";
        ws.Cell(1, 7).Value = "Firma electrónica";
        ws.Cell(1, 8).Value = "¿Cómo escuchaste de nosotros?";
        ws.Cell(1, 9).Value = "Autoriza recontacto";
        ws.Cell(1, 10).Value = "Cuenta creada en";

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var customer in customers)
        {
            ws.Cell(row, 1).Value = customer.FirstName + " " + customer.LastName;
            ws.Cell(row, 2).Value = customer.LicenseNumber;
            ws.Cell(row, 3).Value = customer.PhoneNumber;
            ws.Cell(row, 4).Value = customer.Email ?? string.Empty;
            ws.Cell(row, 5).Value = customer.City;
            ws.Cell(row, 6).Value = customer.Country;
            ws.Cell(row, 7).Value = string.Empty;
            ws.Cell(row, 8).Value = customer.HowDidYouHear ?? string.Empty;
            ws.Cell(row, 9).Value = customer.AuthorizeRecontact ? "Sí" : "No";
            ws.Cell(row, 10).Value = customer.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

            if (TryGetSignaturePngBytes(customer.ElectronicSignature, out var signatureBytes))
            {
                using var signatureStream = new MemoryStream(signatureBytes);
                var picture = ws.AddPicture(signatureStream, $"signature-{customer.Id}");
                picture.MoveTo(ws.Cell(row, 7), 4, 4);
                picture.WithSize(180, 56);
                ws.Row(row).Height = 46;
            }

            row++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 6).AdjustToContents();
        ws.Column(7).Width = 25;
        ws.Column(8).AdjustToContents();
        ws.Column(9).AdjustToContents();
        ws.Column(10).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"clientes-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ExportBookings()
    {
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Customer)
            .OrderByDescending(booking => booking.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Reservas");
        ws.Cell(1, 1).Value = "Nombre de cliente";
        ws.Cell(1, 2).Value = "Licencia";
        ws.Cell(1, 3).Value = "Comienza";
        ws.Cell(1, 4).Value = "Concluye";
        ws.Cell(1, 5).Value = "Scooters";
        ws.Cell(1, 6).Value = "E-bikes";
        ws.Cell(1, 7).Value = "Total estimado";
        // ws.Cell(1, 10).Value = "Estado";
        // ws.Cell(1, 11).Value = "Nota admin";
        ws.Cell(1, 8).Value = "Reserva creada en";

        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);

        var row = 2;
        foreach (var booking in bookings)
        {
            var customerName = booking.Customer == null
                ? "Cliente"
                : $"{booking.Customer.FirstName} {booking.Customer.LastName}".Trim();

            ws.Cell(row, 1).Value = customerName;
            ws.Cell(row, 2).Value = booking.Customer?.LicenseNumber ?? string.Empty;
            ws.Cell(row, 3).Value = booking.RequestedStart.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 4).Value = booking.RequestedEnd?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
            ws.Cell(row, 5).Value = booking.ScooterQuantity;
            ws.Cell(row, 6).Value = booking.EbikeQuantity;
            ws.Cell(row, 7).Value = booking.EstimatedTotal;
            // ws.Cell(row, 10).Value = NormalizeStatus(booking.Status);
            // ws.Cell(row, 11).Value = NormalizeAdminNotes(booking.AdminNotes) ?? string.Empty;
            ws.Cell(row, 8).Value = booking.CreatedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
            row++;
        }

        ws.Columns(1, 8).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"reservas-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomer(long id, string tab = "clients")
    {
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(existingCustomer => existingCustomer.Id == id);

        if (customer is null)
        {
            TempData["BookingRejectedError"] = "No encontramos el cliente que intentaste eliminar.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync();

        TempData["BookingApproved"] = $"Cliente {customer.FirstName} {customer.LastName} eliminado junto con sus reservas.";
        return RedirectToAction(nameof(Admin), new { tab });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBooking(long id, string tab = "bookings")
    {
        var booking = await dbContext.Bookings
            .FirstOrDefaultAsync(existingBooking => existingBooking.Id == id);

        if (booking is null)
        {
            TempData["BookingRejectedError"] = "No encontramos la reserva que intentaste eliminar.";
            return RedirectToAction(nameof(Admin), new { tab });
        }

        dbContext.Bookings.Remove(booking);
        await dbContext.SaveChangesAsync();

        TempData["BookingApproved"] = "Reserva eliminada correctamente.";
        return RedirectToAction(nameof(Admin), new { tab });
    }

    public new async Task<IActionResult> User(string? licenseNumber = null)
    {
        await RejectExpiredPendingBookingsAsync();

        Customer? currentCustomer = null;
        if (!string.IsNullOrWhiteSpace(licenseNumber))
        {
            var normalizedLicense = licenseNumber.Trim().ToLowerInvariant();
            currentCustomer = await dbContext.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower() == normalizedLicense);
        }
        else
        {
            var cookieCustomerCode = Request.Cookies[CustomerCodeCookieName];
            if (!string.IsNullOrWhiteSpace(cookieCustomerCode))
            {
                var normalizedCode = cookieCustomerCode.Trim().ToUpperInvariant();
                currentCustomer = await dbContext.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(customer => customer.CustomerCode.ToUpper() == normalizedCode);
            }
        }

        object? customerInfo = null;
        if (currentCustomer is not null)
        {
            var normalizedCode = currentCustomer.CustomerCode.Trim().ToUpperInvariant();
            customerInfo = await dbContext.Customers
                .AsNoTracking()
                .Where(customer => customer.CustomerCode.ToUpper() == normalizedCode)
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

            Response.Cookies.Append(CustomerCodeCookieName, currentCustomer.CustomerCode, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
        }
        else
        {
            Response.Cookies.Delete(CustomerCodeCookieName);
            Response.Cookies.Delete(LicenseCookieName);
        }

        ViewData["CustomerInfo"] = customerInfo;

        return View(new BookingCreateViewModel
        {
            LicenseNumber = currentCustomer?.LicenseNumber ?? licenseNumber
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
            ModelState.AddModelError(nameof(model.LicenseNumber), "Entra tu número de licencia para buscar tu cuenta.");
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

        Response.Cookies.Append(CustomerCodeCookieName, customer.CustomerCode, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });

        return RedirectToAction(nameof(User));
    }

    [HttpGet]
    public IActionResult LogoffProfile()
    {
        Response.Cookies.Delete(CustomerCodeCookieName);
        Response.Cookies.Delete(LicenseCookieName);
        return RedirectToAction("Create", "Bookings");
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.CustomerCode))
        {
            return BadRequest(new { message = "Codigo de cliente requerido." });
        }

        var normalizedCode = request.CustomerCode.Trim().ToUpperInvariant();
        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(c => c.CustomerCode.ToUpper() == normalizedCode);

        if (customer is null)
        {
            return NotFound(new { message = "No encontramos la cuenta." });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName)
            || string.IsNullOrWhiteSpace(request.LastName)
            || string.IsNullOrWhiteSpace(request.LicenseNumber)
            || string.IsNullOrWhiteSpace(request.City)
            || string.IsNullOrWhiteSpace(request.Country))
        {
            return BadRequest(new { message = "Nombre, apellido, licencia, ciudad y pais son requeridos." });
        }

        var cleanedLicense = request.LicenseNumber.Trim();
        var cleanedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        var cleanedPhone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(cleanedEmail))
        {
            try { _ = new MailAddress(cleanedEmail); }
            catch { return BadRequest(new { message = "Correo electronico invalido." }); }
        }

        if (!string.IsNullOrWhiteSpace(cleanedPhone))
        {
            if (!Regex.IsMatch(cleanedPhone, @"^\+?[0-9()\-\s]{7,20}$") || cleanedPhone.Count(char.IsDigit) is < 7 or > 15)
            {
                return BadRequest(new { message = "Telefono invalido." });
            }
        }

        var normalizedNewLicense = cleanedLicense.ToLowerInvariant();
        var licenseInUse = await dbContext.Customers
            .AsNoTracking()
            .AnyAsync(c => c.Id != customer.Id && c.LicenseNumber != null && c.LicenseNumber.ToLower() == normalizedNewLicense);
        if (licenseInUse)
        {
            return BadRequest(new { message = "La licencia ya existe en otra cuenta." });
        }

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.LicenseNumber = cleanedLicense;
        customer.PhoneNumber = cleanedPhone ?? string.Empty;
        customer.Email = cleanedEmail;
        customer.City = request.City.Trim();
        customer.Country = request.Country.Trim();
        customer.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return Json(new
        {
            customerCode = customer.CustomerCode,
            firstName = customer.FirstName,
            lastName = customer.LastName,
            licenseNumber = customer.LicenseNumber,
            phoneNumber = customer.PhoneNumber,
            email = customer.Email,
            city = customer.City,
            country = customer.Country
        });
    }

    public sealed class UpdateProfileRequest
    {
        public string? CustomerCode { get; set; }
        public string? LicenseNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
    }

    private async Task SignInAdminAsync(AdminUser adminUser)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, adminUser.Id.ToString()),
            new(ClaimTypes.Name, adminUser.UserName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
                IsPersistent = true
            });
    }

    private async Task<AdminUser?> FindCurrentAdminUserAsync()
    {
        var adminIdValue = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(adminIdValue, out var adminId)
            ? await dbContext.AdminUsers.FirstOrDefaultAsync(adminUser => adminUser.Id == adminId)
            : null;
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Admin));
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

    private static bool TryGetSignaturePngBytes(byte[]? signature, out byte[] pngBytes)
    {
        pngBytes = [];
        if (signature is not { Length: > 0 })
        {
            return false;
        }

        if (IsPng(signature))
        {
            pngBytes = signature;
            return true;
        }

        // Backward compatibility: some rows may still contain UTF-8 data URL bytes.
        try
        {
            var asText = System.Text.Encoding.UTF8.GetString(signature);
            const string prefix = "data:image/png;base64,";
            if (!asText.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var decoded = Convert.FromBase64String(asText[prefix.Length..]);
            if (!IsPng(decoded))
            {
                return false;
            }

            pngBytes = decoded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPng(byte[] bytes)
    {
        if (bytes.Length < 8)
        {
            return false;
        }

        return bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47
            && bytes[4] == 0x0D
            && bytes[5] == 0x0A
            && bytes[6] == 0x1A
            && bytes[7] == 0x0A;
    }
}








