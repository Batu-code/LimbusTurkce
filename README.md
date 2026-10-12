# Limbus Company Türkçe Yama

Limbus Company için hayran yapımı Türkçe çeviri ve onu kurup güncel tutan küçük bir Windows uygulaması.
Project Moon ile bağlantısı yoktur.

## Kurulum

1. [Sürümler](https://github.com/Batu-code/LimbusTurkce/releases/latest) sayfasından `LimbusTurkce.exe`'yi indirin.
2. Çalıştırın ve **Kur**'a basın, sonra **Oyna**'ya basın.

Yeni sürüm çıkınca uygulamayı açmanız yeterli. Büyük tuş **Güncelle** der, değişenler yanında yazar.

Windows "Bilinmeyen yayıncı" uyarısı verebilir, çünkü uygulama imzalı değil (imza sertifikası ücretli).
**Ek bilgi** → **Yine de çalıştır** ile açılır. Uygulamanın ne yaptığı aşağıda, kaynak kodu bu depoda.

## Neler çevrildi

- Prolog (Karanlık Orman) ve Canto I (Ait Olamayan) hikâyesinin tamamı
- Ana menü, Günahkârlar ekranı, bölüm ve oda kartları
- Ayrıntılar her sürümün notunda

Giriş ekranı İngilizce kalır, oyun onu dil paketinden önce çiziyor.

## Uygulama bilgisayarınızda ne yapar

Uygulama oyunun resmî **özel dil desteğini** kullanır. Oyun dosyalarını değiştirmez, oyuna ek program sokmaz.

| Ne | Nerede |
|---|---|
| Oyunu bulur | Steam'in kendi kayıt defteri girdisi ve `libraryfolders.vdf` |
| Oyunun İngilizce metnini **okur** | `LimbusCompany_Data\Assets\Resources_moved\Localize\en` |
| Dil paketini **yazar** | `LimbusCompany_Data\Lang\Turkish` |
| Oyun dilini Türkçe yapar (kutu işaretliyse) | `LimbusCompany_Data\Lang\config.json` içindeki `lang` değeri |
| Ayarlarını tutar | `%APPDATA%\LimbusTurkce` |
| İndirdiği son paketi tutar | `%LOCALAPPDATA%\LimbusTurkce\paketler` |
| İnternete bağlanır | Sadece GitHub: bu deponun sürüm listesi ve sürüm dosyaları |

**Kaldır** tuşu `Lang\Turkish` klasörünü ve Türkçeyi gösteren `config.json`'u siler, oyun İngilizceye döner.

Neden oyunun İngilizcesini kopyalıyor: oyun özel dili ancak eksiksiz bir set olarak okuyor. Çevrilmemiş
dosyalar için İngilizce gerekiyor. Project Moon'un metnini dağıtmamak için onu sizin oyununuzdan alıyoruz.
Font da aynı sebeple şimdilik Windows'un Segoe UI fontundan kopyalanıyor.

## Kaynaktan derleme

.NET SDK (6 ya da üstü) yeterli, uygulama Windows'ta hazır gelen .NET Framework 4.8 üzerinde çalışır.

```
dotnet build -c Release
```

Çıktı: `bin\Release\net48\LimbusTurkce.exe`

## Lisans

- **Uygulamanın kaynak kodu:** MIT, `LICENSE` dosyasında.
- **Çeviriler** (sürümlerdeki `turkce-yama.zip`): bu projenin emeğidir. İzin almadan başka yerde
  yayımlanamaz, değiştirilip yeniden dağıtılamaz, para karşılığı verilemez. Paylaşmak isterseniz bu sayfanın
  bağlantısını paylaşın.
- **Limbus Company** ve oyunun bütün içeriği Project Moon'a aittir. Bu proje Project Moon'un
  [hayran yapımı kurallarına](https://x.com/ProjMoonStudio/status/1629085462236397573) uyar ve onlar tarafından onaylanmış değildir.
