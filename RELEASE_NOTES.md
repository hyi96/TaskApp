# First Release ??

Thank you for trying TaskApp! Below are platform-specific instructions to help you get started.

---

## Windows Users

### 1. Choose the right architecture
- **64-bit (most common):** Download `Windows-win-x64.zip`
- **32-bit:** Download `Windows-win-x86.zip`

> *Not sure? Go to **Settings > System > About** and check "System type".*

### 2. Bypass SmartScreen
Because this app is not signed by a Microsoft-recognized certificate, Windows may show a blue **"Windows protected your PC"** banner.

1. Click **More info**.
2. Click **Run anyway**.

> *This only appears the first time you run the application.*

### 3. Antivirus False Positives
Some antivirus software (like Bitdefender or Norton) may flag new GitHub releases as "suspicious" simply because they haven't seen the file before. If the file disappears after downloading, check your antivirus **Quarantine** or **Protection History** to restore it.

---

## macOS Users

### 1. Choose the right architecture
- **Intel Macs:** Download `macOS-osx-x64.zip`
- **Apple Silicon (M1/M2/M3):** Download `macOS-osx-arm64.zip`

> *Not sure? Go to **Apple Menu > About This Mac** and check Chip/Processor.*

### 2. Set execute permissions
If the app doesn't open immediately, you may need to make it executable via Terminal:

1. Unzip the downloaded file.
2. Open the **Terminal** app.
3. Type `chmod +x ` (with a **space** at the end).
4. **Drag and drop** the `TaskApp` file from Finder into the Terminal window.
5. Press **Enter**.

### 3. Bypass Gatekeeper (Unsigned App Warning)
If you see: *"TaskApp" can't be opened because Apple cannot check it for malicious software.*

1. **Do not** move to Bin.
2. **Control+Click** (or Right-Click) the app icon and select **Open**.
3. Click **Open** again in the confirmation dialog.

> *You only need to do this once. Subsequent launches will work normally.*

### Troubleshooting: "App is Damaged"
If you see a message stating the app is **"damaged and should be moved to the Trash,"** it's a quarantine issue. Run this command in Terminal:

```
xattr -cr /path/to/TaskApp
```

> *Tip: Drag and drop the file into Terminal to auto-fill the path.*

---

## Linux Users

### 1. Choose the right architecture
- **64-bit Intel/AMD (most common):** Download `Linux-linux-x64.zip`
- **ARM64 (Raspberry Pi 4, etc.):** Download `Linux-linux-arm64.zip`
- **ARM32 (older Raspberry Pi):** Download `Linux-linux-arm.zip`
- **Alpine/musl-based distros:** Download `Linux-linux-musl-x64.zip`

> *Not sure? Run `uname -m` in terminal. `x86_64` = linux-x64, `aarch64` = linux-arm64.*

### 2. Set execute permissions
After extracting, make the app executable:

```
chmod +x ./TaskApp
```

### 3. Run the app

```
./TaskApp
```

### Troubleshooting: Missing dependencies
If you encounter errors about missing libraries, install the required dependencies:

**Debian/Ubuntu:**
```
sudo apt install libx11-6 libice6 libsm6 libfontconfig1
```

**Fedora/RHEL:**
```
sudo dnf install libX11 libICE libSM fontconfig
```

**Arch Linux:**
```
sudo pacman -S libx11 libice libsm fontconfig
```

### Troubleshooting: GPU/Rendering issues
If the app crashes or shows graphical glitches, try running with software rendering:

```
LIBGL_ALWAYS_SOFTWARE=1 ./TaskApp
```

---

## Need Help?

If you encounter any issues not covered here, please [open an issue](https://github.com/hyi96/TaskApp/issues) on GitHub.
