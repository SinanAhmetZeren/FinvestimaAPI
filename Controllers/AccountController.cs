using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.RegisterLoginDtos;
using FinvestimaAPI.Helpers;
using FinvestimaAPI.Models;
using FinvestimaAPI.Services.Token;
using FinvestimaAPI.Services.EmailSender;

namespace FinvestimaAPI.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private static readonly TimeSpan RefreshExpiry = TimeSpan.FromDays(30);

        private readonly UserManager<AppUser> _userManager;
        private readonly TokenService _tokenService;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailSender _emailSender;
        private readonly DataContext _context;

        public AccountController(
            UserManager<AppUser> userManager,
            TokenService tokenService,
            ILogger<AccountController> logger,
            IEmailSender emailSender,
            DataContext context
        )
        {
            _userManager = userManager;
            _tokenService = tokenService;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserResponseDto>> Login(LoginDto loginDto)
        {
            var normalizedEmail = _userManager.NormalizeEmail(loginDto.Email?.Trim());

            AppUser? user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

            if (user == null || !user.Confirmed)
            {
                _logger.LogWarning("Login failed. Email not found or not confirmed. Email: {Email}", loginDto.Email);
                return Unauthorized("Invalid credentials");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, loginDto.Password);
            if (!passwordValid)
            {
                _logger.LogWarning("Login failed. Invalid password. UserId: {UserId}", user.Id);
                return Unauthorized("Invalid credentials");
            }

            user.RefreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Refresh token update failed. UserId: {UserId}", user.Id);
                return StatusCode(500, "Login failed");
            }

            _logger.LogInformation("Login success. UserId: {UserId}", user.Id);
            return await CreateUserObject(user);
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserResponseDto>> Register(RegisterDto registerDto)
        {
            if (string.IsNullOrWhiteSpace(registerDto.Email) ||
                string.IsNullOrWhiteSpace(registerDto.UserName) ||
                string.IsNullOrWhiteSpace(registerDto.Password))
            {
                return BadRequest("Missing required fields.");
            }

            // UserName alanı artık "Ad Soyad" olarak kullanılıyor — boşluk ve Türkçe karakterlere izin verilir.
            var trimmedUsername = registerDto.UserName.Trim();
            if (trimmedUsername.Length < 3 || trimmedUsername.Length > 50)
                return BadRequest("Ad soyad 3-50 karakter olmalı.");
            if (!Regex.IsMatch(trimmedUsername, @"^[\p{L}0-9 '-]+$"))
                return BadRequest("Ad soyad yalnızca harf, rakam, boşluk, kesme işareti ve tire içerebilir.");

            if (!Regex.IsMatch(registerDto.Email.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                return BadRequest("Invalid email format.");

            var (corpOk, corpError) = CorporateEmailChecker.Check(registerDto.Email, registerDto.Website);
            if (!corpOk) return BadRequest(corpError);

            var normalizedEmail = _userManager.NormalizeEmail(registerDto.Email.Trim());
            var normalizedUserName = _userManager.NormalizeName(trimmedUsername);

            var creditBalance = await _context.AllowedEmails
                .Where(a => a.NormalizedEmail == normalizedEmail)
                .SumAsync(a => (int?)a.TaskCredits) ?? 0;
            if (creditBalance <= 0)
            {
                _logger.LogWarning("Register blocked. No task credits left: {Email}", registerDto.Email);
                return BadRequest(new { code = "no_access", error = "Bu e-posta adresiyle kayıt olabilmeniz için önce yöneticinizin size erişim tanımlaması gerekiyor. Lütfen yöneticinizle iletişime geçin." });
            }

            var existingUser = await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.NormalizedEmail == normalizedEmail ||
                    u.NormalizedUserName == normalizedUserName);

            if (existingUser != null)
            {
                if (existingUser.Confirmed)
                {
                    if (existingUser.NormalizedEmail == normalizedEmail && existingUser.NormalizedUserName == normalizedUserName)
                        ModelState.AddModelError("Email and Username", "Email and Username are already taken");
                    else if (existingUser.NormalizedEmail == normalizedEmail)
                        ModelState.AddModelError("Email", "Email is already taken");
                    else
                        ModelState.AddModelError("Username", "Username is already taken");

                    return ValidationProblem();
                }

                // Re-register unconfirmed account
                string confirmationCode = new CodeGenerator().GenerateCode();

                existingUser.UserName = trimmedUsername;
                existingUser.ConfirmationCode = confirmationCode;
                existingUser.NormalizedUserName = normalizedUserName;
                existingUser.NormalizedEmail = normalizedEmail;
                ApplyProfileFields(existingUser, registerDto);

                var passwordHasher = new PasswordHasher<AppUser>();
                existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, registerDto.Password);
                existingUser.RefreshToken = _tokenService.GenerateRefreshToken();
                existingUser.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);

                var reregisterResult = await _userManager.UpdateAsync(existingUser);
                if (!reregisterResult.Succeeded)
                {
                    _logger.LogError("User update failed during re-register. UserId: {UserId}", existingUser.Id);
                    return StatusCode(500);
                }

                await UpsertOwnerCompanyAndTaskAsync(existingUser.Id, registerDto);
                await _context.SaveChangesAsync();

                SendConfirmationEmailSafe(existingUser);
                return await CreateUserObject(existingUser);
            }

            string newConfirmationCode = new CodeGenerator().GenerateCode();

            var newUser = new AppUser
            {
                Email = registerDto.Email.Trim(),
                UserName = trimmedUsername,
                ConfirmationCode = newConfirmationCode,
                EmailConfirmed = false,
                NormalizedEmail = normalizedEmail,
                NormalizedUserName = normalizedUserName,
                PublicId = await GenerateUniquePublicId(),
            };
            ApplyProfileFields(newUser, registerDto);

            var createResult = await _userManager.CreateAsync(newUser, registerDto.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogError("User creation failed. Email: {Email}, Errors: {Errors}", newUser.Email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return BadRequest(createResult.Errors);
            }

            newUser.RefreshToken = _tokenService.GenerateRefreshToken();
            newUser.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);
            await _userManager.UpdateAsync(newUser);

            await UpsertOwnerCompanyAndTaskAsync(newUser.Id, registerDto);
            await _context.SaveChangesAsync();

            SendConfirmationEmailSafe(newUser);

            _logger.LogInformation("New user registered successfully. Email: {Email}", newUser.Email);
            return await CreateUserObject(newUser);
        }

        [HttpPost("sendCode/{email}")]
        public async Task<IActionResult> SendCode(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest("Email is required.");

            var normalizedEmail = _userManager.NormalizeEmail(email.Trim());
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);

            if (user == null)
                return BadRequest("No user found with this email.");

            var confirmationCode = new CodeGenerator().GenerateCode();
            user.ConfirmationCode = confirmationCode;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return StatusCode(500, "Failed to generate code.");

            var userEmail = user.Email!;
            var userName = user.UserName ?? string.Empty;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailSender.SendConfirmationEmail(userEmail, confirmationCode, userName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SendCode email failed. UserId={UserId}", user.Id);
                }
            });

            return Ok(new { message = "Confirmation code sent." });
        }

        [HttpPost("resetPassword")]
        public async Task<ActionResult<UserResponseDto>> ResetPassword(UpdatePasswordDto updatePasswordDto)
        {
            var normalizedEmail = _userManager.NormalizeEmail(updatePasswordDto.Email?.Trim());

            var existingUser = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.NormalizedEmail == normalizedEmail &&
                u.ConfirmationCode == updatePasswordDto.ConfirmationCode &&
                u.Confirmed);

            if (existingUser == null)
            {
                ModelState.AddModelError("email", "Invalid email or confirmation code");
                return ValidationProblem();
            }

            var newPassword = updatePasswordDto.Password;
            if (newPassword == null ||
                newPassword.Length < 8 ||
                !Regex.IsMatch(newPassword, @"[A-Z]") ||
                !Regex.IsMatch(newPassword, @"[a-z]") ||
                !Regex.IsMatch(newPassword, @"[0-9]"))
            {
                return BadRequest("Password must be at least 8 characters and include an uppercase letter, a lowercase letter, and a number.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(existingUser);
            var result = await _userManager.ResetPasswordAsync(existingUser, token, updatePasswordDto.Password);

            if (!result.Succeeded)
                return BadRequest("Password reset failed");

            existingUser.ConfirmationCode = null;
            existingUser.RefreshToken = _tokenService.GenerateRefreshToken();
            existingUser.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);
            await _userManager.UpdateAsync(existingUser);

            return Ok(await CreateUserObject(existingUser));
        }

        [HttpPost("confirmCode")]
        public async Task<ActionResult<UserResponseDto>> ConfirmCode(ConfirmDto confirmDto)
        {
            var normalizedEmail = _userManager.NormalizeEmail(confirmDto.Email?.Trim());
            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail);

            if (user == null)
                return BadRequest("User not found");

            if (user.ConfirmationCode != confirmDto.Code)
                return BadRequest("Invalid confirmation code");

            user.Confirmed = true;
            user.ConfirmationCode = null;
            user.RefreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
                return StatusCode(500, "Failed to confirm code");

            return await CreateUserObject(user);
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<UserResponseDto>> RefreshToken([FromBody] RefreshTokenDto refreshTokenDto)
        {
            var token = refreshTokenDto.RefreshToken?.Trim();
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.RefreshToken == token);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
                return StatusCode(401, new { error = "Invalid or expired refresh token" });

            var newAccessToken = _tokenService.CreateToken(user);
            user.RefreshToken = _tokenService.GenerateRefreshToken();
            user.RefreshTokenExpiryTime = DateTime.UtcNow.Add(RefreshExpiry);
            await _userManager.UpdateAsync(user);

            var userResponse = await CreateUserObject(user);
            userResponse.Token = newAccessToken;
            return userResponse;
        }

        private static void ApplyProfileFields(AppUser user, RegisterDto dto)
        {
            if (!string.IsNullOrWhiteSpace(dto.FullName)) user.FullName = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Role)) user.Role = dto.Role.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Phone)) user.Phone = dto.Phone.Trim();
            if (dto.KvkkConsent) { user.KvkkConsent = true; user.KvkkConsentAt = DateTime.UtcNow; }
            user.MarketingConsent = dto.MarketingConsent;
        }

        // Firma profilini kaydeder ve — ilk onboarding ise ve kredi varsa — otomatik bir
        // ValuationTask açar (admin'in elle "Task oluştur" yapmasının yerini alır).
        private async Task UpsertOwnerCompanyAndTaskAsync(string userId, RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CompanyName)) return;

            var normalizedEmail = _userManager.NormalizeEmail(dto.Email.Trim());
            var company = await _context.Companies
                .Include(c => c.ValuationTasks)
                .FirstOrDefaultAsync(c => c.OwnerUserId == userId);
            var isFirstOnboarding = company == null;
            if (company == null)
            {
                company = new Models.ValuatorPro.Company { Id = Guid.NewGuid(), OwnerUserId = userId };
                _context.Companies.Add(company);
            }

            company.Name = dto.CompanyName.Trim();
            company.Website = CorporateEmailChecker.WebsiteHost(dto.Website) ?? dto.Website?.Trim();
            company.Sector = dto.Sector?.Trim();
            company.SubSector = dto.SubSector?.Trim();
            company.Employees = dto.Employees?.Trim();
            company.Revenue = dto.Revenue?.Trim();
            company.Purpose = dto.Purpose?.Trim();

            if (isFirstOnboarding)
            {
                var balance = await _context.AllowedEmails.Where(a => a.NormalizedEmail == normalizedEmail).SumAsync(a => (int?)a.TaskCredits) ?? 0;
                if (balance > 0)
                {
                    _context.ValuationTasks.Add(new Models.ValuatorPro.ValuationTask
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        Title = "Değerleme",
                        UserId = userId,
                    });
                    // Kullanım satırı: bakiyeden bir kredi düşer, ayrı bir defter satırı olarak.
                    _context.AllowedEmails.Add(new AllowedEmail
                    {
                        Id = Guid.NewGuid(),
                        Email = dto.Email.Trim(),
                        NormalizedEmail = normalizedEmail!,
                        Note = "Task oluşturuldu: " + company.Name,
                        AddedByUserId = userId,
                        TaskCredits = -1,
                    });
                }
            }
        }

        private async Task<UserResponseDto> CreateUserObject(AppUser user)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.OwnerUserId == user.Id);
            return new UserResponseDto
            {
                Token = _tokenService.CreateToken(user),
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                UserId = user.Id,
                RefreshToken = user.RefreshToken ?? string.Empty,
                RefreshTokenExpiryTime = user.RefreshTokenExpiryTime,
                IsAdmin = user.IsAdmin,
                Language = user.Language,
                FullName = user.FullName,
                Role = user.Role,
                Phone = user.Phone,
                CompanyName = company?.Name,
                Website = company?.Website,
                Sector = company?.Sector,
                SubSector = company?.SubSector,
                Employees = company?.Employees,
                Revenue = company?.Revenue,
                Purpose = company?.Purpose,
            };
        }

        private async Task<string> GenerateUniquePublicId()
        {
            int length = 10;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            while (true)
            {
                char[] id = new char[length];
                byte[] randomBytes = new byte[length];
                RandomNumberGenerator.Fill(randomBytes);
                for (int i = 0; i < length; i++)
                    id[i] = chars[randomBytes[i] % chars.Length];

                string publicId = new string(id);
                var exists = await _userManager.Users.AnyAsync(u => u.PublicId == publicId);
                if (!exists)
                    return publicId;
            }
        }

        private void SendConfirmationEmailSafe(AppUser user)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailSender.SendRegistrationEmail(user.Email!, user.ConfirmationCode!, user.UserName!);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Async confirmation email failed. UserId={UserId}, Email={Email}", user.Id, user.Email);
                }
            });
        }
    }
}
