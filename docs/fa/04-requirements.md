<div dir="rtl">

# ۴. نصب پیش‌نیازها

خود FanDrop به هیچ چیز نیاز نداره. فقط بعضی تیغه‌ها از ابزارهای بیرونی استفاده می‌کنن:

| ابزار | برای | لازمه؟ |
|---|---|---|
| **ffmpeg** | همه‌ی ویدیو و صداها، WEBP | برای ویدیو/صدا آره |
| **HEIF + HEVC Extensions** | خوندن و ساختن HEIC (عکس‌های آیفون) | فقط برای HEIC |
| **Word یا LibreOffice** | تبدیل سند با حفظ فرمت‌بندی | اختیاری |
| **exiftool** | Strip metadata برای PDF | فقط همین یه تیغه |

---

<a id="ffmpeg"></a>

## 🎬 ffmpeg

### روش ۱: winget (پیشنهادی)

PowerShell یا Terminal رو باز کن:

```powershell
winget install Gyan.FFmpeg
```

FanDrop رو یه بار ببند و دوباره باز کن (راست‌کلیک روی آیکون ← Quit، بعد اجرای دوباره).

### روش ۲: کنار FanDrop.exe

1. از [gyan.dev/ffmpeg/builds](https://www.gyan.dev/ffmpeg/builds/) نسخه‌ی `ffmpeg-release-essentials.zip` رو دانلود کن.
2. از پوشه‌ی `bin` داخلش، فایل `ffmpeg.exe` رو کپی کن **کنار** `FanDrop.exe`.

```
C:\Tools\FanDrop\
├── FanDrop.exe
└── ffmpeg.exe
```

این روش «پرتابل» هم هست؛ کل پوشه رو می‌تونی روی فلش ببری.

### روش ۳: Scoop یا Chocolatey

```powershell
scoop install ffmpeg
# یا
choco install ffmpeg
```

### FanDrop کجاها دنبال ffmpeg می‌گرده؟

1. کنار `FanDrop.exe` (یا توی زیرپوشه‌ی `tools\` کنارش)
2. `PATH` سیستم
3. `C:\ffmpeg\bin\`
4. `C:\Program Files\ffmpeg\bin\`
5. Scoop: `%USERPROFILE%\scoop\shims\`
6. Chocolatey: `C:\ProgramData\chocolatey\bin\`
7. WinGet: `%LOCALAPPDATA%\Microsoft\WinGet\Links\` و پوشه‌ی Packages

**تست:** یه ویدیو رو بکش، <kbd>Shift</kbd> ← **GIF**. اگه نوتیفیکیشن «ffmpeg not found» اومد، [عیب‌یابی](06-troubleshooting.md#ffmpeg-not-found) رو ببین.

---

<a id="heic"></a>

## 📱 HEIC (عکس‌های آیفون)

ویندوز برای HEIC دو تا افزونه از Microsoft Store لازم داره:

1. **HEIF Image Extensions** — رایگان
2. **HEVC Video Extensions** — روی بعضی سیستم‌ها از قبل نصبه؛ وگرنه از Store

روی هر کدوم توی Microsoft Store جستجو کن و **Get / Install** بزن.

> اگه کدک نصب نباشه، FanDrop برای **خوندن** HEIC سراغ ffmpeg می‌ره، پس با ffmpeg هم می‌تونی HEIC رو به JPG تبدیل کنی. ولی **ساختن** HEIC حتماً کدک ویندوز رو می‌خواد.

---

## 📝 Word یا LibreOffice (اختیاری)

اگه یکی‌شون نصب باشه، FanDrop خودش پیداش می‌کنه:

- **Microsoft Word** — هر نسخه‌ی دسکتاپ
- **LibreOffice** — رایگان: `winget install TheDocumentFoundation.LibreOffice`

بدون این‌ها هم تبدیل سند کار می‌کنه، فقط bold، italic، جدول و عکس‌ها حذف می‌شن و متن خالص می‌مونه.

می‌خوای حتی با وجود Word از تبدیل داخلی استفاده بشه؟ متغیر محیطی `FANDROP_NO_OFFICE=1` رو ست کن ([آموزش ۷](07-build-and-develop.md#env)).

---

## 🧹 exiftool (فقط برای PDF)

```powershell
winget install OliverBetz.ExifTool
```

یا `exiftool.exe` رو از [exiftool.org](https://exiftool.org) بگیر و بذار کنار `FanDrop.exe` یا توی `PATH`.

---

[→ آموزش ۳](03-reference.md) · [فهرست](README.md) · [آموزش ۵: سناریوها ←](05-recipes.md)

</div>
