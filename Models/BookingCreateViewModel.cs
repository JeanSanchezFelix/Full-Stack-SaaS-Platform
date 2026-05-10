using System.ComponentModel.DataAnnotations;

namespace SvelteHybridMVC.Models;

public class BookingCreateViewModel
{
    [Display(Name = "Número de licencia")]
    public string? LicenseNumber { get; set; }

    [Required]
    [Display(Name = "Fecha y hora")]
    public DateTime RequestedStart { get; set; } = DateTime.Now.AddHours(2);

    [Display(Name = "Termina")]
    public DateTime? RequestedEnd { get; set; }

    [Range(1, 20)]
    [Display(Name = "Scooters")]
    public int ScooterQuantity { get; set; } = 1;

    [Range(0, 20)]
    [Display(Name = "E-bikes")]
    public int EbikeQuantity { get; set; }

    [Display(Name = "He leido el relevo de responsabilidad.")]
    public bool LiabilityWaiverSigned { get; set; }

    [Display(Name = "Firma electrónica")]
    public string? ElectronicSignature { get; set; }
}
