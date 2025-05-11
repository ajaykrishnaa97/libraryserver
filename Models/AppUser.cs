using System.ComponentModel.DataAnnotations.Schema;

namespace Library.Models{
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int LibraryId { get; set; } 
    public LibraryBranch LibraryBranch { get; set; }
    public string Email { get; set; }
    public string? PasswordHash { get; set; }
    public string Role { get; set; }
    public bool IsApproved { get; set; } = false;
    public bool HasSetPassword { get; set; } = false;
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpires { get; set; }
}

}

