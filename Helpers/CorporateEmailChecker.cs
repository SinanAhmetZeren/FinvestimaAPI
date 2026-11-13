using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers
{
    // Onboarding'de kurumsal e-posta zorunluluğunu backend tarafında da uygular —
    // frontend'deki (yeni Valuator onboarding formu) domainCheck mantığının aynısı,
    // sadece JS tarafı atlanırsa (doğrudan API çağrısı) diye tekrarlanıyor.
    public static class CorporateEmailChecker
    {
        private static readonly HashSet<string> FreeProviders = new(StringComparer.OrdinalIgnoreCase)
        {
            "gmail.com", "googlemail.com", "hotmail.com", "hotmail.com.tr", "outlook.com", "outlook.com.tr",
            "live.com", "msn.com", "yahoo.com", "yahoo.com.tr", "ymail.com", "icloud.com", "me.com", "mac.com",
            "yandex.com", "yandex.com.tr", "yandex.ru", "mail.ru", "aol.com", "gmx.com", "gmx.de", "proton.me",
            "protonmail.com", "zoho.com", "mynet.com", "windowslive.com",
        };

        public static string? EmailDomain(string? email)
        {
            var m = Regex.Match(email?.Trim() ?? "", @"^[^\s@]+@([a-zA-Z0-9.-]+\.[a-zA-Z]{2,})$");
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : null;
        }

        public static string? WebsiteHost(string? website)
        {
            if (string.IsNullOrWhiteSpace(website)) return null;
            var w = website.Trim();
            if (!Regex.IsMatch(w, @"^https?://", RegexOptions.IgnoreCase)) w = "https://" + w;
            if (!Uri.TryCreate(w, UriKind.Absolute, out var uri)) return null;
            var h = uri.Host.ToLowerInvariant();
            if (h.StartsWith("www.")) h = h[4..];
            return Regex.IsMatch(h, @"^[a-z0-9-]+(\.[a-z0-9-]+)+$") ? h : null;
        }

        // website null/boş geçilirse sadece "ücretsiz sağlayıcı değil" kontrolü yapılır.
        public static (bool Ok, string? Error) Check(string? email, string? website)
        {
            var domain = EmailDomain(email);
            if (domain == null) return (false, "Geçerli bir e-posta adresi girin.");
            // Ücretsiz e-posta sağlayıcı kısıtlaması kaldırıldı (istenerek) — FreeProviders listesi duruyor, gerekirse geri açılabilir.
            // if (FreeProviders.Contains(domain)) return (false, "Ücretsiz e-posta sağlayıcıları kabul edilmiyor. Firmanızın kurumsal adresini kullanın.");

            var host = WebsiteHost(website);
            if (host == null) return (true, null); // website verilmemişse yalnızca ücretsiz-sağlayıcı kontrolü yeterli

            var ok = domain == host || domain.EndsWith("." + host) || host.EndsWith("." + domain);
            return ok ? (true, null) : (false, $"E-posta adresi firmanızın alan adıyla bitmeli: …@{host}");
        }
    }
}
