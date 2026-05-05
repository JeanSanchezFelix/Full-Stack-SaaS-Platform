using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SvelteHybridMVC.Infrastructure.Data;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class CustomersController(AppDbContext dbContext) : Controller
{
    [HttpGet]
    public IActionResult Create()
    {
        return View(new CustomerIntakeViewModel
        {
            Country = "Puerto Rico"
        });
    }

    [HttpGet]
    public IActionResult LiabilityWaiver()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerIntakeViewModel model)
    {
        if (model.LiabilityWaiverSigned && string.IsNullOrWhiteSpace(model.ElectronicSignature))
        {
            ModelState.AddModelError(nameof(model.ElectronicSignature), "Electronic signature is required when the waiver is signed.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
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
            LiabilityWaiverSigned = model.LiabilityWaiverSigned,
            LiabilityWaiverSignedAt = model.LiabilityWaiverSigned ? DateTime.UtcNow : null,
            ElectronicSignature = NormalizeOptional(model.ElectronicSignature),
            AuthorizeRecontact = model.AuthorizeRecontact,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        TempData["CustomerCreated"] = $"{customer.FirstName} {customer.LastName} was saved with customer code {customer.CustomerCode}.";
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
        while (await dbContext.Customers.AnyAsync(customer => customer.CustomerCode == code));

        return code;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
