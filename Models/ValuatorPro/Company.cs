namespace FinvestimaAPI.Models.ValuatorPro
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ValuationTask> ValuationTasks { get; set; } = new();

        // Onboarding sırasında kullanıcının kendi beyan ettiği firma — her task'ın kendi
        // (muhtemelen anonim) Company kaydından farklı: bu, kullanıcının hesabına bağlı profildir.
        public string? OwnerUserId { get; set; }
        public string? Website { get; set; }
        public string? Sector { get; set; }
        public string? SubSector { get; set; }
        public string? Employees { get; set; }
        public string? Revenue { get; set; }
        public string? Purpose { get; set; }
        public string? ReportEmail { get; set; }

        // Bu task'a özel iletişim kişisi — hesap sahibinin kendi bilgisi değil, bu
        // değerleme/rapor için belirttiği kişi (danışman farklı firmalar için farklı
        // kişi girebilir).
        public string? ContactFullName { get; set; }
        public string? ContactRole { get; set; }
        public string? ContactPhone { get; set; }

        // A0 doğrulama: Task henüz yokken (ValuationTask oluşmadan) bu firma kaydı "beklemede"
        // sayılır. Kod ReportEmail'e gider, 1 dk / 3 dk aralıklarla en fazla 3 kez gönderilebilir.
        public string? ConfirmationCode { get; set; }
        public DateTime? ConfirmationCodeExpiry { get; set; }
        public int ConfirmationSendCount { get; set; }
        public DateTime? ConfirmationLastSentAt { get; set; }
    }
}
