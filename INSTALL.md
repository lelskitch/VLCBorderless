# Install guide

## Requirements

- **Windows 10 or 11, 64-bit.** Developed and tested on Windows 11.
- **VLC 3.0.x, 64-bit**, the desktop version from [videolan.org](https://www.videolan.org/vlc/) (tested with 3.0.23). VLC 4 and the Microsoft Store version of VLC are untested - the Store version keeps its settings somewhere else, so the extension folder below may not be the one it reads.
- **Windows PowerShell 5.1**
- Nothing else. Building from source uses `csc.exe`, the C# compiler that ships inside Windows' own .NET Framework 4.x folder.

The install is **per user** no admin rights and changes no registry keys.

---

## Option 1 - the release zip (no build needed)

1. Download `vlc-borderless-(ver-no.).zip` from the [Releases](../../releases) page.
2. Extract it anywhere.
3. Double-click **`install.cmd`**.
   The script stops a running helper if there is one, copies two files, and prints where it put them. 
4. **Restart VLC** if you already have it open. It should rescan extensions upon startup.

## Option 2 - from source (git clone)

```powershell
git clone https://github.com/lelskitch/vlc-borderless.git
cd vlc-borderless
.\install.ps1           # builds dist\VlcBorderless.exe first (build.ps1) when it is missing, then installs
```

If PowerShell refuses to run scripts ("running scripts is disabled on this system"), use `powershell -ExecutionPolicy Bypass -File .\install.ps1`, or double-click `install.cmd`, which does that for one run only.

To only build: `.\build.ps1`  ->  `dist\VlcBorderless.exe`.

## Option 3 - by hand

Copy two files, creating the folders if they do not exist:

| From | To |
|---|---|
| `extension\borderless.lua` | `%APPDATA%\vlc\lua\extensions\borderless.lua` |
| `dist\VlcBorderless.exe` | `%APPDATA%\vlc\borderless\VlcBorderless.exe` |

(`%APPDATA%` is usually `C:\Users\<yourusername>\AppData\Roaming`.) The extension looks for the helper in `%APPDATA%\vlc\borderless\` first, then in `C:\Program Files\VideoLAN\VLC\borderless\`.

## Updating

Run the installer again from the new version. It stops the running helper (putting the title bar back), overwrites the two files, and prints the result. Restart VLC afterwards so it reloads the extension.

## Uninstalling

Double-click **`uninstall.cmd`** (or run `.\uninstall.ps1`). It puts the title bar back, stops the helper, and deletes `borderless.lua` and the `borderless` folder from `%APPDATA%\vlc` (and from Program Files if you used `-System` - that part needs an administrator PowerShell).

Optionally delete `%LOCALAPPDATA%\VlcBorderless` too: it only holds the last window position, an optional `options.txt` / `helper.log`, and a backup of VLC's `vlc-qt-interface.ini` (see the [README](README.md##How-it-interacts-with-your-system)).

## Troubleshooting

### "Borderless Window" is not in the View menu
- Restart VLC completely (check Task Manager for a leftover `vlc.exe`).
- Check the file is at `%APPDATA%\vlc\lua\extensions\borderless.lua`.
- In VLC: **Tools > Plugins and extensions > Active Extensions** lists installed extensions; "Borderless Window" should be there.
- A portable VLC keeps its data next to `vlc.exe` instead of in `%APPDATA%\vlc`: install into that folder's `lua\extensions` and `borderless` folders by hand (Option 3).

### Ticking does nothing, or a dialog says the helper was not found
- Check `%APPDATA%\vlc\borderless\VlcBorderless.exe` exists (run the installer again).
- Start the helper by hand to see whether Windows or your antivirus blocks it: `& "$env:APPDATA\vlc\borderless\VlcBorderless.exe"` - a tray icon should appear within a second or two (it exits again after 5 s if no VLC is open).
- VLC's own log shows what the extension did. Run VLC with logging on (from a Command Prompt; `%TEMP%` is not expanded in PowerShell): `"C:\Program Files\VideoLAN\VLC\vlc.exe" --verbose=2 --file-logging --logfile=%TEMP%\vlc.log --logmode=text`, tick the option, and look for lines starting `[borderless]`. (**Tools > Messages** with verbosity 2 shows the same live.)

### It will not untick / the title bar does not come back
- Normally unticking sends `--stop` to the helper, which puts the title bar back. VLC's log (see above) should show `[borderless] deactivate`. If it instead shows `Error while running script ...borderless.lua` or `Could not activate extension!`, the extension itself failed to start - VLC then treats every click as "tick" and never unticks. That is a bug: please open an issue with those log lines.
- To get the title bar back right now, right-click the helper's **tray icon > Put the title bar back and exit**, or run `"%APPDATA%\vlc\borderless\VlcBorderless.exe" --stop`.

### VLC opens fullscreen, with no title bar
VLC saves "fullscreen" when it is closed in fullscreen and reopens that way. Press **Esc** or **F**. The helper clears that saved flag when the VLC it handled closes; if VLC is already stuck, close VLC and run `"%APPDATA%\vlc\borderless\VlcBorderless.exe" --fix-vlc-start` once.

### Black bars stay, or resizing does not keep the aspect ratio
- A click (or a drag) on any window edge snaps the window to the picture's shape.
- Turn on the helper's log: create `%LOCALAPPDATA%\VlcBorderless\options.txt` containing `--log`, untick and tick again, then read `%LOCALAPPDATA%\VlcBorderless\helper.log` (delete `options.txt` to turn logging off again).
- Please open an issue with the log, your Windows and VLC versions, and whether you use Minimal Interface.

### Something else
Open an issue and include: Windows version, VLC version (Help > About), 32- or 64-bit VLC, Minimal Interface or normal, and the helper log from above.
Cheers.
