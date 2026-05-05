using System.ComponentModel.DataAnnotations;

namespace SvelteHybridMVC.Models;

public class CustomerIntakeViewModel
{
    [Required]
    [Display(Name = "Nombre")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Apellido")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Número de licencia")]
    public string? LicenseNumber { get; set; }

    [Required]
    [Phone]
    [Display(Name = "Número de teléfono")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    [Display(Name = "Pueblo de procedencia")]
    public string City { get; set; } = string.Empty;

    [Required]
    [Display(Name = "País")]
    public string Country { get; set; } = string.Empty;

    [Display(Name = "He leído el relevo de responsabilidad.")]
    public bool LiabilityWaiverSigned { get; set; }

    [Display(Name = "Firma electrónica")]
    public string? ElectronicSignature { get; set; }

    [Display(Name = "Autorizo recibir comunicaciones promocionales relacionadas con Scooter de la Bahía vía correo electrónico o WhatsApp.")]
    public bool AuthorizeRecontact { get; set; }
}
