namespace Shared.Requests;

public class RegisterRequest : IRequest {
    [Required, MaxLength(50)]
    public required string Username { get; set; }
    
    [EmailAddress, Required, MaxLength(100)]
    public required string Email { get; set; }

    [Required]
    public required string Password { get; set; }

    public void Sanitize() {
        Username = Username.Trim();
        Email = Email.Trim();
        Password = Password.Trim();
    }
}