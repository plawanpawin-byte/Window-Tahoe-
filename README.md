# MacLookLike

macOS-style **liquid-glass desktop widgets for Windows**.

Translucent widgets sit on your desktop, let the wallpaper show through, and slip underneath your app windows when you open them, just like on a Mac. Everything ships as a single `MacLookLike.exe`: no installer, no admin rights.

![Desktop widgets](docs/desktop.png)

## Install

1. Open the **[Releases](../../releases)** page.
2. Download **`MacLookLike.exe`** from the latest release.
3. Put it anywhere you like (for example `Documents\MacLookLike\`) and double-click it.
4. The Settings window opens and your widgets appear on the desktop.

To uninstall, quit the app (**Settings → General → Quit MacLookLike**) and delete the exe. Optionally delete the settings folder `%APPDATA%\MacLookLike`.

### "Windows protected your PC"

The exe is not code-signed, so Windows SmartScreen may warn you the first time. Click **More info → Run anyway**. Some antivirus programs may also flag small unsigned apps by mistake. The full source code is in this repository if you want to check it.

### Requirements

- Windows 10 or Windows 11
- .NET Framework 4.8, which is already included in current Windows 10 and 11
- Transparency effects work best with Windows' own transparency switched on (Settings → Personalization → Colors → Transparency effects)

## Widgets

| Widget | Styles | What it shows |
| --- | --- | --- |
| **Clock** | Digital, Analog | Current time and date |
| **Calendar** | Date, Month | Today's date, or a full month with today highlighted |
| **Weather** | Details, Forecast | Temperature, conditions, daily high/low and a forecast |
| **Battery** | Rings, Bars | Your laptop battery plus connected Bluetooth devices that report a battery level |
| **Note** | Yellow, plain | A sticky note you can type into |

Every widget comes in **Small, Medium and Large** sizes, and the layout changes with the size.

![Settings](docs/settings.png)

## Using it

- **Open Settings:** right-click any widget → **MacLookLike Settings…**, or just run `MacLookLike.exe` again. Settings also opens automatically every time you start the app.
- **Move a widget:** click and drag it anywhere on the desktop.
- **Change style or size:** right-click the widget → **Style** or **Size**.
- **Remove a widget:** right-click it → **Remove Widget**.
- **Add a widget:** Settings → **Desktop Widgets**, then click the widget style and size you want.
- **Widgets live on the desktop level.** They stay under your app windows. To see them, show the desktop (**Win + D**) or minimize your windows.

## Settings

**General**
- Launch at login: start MacLookLike when you sign in to Windows
- 24-hour time
- Temperature in °F (default is °C)
- Weather location: detect automatically, or type a city and press **Search**
- Quit MacLookLike

**Appearance**
- Tint: **Dark**, **Light** or **Clear** glass
- Tint strength: how opaque the glass is

**Menu Bar**
- **Show menu bar:** an optional slim glass bar across the top of the screen. Maximized windows cover it. Right-click it to hide it again.

**Desktop Widgets**
- Add any widget in any style and size

The top of the Settings sidebar shows your Windows account name and picture, as the macOS System Settings does. It is only shown locally and never sent anywhere.

## Privacy

- MacLookLike has **no telemetry, no account and no ads**.
- The only network requests are for the weather:
  - **Automatic location** sends a request to `ipwho.is` to estimate your city from your IP address. Turn it off in Settings → General and type your city instead.
  - **Weather data** comes from [Open-Meteo](https://open-meteo.com), using your city's coordinates. Searching for a city also uses Open-Meteo's geocoding service.
- Your settings are stored locally in `%APPDATA%\MacLookLike\config.json`. Errors, if any, are written to `error.log` in the same folder.

## Troubleshooting

- **I can't see my widgets.** They sit under app windows. Press **Win + D** or minimize your windows. If you still don't see them, run `MacLookLike.exe` again to open Settings, then re-add them in **Desktop Widgets**.
- **The glass looks flat or not transparent.** Turn on **Transparency effects** in Windows personalization settings, and try Tint → **Clear** in Settings → Appearance.
- **Weather is wrong or missing.** Turn off automatic location and type your city, then press **Search**. Check your internet connection.
- **The Battery widget doesn't show my Bluetooth device.** Only devices that report a battery level to Windows are listed. Make sure the device is connected.
- **Something crashed.** Check `%APPDATA%\MacLookLike\error.log` and open an [issue](../../issues) with its contents.
- **Reset everything.** Quit the app, delete the folder `%APPDATA%\MacLookLike`, and start it again.

## Feedback

Found a bug or have an idea? Open an [issue](../../issues). Please include your Windows version, your screen resolution, and the contents of `error.log` if there is one. Pull requests are welcome.

## License

[MIT](LICENSE)
