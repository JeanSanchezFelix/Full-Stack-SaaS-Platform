using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;
using System.Text.Json;

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
        if (model.LiabilityWaiverSigned && string.IsNullOrWhiteSpace(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "La firma electronica es requerida para guardar su información.");
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
            LicenseNumber = NormalizeOptional(model.LicenseNumber),
            PhoneNumber = model.PhoneNumber.Trim(),
            Email = NormalizeOptional(model.Email),
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
}
