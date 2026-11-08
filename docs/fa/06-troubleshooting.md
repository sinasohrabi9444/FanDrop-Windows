<div dir="rtl">

# ۶. عیب‌یابی

قدم اول برای هر مشکلی: **راست‌کلیک روی آیکون ← Show log**.
فایل لاگ اینجاست: `%TEMP%\fandrop.log`

---

## بادبزن باز نمی‌شه

| علت | راه‌حل |
|---|---|
| کلید رو **قبل** از drag نگه داشتی | اول drag رو شروع کن، بعد کلید رو بزن. یا کلید رو ول کن و دوباره بزن. |
| FanDrop اجرا نیست | آیکون بادبزن توی tray هست؟ (فلش `^` کنار ساعت رو هم چک کن) |
| از پنجره‌ای drag می‌کنی که **Run as administrator** اجرا شده | ویندوز اجازه نمی‌ده برنامه‌ی معمولی، drag برنامه‌ی ادمین رو بگیره. FanDrop رو هم به‌صورت ادمین اجرا کن، یا از Explorer معمولی drag کن. |
| چیزی که می‌کشی فایل نیست (مثلاً متن انتخاب‌شده) | بادبزن فقط برای فایل‌ها باز می‌شه. |
| hook موس نصب نشده | توی لاگ دنبال `mouse hook failed` بگرد؛ FanDrop رو ببند و دوباره باز کن. |

<a id="ffmpeg-not-found"></a>

## «ffmpeg not found»

1. توی PowerShell بزن: `ffmpeg -version`. اگه کار نکرد، ffmpeg نصب نیست ← [آموزش ۴](04-requirements.md#ffmpeg).
2. اگه **همین الان** با winget نصب کردی، FanDrop رو ببند و دوباره باز کن (PATH جدید رو باید بخونه).
3. مطمئن‌ترین راه: `ffmpeg.exe` رو بذار کنار `FanDrop.exe`.

## «HEIC encoding failed»

افزونه‌های **HEIF Image Extensions** و **HEVC Video Extensions** رو از Microsoft Store نصب کن ([آموزش ۴](04-requirements.md#heic)).

## «doesn't apply to this file»

چند فایل از نوع‌های مختلف رو با هم کشیدی. تیغه‌ها بر اساس اولین فایل انتخاب می‌شن؛ فایل‌هایی که اون تیغه براشون معنی نداره رد می‌شن. نوع‌ها رو جدا بکش.

## «PDF strip metadata (needs exiftool)»

`exiftool` رو نصب کن: `winget install OliverBetz.ExifTool`.

## تبدیل سند فرمت‌بندی رو از دست داد

Word یا LibreOffice نصب نیست (یا `FANDROP_NO_OFFICE` ست شده). تبدیل داخلی فقط متن و پاراگراف‌ها رو نگه می‌داره. LibreOffice رایگانه: `winget install TheDocumentFoundation.LibreOffice`.

## SmartScreen اجازه‌ی اجرا نمی‌ده

فایل امضای دیجیتال نداره. **More info ← Run anyway**. اگه به فایل دانلودی شک داری، از سورس بسازش ([آموزش ۷](07-build-and-develop.md)) یا هش SHA-256‌ش رو با Release مقایسه کن.

## آنتی‌ویروس هشدار می‌ده

FanDrop برای فهمیدن drag از **low-level mouse hook** استفاده می‌کنه (`SetWindowsHookEx(WH_MOUSE_LL)`). بعضی آنتی‌ویروس‌ها این رو مشکوک می‌دونن. FanDrop هیچ کلیدی رو ثبت نمی‌کنه و فقط وضعیت کلیک چپ و سه کلید Shift/Ctrl/Alt رو می‌خونه — کل کد توی [`src/DragWatcher.cs`](../../src/DragWatcher.cs) هست و می‌تونی خودت ببینی و بسازیش.

## «FanDrop is already running»

یه نسخه از قبل اجراست. توی tray دنبال آیکونش بگرد.

## Start with Windows کار نمی‌کنه

بعد از روشن کردنش فایل `FanDrop.exe` رو جابه‌جا کردی. گزینه رو یه بار خاموش و دوباره روشن کن تا مسیر جدید ثبت بشه.
(مقدارش توی رجیستری: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` ← `FanDrop`)

## لاگ مفصل‌تر

```powershell
$env:FANDROP_DEBUG = "1"
& "C:\Tools\FanDrop\FanDrop.exe"
```

حالا هر drag، کلید و تصمیم بادبزن توی لاگ ثبت می‌شه.

---

مشکلت حل نشد؟ یه [Issue](https://github.com/sinasohrabi9444/FanDrop-Windows/issues/new/choose) باز کن و بخش‌های مربوط لاگ رو بذار.

[→ آموزش ۵](05-recipes.md) · [فهرست](README.md) · [آموزش ۷: ساخت از سورس ←](07-build-and-develop.md)

</div>
