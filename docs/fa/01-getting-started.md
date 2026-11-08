<div dir="rtl">

# ۱. نصب و اجرا

FanDrop نصب نداره. یه فایل `FanDrop.exe` حدود ۱۱۰ کیلوبایتیه که روی هر ویندوز ۱۰ و ۱۱ اجرا می‌شه (از .NET Framework 4.8 که خود ویندوز داره استفاده می‌کنه).

## قدم ۱: دانلود

1. برو به صفحه‌ی [Releases](https://github.com/sinasohrabi9444/FanDrop-Windows/releases/latest).
2. از بخش **Assets** فایل `FanDrop.exe` (یا `FanDrop-Windows.zip`) رو دانلود کن.
3. یه جای ثابت بذارش، مثلاً `C:\Tools\FanDrop\FanDrop.exe`.

> [!IMPORTANT]
> اگه بعداً **Start with Windows** رو روشن کنی، مسیر همین فایل توی رجیستری ذخیره می‌شه. پس اول جای نهاییش رو انتخاب کن، بعد اجراش کن. اگه جابه‌جاش کردی، یه بار گزینه رو خاموش و روشن کن.

## قدم ۲: اولین اجرا

روی `FanDrop.exe` دوبار کلیک کن.

- چون فایل امضای دیجیتال نداره، ممکنه **Windows SmartScreen** بگه «Windows protected your PC». روی **More info** و بعد **Run anyway** بزن.
- یه نوتیفیکیشن میاد: *FanDrop is running*.
- آیکون بادبزن آبی میاد توی **system tray** (کنار ساعت). اگه نمی‌بینیش، روی فلش `^` کنار ساعت بزن.

<div align="center"><img src="../images/logo.png" width="72" alt="آیکون FanDrop"></div>

## قدم ۳: منوی آیکون

روی آیکون **راست‌کلیک** کن:

| گزینه | کارش |
|---|---|
| **Open output folder** | پوشه‌ی خروجی‌ها (`Downloads\FanDrop`) رو باز می‌کنه |
| **Show log** | فایل لاگ رو باز می‌کنه (برای عیب‌یابی) |
| **Start with Windows** | با روشن شدن ویندوز خودکار اجرا بشه (بی‌صدا، بدون نوتیفیکیشن) |
| **Quit FanDrop** | بستن برنامه |

**دوبار کلیک** روی آیکون هم پوشه‌ی خروجی رو باز می‌کنه.

## قدم ۴ (اختیاری): پیش‌نیازها

برای عکس‌های PNG/JPG/TIFF، PDF، ZIP و همه‌ی Actions **هیچ چیز اضافه‌ای لازم نیست**.
برای ویدیو و صدا باید ffmpeg نصب باشه — یه خط دستوره:

```powershell
winget install Gyan.FFmpeg
```

جزئیات کامل توی [آموزش ۴: پیش‌نیازها](04-requirements.md).

---

✅ آماده‌ای. برو سراغ [آموزش ۲: اولین drag ←](02-using-the-fan.md)

</div>
