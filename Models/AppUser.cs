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
        public string Language { get; set; } = "tr";

        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Phone { get; set; }
        public bool KvkkConsent { get; set; } = false;
        public DateTime? KvkkConsentAt { get; set; }
        public bool MarketingConsent { get; set; } = false;

        // Admin tarafından "screening" sonrası onaylandı mı — onaylanmadan Sanal POS'tan
        // kredi satın alamaz (bkz. ValuatorProController.AdminApproveUser).
        public bool IsApprovedForPurchase { get; set; } = false;
    }
}
