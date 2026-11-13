namespace FinvestimaAPI.Dtos.RegisterLoginDtos
{
    public class UserResponseDto
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime? RefreshTokenExpiryTime { get; set; }
        public bool IsAdmin { get; set; } = false;
        public string Language { get; set; } = "tr";

        public string? FullName { get; set; }
        public string? Role { get; set; }
        public string? Phone { get; set; }

        public string? CompanyName { get; set; }
        public string? Website { get; set; }
        public string? Sector { get; set; }
        public string? SubSector { get; set; }
        public string? Employees { get; set; }
        public string? Revenue { get; set; }
        public string? Purpose { get; set; }
    }
}
