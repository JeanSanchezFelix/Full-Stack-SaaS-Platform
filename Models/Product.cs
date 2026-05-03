namespace SvelteHybridMVC.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProductListViewModel
{
    public IEnumerable<Product> Products { get; set; } = [];
    public int TotalCount { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public ProductFilters Filters { get; set; } = new();
    public IEnumerable<Category> Categories { get; set; } = [];
}

public class ProductFilters
{
    public string? SearchTerm { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public List<int> CategoryIds { get; set; } = [];
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
