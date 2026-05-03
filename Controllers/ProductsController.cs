using Microsoft.AspNetCore.Mvc;
using SvelteHybridMVC.Models;

namespace SvelteHybridMVC.Controllers;

public class ProductsController : Controller
{
    private static readonly List<Product> _products =
    [
        new() { Id = 1, Name = "Product 1", Price = 99.99m, Stock = 50, Category = "Electronics" },
        new() { Id = 2, Name = "Product 2", Price = 149.99m, Stock = 30, Category = "Electronics" },
        new() { Id = 3, Name = "Product 3", Price = 29.99m, Stock = 5, Category = "Clothing" },
    ];

    public IActionResult Index()
    {
        return View(_products.AsEnumerable());
    }

    public IActionResult Edit(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product == null) return NotFound();
        return View(product);
    }

    [HttpPost]
    public IActionResult Edit(Product product)
    {
        if (!ModelState.IsValid) return View(product);

        var existing = _products.FirstOrDefault(p => p.Id == product.Id);
        if (existing != null)
        {
            existing.Name = product.Name;
            existing.Price = product.Price;
            existing.Stock = product.Stock;
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Delete(int id)
    {
        var product = _products.FirstOrDefault(p => p.Id == id);
        if (product != null) _products.Remove(product);
        return RedirectToAction(nameof(Index));
    }
}
