<div dir="rtl">

# ۷. ساخت از سورس و توسعه

FanDrop بدون Visual Studio، بدون .NET SDK و بدون NuGet ساخته می‌شه. فقط ویندوز.

## ساخت

```bat
git clone https://github.com/sinasohrabi9444/FanDrop-Windows.git
cd FanDrop-Windows
build.cmd
```

خروجی: `FanDrop.exe` توی ریشه‌ی پروژه.

### `build.cmd` چی‌کار می‌کنه؟

1. از کامپایلر C# که داخل خود ویندوزه استفاده می‌کنه:
   `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`
2. به WPF، WinForms و متادیتای WinRT (`System32\WinMetadata\*.winmd`) رفرنس می‌ده — برای HEIC و رندر PDF.
3. **دو بار** کامپایل می‌کنه:
   - بار اول یه exe موقت می‌سازه که با `--make-icon` آیکون برنامه رو **با کد** می‌کشه (`build\FanDrop.ico`)؛
   - بار دوم exe نهایی رو با همون آیکون embed‌شده می‌سازه.

> هر بار که روی گیت‌هاب push بشه، GitHub Actions همین `build.cmd` رو روی Windows اجرا می‌کنه. با push کردن یه تگ مثل `v1.2.0`، خودکار یه Release با `FanDrop.exe` ساخته می‌شه.

---

## ساختار کد

```
src/
├── App.cs          نقطه‌ی شروع، آیکون tray، نوتیفیکیشن، اجرای خودکار، لاگ
├── DragWatcher.cs  hook سطح پایین موس: تشخیص drag و Shift/Ctrl/Alt
├── FanWindow.cs    پنجره‌ی شفاف بادبزن (WPF) و انیمیشن‌هاش
├── DropData.cs     خوندن فایل‌های واقعی و مجازی از drop
├── Model.cs        نوع فایل‌ها، حالت‌ها و تیغه‌های هر کدوم
├── Actions.cs      اجرای Convert / Tools / Actions و نام‌گذاری خروجی
├── Media.cs        پردازش تصویر (WIC) و اجرای ffmpeg
├── Docs.cs         استخراج متن و تبدیل سند (Word / LibreOffice / داخلی)
├── WinRtImaging.cs ساخت HEIC با WinRT
├── WinRtPdf.cs     رندر صفحه‌های PDF با WinRT
├── QuickLook.cs    پنجره‌ی پیش‌نمایش
├── IconArt.cs      آیکون برنامه و tray، کشیده‌شده با کد
└── Native.cs       P/Invoke های Win32
```

### جریان کار

```
کلیک چپ + حرکت > ۶px          (DragWatcher — روی thread جدا)
        │
        ▼
فشردن Shift / Ctrl / Alt       (کلیدی که از قبل نگه داشته شده بود رد می‌شه)
        │
        ▼
FanWindow زیر نشانگر ظاهر می‌شه  (اول نامرئی؛ وقتی drag فایل‌دار واردش شد، بادبزن باز می‌شه)
        │
        ▼
Drop روی تیغه  ──► DropData فایل‌ها رو درمیاره ──► Actions.Run(slot, mode, files)
                                                         │
                                  ┌──────────────────────┼─────────────────────┐
                                  ▼                      ▼                     ▼
                              Convert                 Tools                Actions
                         (thread پس‌زمینه)        (thread پس‌زمینه)       (روی UI thread)
                                  └──────────► نوتیفیکیشن ◄───────────────────┘
```

---

## ابزارهای توسعه

### اجرای یه اکشن بدون UI

```bat
FanDrop.exe --run <Convert|Tools|Actions> <تیغه> <فایل‌ها...>
```

```bat
FanDrop.exe --run Convert PNG "C:\pics\photo.heic"
FanDrop.exe --run Tools "Resize 1280" "C:\pics\a.jpg" "C:\pics\b.jpg"
FanDrop.exe --run Tools ZIP "C:\project"
```

### گرفتن عکس از بادبزن

```bat
FanDrop.exe --snapshot <خروجی.png> <Convert|Tools|Actions> <فایل> [شماره‌ی تیغه‌ی فعال]
```

```bat
FanDrop.exe --snapshot fan.png Tools "C:\videos\clip.mp4" 0
```

همه‌ی عکس‌های این مستندات با همین دستور ساخته شدن. فایل لازم نیست واقعاً محتوا داشته باشه؛ فقط پسوندش مهمه.

<a id="env"></a>

### متغیرهای محیطی

| متغیر | اثر |
|---|---|
| `FANDROP_OUT` | پوشه‌ی خروجی (پیش‌فرض: `Downloads\FanDrop`) |
| `FANDROP_DEBUG=1` | لاگ مفصل |
| `FANDROP_NO_OFFICE=1` | برای سندها از Word / LibreOffice استفاده نشه |

ست کردن دائمی (PowerShell):

```powershell
[Environment]::SetEnvironmentVariable("FANDROP_OUT", "D:\Converted", "User")
```

بعدش FanDrop رو ببند و دوباره باز کن.

---

## 🧩 اضافه کردن یه تیغه‌ی جدید

مثال: یه تیغه‌ی **Flip** برای عکس‌ها توی حالت Tools.

**۱. تیغه رو به لیست اضافه کن** — [`src/Model.cs`](../../src/Model.cs) ← `Slots.For`:

```csharp
case FileKind.Image: return new[] { "Resize 1280", "Compress", "Strip metadata", "Rotate 90°", "Grayscale", "Flip" };
```

**۲. یه آیکون بهش بده** — همون فایل ← `Slots.Icon`. گلیف‌ها از فونت *Segoe Fluent Icons* هستن (لیستشون رو توی برنامه‌ی Character Map ویندوز می‌بینی):

```csharp
case "Flip": return "\uE8AB";
```

**۳. کارش رو پیاده کن** — [`src/Actions.cs`](../../src/Actions.cs) ← متد `Tool`، توی بلوک تصویر:

```csharp
case "Flip": dst = Out(f, " (flipped)", same); Images.Save(Images.Flip(Images.Load(f)), dst, 95); break;
```

و توی [`src/Media.cs`](../../src/Media.cs) کنار `Rotate90`:

```csharp
public static BitmapSource Flip(BitmapSource s)
{
    var r = new TransformedBitmap(s, new ScaleTransform(-1, 1));
    r.Freeze();
    return r;
}
```

**۴. بساز و تست کن:**

```bat
build.cmd
FanDrop.exe --snapshot test.png Tools photo.jpg 5
FanDrop.exe --run Tools Flip photo.jpg
```

بادبزن خودش تعداد تیغه‌ها رو تقسیم می‌کنه؛ تا حدود ۸ تیغه خوانا می‌مونه.

---

## انتشار نسخه‌ی جدید

```bash
git tag v1.1.0
git push origin v1.1.0
```

Workflow گیت‌هاب exe رو می‌سازه و یه Release با `FanDrop.exe`، `FanDrop-Windows.zip` و فایل `SHA256SUMS.txt` منتشر می‌کنه.

---

[→ آموزش ۶](06-troubleshooting.md) · [فهرست](README.md) · [مشارکت در پروژه](../../CONTRIBUTING.md)

</div>
