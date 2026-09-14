using Microsoft.AspNetCore.Identity;

namespace FinvestimaAPI.Models
{
    public class AppUser : IdentityUser
    {
        public string PublicId { get; set; } = string.Empty;
        public string? ConfirmationCode { get; set; }
        public bool Confirmed { get; set; } = false;
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsAdmin { get; set; } = false;
    }
}
