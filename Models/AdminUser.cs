namespace SvelteHybridMVC.Models;

public class AdminUser
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public byte[] Salt { get; set; } = [];
    public byte[] PasswordHash { get; set; } = [];
}
