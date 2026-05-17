using System.ComponentModel.DataAnnotations;

namespace SvelteHybridMVC.Models;

public class UserAuthViewModel
{
    [Display(Name = "Es tu primera visita?")]
    public bool IsFirstTime { get; set; } = true;

    [Required]
    [Display(Name = "Numero de licencia")]
    public string? LicenseNumber { get; set; }
}
