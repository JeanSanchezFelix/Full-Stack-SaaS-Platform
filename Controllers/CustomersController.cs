using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SvelteHybridMVC.Controllers;

public class CustomersController : Controller
{
    private readonly AppDbContext _dbContext;
    private const string CustomerDraftTempDataKey = "CustomerCreateDraft";
    private const string CustomerDraftErrorsTempDataKey = "CustomerCreateDraftErrors";

    public CustomersController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Create(string? returnUrl = null, string? licenseNumber = null)
    {
        if (TempData[CustomerDraftErrorsTempDataKey] is string errorsJson)
        {
            var errors = JsonSerializer.Deserialize<List<string>>(errorsJson) ?? [];
            ViewData["DraftErrors"] = errors;
        }

        if (TempData[CustomerDraftTempDataKey] is string draftJson)
        {
            var draft = JsonSerializer.Deserialize<CustomerIntakeViewModel>(draftJson);
            if (draft is not null)
            {
                return View(draft);
            }
        }

        return View(new CustomerIntakeViewModel
        {
            Country = "Puerto Rico",
            LicenseNumber = licenseNumber,
            ReturnUrl = returnUrl
        });
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
        else if (!IsValidElectronicSignature(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electronica debe contener su nombre.");
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
            var errors = ModelState.Values
                .SelectMany(state => state.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .ToList();

            TempData[CustomerDraftTempDataKey] = JsonSerializer.Serialize(model);
            TempData[CustomerDraftErrorsTempDataKey] = JsonSerializer.Serialize(errors);
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
            Observations = NormalizeOptional(model.Observations),
            LiabilityWaiverSigned = model.LiabilityWaiverSigned,
            LiabilityWaiverSignedAt = model.LiabilityWaiverSigned ? DateTime.UtcNow : null,
            ElectronicSignature = NormalizeOptional(model.ElectronicSignature),
            AuthorizeRecontact = model.AuthorizeRecontact,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync();

        TempData["CustomerCreated"] = $"{customer.FirstName} {customer.LastName} was saved with customer code {customer.CustomerCode}.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect($"{model.ReturnUrl}?licenseNumber={Uri.EscapeDataString(customer.LicenseNumber ?? string.Empty)}");
        }

        return RedirectToAction(nameof(Create));
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

    private static bool IsValidElectronicSignature(string signature)
    {
        var normalized = signature.Trim();
        if (normalized.Length < 2)
        {
            return false;
        }

        return normalized.Any(char.IsLetter);
    }
}
