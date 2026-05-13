using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace SvelteHybridMVC.Controllers;

public class CustomersController : Controller
{
    private readonly AppDbContext _dbContext;
    private const string TempDataCookieName = ".AspNetCore.Mvc.CookieTempDataProvider";
    private const string IntakeModelTempDataKey = "CustomerIntake.Model";
    private const string IntakeErrorsTempDataKey = "CustomerIntake.Errors";

    public CustomersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl = null, string? licenseNumber = null)
    {
        // Clear stale oversized temp-data cookie payloads that can cause HTTP 431.
        Response.Cookies.Delete(TempDataCookieName);

        var model = ReadIntakeModelFromTempData() ?? new CustomerIntakeViewModel
        {
            Country = "Puerto Rico",
            LicenseNumber = licenseNumber,
            ReturnUrl = returnUrl
        };

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            model.ReturnUrl = returnUrl;
        }

        if (!string.IsNullOrWhiteSpace(licenseNumber))
        {
            model.LicenseNumber = licenseNumber;
        }

        ApplyModelStateErrorsFromTempData();

        return View(model);
    }

    [HttpGet]
    public IActionResult LiabilityWaiver(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action(nameof(Create), "Customers");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerIntakeViewModel model)
    {
        model.LicenseNumber = model.LicenseNumber?.Trim();
        model.PhoneNumber = model.PhoneNumber?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim();
        model.ElectronicSignature = model.ElectronicSignature?.Trim();

        var normalizedLicense = NormalizeOptional(model.LicenseNumber);
        var normalizedEmail = NormalizeOptional(model.Email);

        if (!string.IsNullOrWhiteSpace(model.PhoneNumber) && !IsValidPhone(model.PhoneNumber))
        {
            ModelState.AddModelError(nameof(model.PhoneNumber), "Entra un numero de telefono valido.");
        }

        if (!string.IsNullOrWhiteSpace(model.Email) && !IsValidEmail(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Entra un correo electronico valido.");
        }

        if (string.IsNullOrWhiteSpace(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electronica es requerida para guardar su informacion.");
        }
        else if (!TryDecodeSignaturePng(model.ElectronicSignature, out _))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electronica no es valida.");
        }

        if (!model.LiabilityWaiverSigned)
        {
            ModelState.AddModelError(nameof(model.LiabilityWaiverSigned), "Debe aceptar el relevo de responsabilidad para continuar.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedLicense) && await LicenseExistsAsync(normalizedLicense))
        {
            ModelState.AddModelError(nameof(model.LicenseNumber), "Ya existe un cliente registrado con este numero de licencia.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedEmail) && await EmailExistsAsync(normalizedEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "Ya existe un cliente registrado con este correo electronico.");
        }

        if (!ModelState.IsValid)
        {
            StoreIntakeModelInTempData(model);
            StoreModelStateErrorsInTempData();
            return RedirectToAction(nameof(Create), new
            {
                returnUrl = model.ReturnUrl,
                licenseNumber = model.LicenseNumber
            });
        }

        var customer = new Customer
        {
            CustomerCode = await GenerateCustomerCodeAsync(model.FirstName, model.LastName),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            LicenseNumber = normalizedLicense,
            PhoneNumber = NormalizePhoneForStorage(model.PhoneNumber),
            Email = normalizedEmail,
            City = model.City.Trim(),
            Country = model.Country.Trim(),
            HowDidYouHear = NormalizeOptional(model.HowDidYouHear),
            LiabilityWaiverSigned = model.LiabilityWaiverSigned,
            LiabilityWaiverSignedAt = model.LiabilityWaiverSigned ? DateTime.UtcNow : null,
            ElectronicSignature = DecodeSignaturePngOrNull(model.ElectronicSignature),
            AuthorizeRecontact = model.AuthorizeRecontact,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        TempData["CustomerCreated"] = $"El perfil de {customer.FirstName} {customer.LastName} fue guardado con el código de cliente {customer.CustomerCode}.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect($"{model.ReturnUrl}?licenseNumber={Uri.EscapeDataString(customer.LicenseNumber ?? string.Empty)}");
        }

        return RedirectToAction("User", "Accounts", new { licenseNumber = customer.LicenseNumber });
    }

    private async Task<string> GenerateCustomerCodeAsync(string firstName, string lastName)
    {
        var prefixSource = string.IsNullOrWhiteSpace(firstName) ? lastName : firstName;
        var prefix = new string(prefixSource
            .Where(char.IsLetterOrDigit)
            .Take(6)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (prefix.Length == 0)
        {
            prefix = "CLIENT";
        }

        string code;
        do
        {
            code = $"{prefix}-{Random.Shared.Next(1000, 10000)}";
        }
        while (await _dbContext.Customers.AnyAsync(customer => customer.CustomerCode == code));

        return code;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<bool> LicenseExistsAsync(string normalizedLicense)
    {
        var lookup = normalizedLicense.ToLowerInvariant();
        return await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.LicenseNumber != null && customer.LicenseNumber.ToLower() == lookup);
    }

    private async Task<bool> EmailExistsAsync(string normalizedEmail)
    {
        var lookup = normalizedEmail.ToLowerInvariant();
        return await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(customer => customer.Email != null && customer.Email.ToLower() == lookup);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPhone(string phone)
    {
        if (!Regex.IsMatch(phone, @"^\+?[0-9()\-\s]{7,20}$"))
        {
            return false;
        }

        var digits = phone.Count(char.IsDigit);
        return digits >= 7 && digits <= 15;
    }

    private static string NormalizePhoneForStorage(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 10)
        {
            return $"+1{digits}";
        }

        if (digits.Length == 11 && digits.StartsWith("1"))
        {
            return $"+{digits}";
        }

        return digits.Length > 0 ? $"+{digits}" : phone.Trim();
    }

    private static byte[]? DecodeSignaturePngOrNull(string? signature)
    {
        return TryDecodeSignaturePng(signature, out var bytes) ? bytes : null;
    }

    private static bool TryDecodeSignaturePng(string? signature, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var normalized = signature.Trim();
        const string prefix = "data:image/png;base64,";
        if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(normalized[prefix.Length..]);
            return bytes.Length >= 128 && bytes.Length <= 750_000;
        }
        catch
        {
            bytes = [];
            return false;
        }
    }

    private void StoreIntakeModelInTempData(CustomerIntakeViewModel model)
    {
        TempData[IntakeModelTempDataKey] = JsonSerializer.Serialize(model);
    }

    private CustomerIntakeViewModel? ReadIntakeModelFromTempData()
    {
        if (TempData[IntakeModelTempDataKey] is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CustomerIntakeViewModel>(raw);
        }
        catch
        {
            return null;
        }
    }

    private void StoreModelStateErrorsInTempData()
    {
        var errors = ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToArray());

        TempData[IntakeErrorsTempDataKey] = JsonSerializer.Serialize(errors);
    }

    private void ApplyModelStateErrorsFromTempData()
    {
        if (TempData[IntakeErrorsTempDataKey] is not string raw || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        try
        {
            var errors = JsonSerializer.Deserialize<Dictionary<string, string[]>>(raw);
            if (errors is null)
            {
                return;
            }

            foreach (var (key, messages) in errors)
            {
                foreach (var message in messages)
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        ModelState.AddModelError(key, message);
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed temp-data payloads.
        }
    }
}
