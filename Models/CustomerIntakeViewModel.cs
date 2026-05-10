using System.ComponentModel.DataAnnotations;

namespace SvelteHybridMVC.Models;

public class CustomerIntakeViewModel
{
    public string? ReturnUrl { get; set; }

    [Required(ErrorMessage = "El nombre es requerido.")]
    [Display(Name = "Nombre")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El apellido es requerido.")]
    [Display(Name = "Apellido")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El numero de licencia es requerido.")]
    [Display(Name = "Numero de licencia")]
    public string? LicenseNumber { get; set; }

    [Required(ErrorMessage = "El numero de telefono es requerido.")]
    [RegularExpression(@"^\+?[0-9()\-\s]{7,20}$", ErrorMessage = "Entra un numero de telefono valido.")]
    [Display(Name = "Numero de telefono")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electronico es requerido.")]
    [EmailAddress(ErrorMessage = "Entra un correo electronico valido.")]
    [Display(Name = "Correo electronico")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "El pueblo de procedencia es requerido.")]
    [Display(Name = "Pueblo de procedencia")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "El pais es requerido.")]
    [Display(Name = "Pais")]
    public string Country { get; set; } = string.Empty;

    [Display(Name = "Como supiste de nosotros?")]
    public string? HowDidYouHear { get; set; }

    [Display(Name = "Comentarios adicionales")]
    public string? Observations { get; set; }

    [Display(Name = "He leido y acepto el relevo de responsabilidad.")]
    public bool LiabilityWaiverSigned { get; set; }

    [Required(ErrorMessage = "La firma electronica es requerida.")]
    [Display(Name = "Firma electronica ")]
    public string? ElectronicSignature { get; set; }

    [Display(Name = "Autorizo recibir comunicaciones promocionales relacionadas con Scooter de la Bahia via correo electronico o WhatsApp.")]
    public bool AuthorizeRecontact { get; set; }
}
