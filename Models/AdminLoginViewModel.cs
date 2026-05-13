using System.ComponentModel.DataAnnotations;

namespace SvelteHybridMVC.Models;

public class AdminLoginViewModel
{
    [Required(ErrorMessage = "Entra el usuario.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Entra la contraseña.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
