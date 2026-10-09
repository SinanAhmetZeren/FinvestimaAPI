


VALUATOR PRO — DOSYA YÜKLEME, HASSAS BİLGİLERİ TEMİZLEME, GEMINI İLE İŞLEME
========================================
Kullanıcı bir mizan dosyası yüklediğinde şu akış işler:

Dosya önce backend'e ulaşır. Eğer bir Excel dosyasıysa, Gemini'ye gönderilmeden
hemen önce bir ön temizleme adımından geçer: dosyadaki hesap kodu kolonu
(başlık metnine bağlı kalmadan, veriye bakılarak) tespit edilir ve bu kolondaki
en derin kırılım seviyesine ait satırlar (rakam sayısı 3+2+2=7'yi aşan, yani
dördüncü gruba geçen kodlar — bunlar genelde banka şubesi veya müşteri/tedarikçi
adı gibi üçüncü taraf bilgisi taşır) metinden çıkarılır. Bu sadece bir satır
çıkarma işlemidir; herhangi bir toplama ya da kümüleme burada yapılmaz.

Temizlenmiş içerik Gemini'ye gönderilir. Gemini, dosyada gördüğü hesap
kodu/isim/tutar satırlarını olduğu gibi, uydurmadan çıkarıp yapılandırılmış
bir JSON olarak döner. Bu ham veri — hangi derinlikte olursa olsun, satırların
hiçbiri atılmadan ya da birleştirilmeden — aynen veritabanına yazılır.

Asıl kümüleme ve doğrulama işi veritabanına yazma anında değil, veri her
görüntülendiğinde (ister yükleme tamamlandığı anda, ister kullanıcı daha
sonra sayfayı tekrar açtığında) gerçekleşir. Veritabanındaki ham satırlar
okunur, her 3'lü ana kod için kendi altındaki her derinlik seviyesi kendi
içinde toplanıp o seviyeye ait bir "102" adayı üretilir, bu adaylar
birbirleriyle karşılaştırılır. Tüm derinlikler tutuyorsa sorun yok; tutmuyorsa
en az derinlikteki (en güvenilir kabul edilen) değer nihai sonuç olarak
kullanılır ve o kod "mismatch" olarak işaretlenir. Kullanıcı arayüzü bu
işaretli kodları farklı bir renkle göstererek kullanıcının dikkatini çeker.

Bu tasarımın kilit noktası şudur: veritabanındaki ham veri hiçbir zaman
değiştirilmez ya da silinmez — kümüleme ve mismatch tespiti her seferinde
ham veri üzerinden yeniden hesaplanan, veriyi bozmayan bir görüntüleme
katmanıdır.
