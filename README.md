# Limbus Company Türkçe Yama

Limbus Company için hayran yapımı Türkçe çeviri ve onu kurup güncel tutan küçük bir Windows uygulaması.
Project Moon ile bağlantısı yoktur.

## Kurulum

1. [Sürümler](https://github.com/Batu-code/LimbusTurkce/releases/latest) sayfasından `LimbusTurkce.exe`'yi indirin.
2. Çalıştırın ve kırmızı bilete basın. Önce **Kur** der, kurulunca **Oyna** der.

Yeni sürüm çıkınca uygulamayı açmanız yeterli. Bilet **Güncelle** der, değişenler soldaki klasörde yazar.

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
| Kurunca oyun dilini Türkçe yapar | `LimbusCompany_Data\Lang\config.json` içindeki `lang` değeri |
| Ayarlarını tutar | `%APPDATA%\LimbusTurkce` |
| İndirdiği son paketi tutar | `%LOCALAPPDATA%\LimbusTurkce\paketler` |
| İnternete bağlanır | Sadece GitHub: bu deponun sürüm listesi ve sürüm dosyaları |

**Oyun dili** düğmesi yamayı kaldırmadan dili değiştirir. İngilizce seçilince sadece Türkçeyi gösteren
`config.json` silinir, `Lang\Turkish` yerinde kalır ve Türkçe tek tıkla geri açılır.

Sağ üstteki dişliyle açılan **Ayarlar** sayfasındaki **Kaldır** tuşu `Lang\Turkish` klasörünü ve Türkçeyi
gösteren `config.json`'u siler, oyun İngilizceye döner.

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
- **Arayüz görselleri** (`art/` klasörü): Limbus Company'den alınmıştır ve Project Moon'a aittir.
  MIT kapsamında değildir, ayrıntısı `art/NOTICE.md` içinde.
- **Çeviriler** (sürümlerdeki `turkce-yama.zip`): bu projenin emeğidir. İzin almadan başka yerde
  yayımlanamaz, değiştirilip yeniden dağıtılamaz, para karşılığı verilemez. Paylaşmak isterseniz bu sayfanın
  bağlantısını paylaşın.
- **Limbus Company** ve oyunun bütün içeriği Project Moon'a aittir. Bu proje Project Moon'un
  [hayran yapımı kurallarına](https://x.com/ProjMoonStudio/status/1629085462236397573) uyar ve onlar tarafından onaylanmış değildir.
