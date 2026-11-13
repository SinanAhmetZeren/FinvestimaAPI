namespace FinvestimaAPI.Dtos.RegisterLoginDtos
{
    public class RegisterDto
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Phone { get; set; }
        public bool KvkkConsent { get; set; } = false;
        public bool MarketingConsent { get; set; } = false;

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Sector { get; set; }
        public string? SubSector { get; set; }
        public string? Employees { get; set; }
        public string? Revenue { get; set; }
        public string? Purpose { get; set; }
    }
}
