# Android APK

`TaskApp.Android` is the native Android client. Use it to open a cloud profile, complete dailies and todos, update habit counters, and claim rewards. Create and edit tasks and rewards in the desktop app.

## Sign In and Switch Profiles

1. In the desktop app, open the Settings gear, select **Cloud Sync**, and upload a profile as described in the [desktop workflow](cloud-api.md#desktop-workflow).
2. Install the signed APK and open TaskApp on Android.
3. Enter the API URL, account ID, and account secret from desktop. The default URL is `https://taskapp-api.hyi96.dev`. Use an HTTPS address the phone can reach for a self-hosted API.
4. Tap **Login**. The app lists profiles under that account and downloads the selected profile.
5. Use the **Profile** picker to switch profiles. **Refresh** refreshes the picker and reloads the selected profile. **Download** reloads the current profile. **Upload** sends the current local snapshot.

The app caches downloaded snapshots locally. When you complete a task, update a habit, or claim a reward, it saves the change locally and attempts an upload. If the upload fails, use **Upload** to send the saved change before refreshing or downloading. Uploads replace the entire profile snapshot, so bring the latest snapshot onto a device before editing the same profile there.

## Setup

Install the Android workload:

```powershell
dotnet workload restore TaskApp.Android/TaskApp.Android.csproj
```

Set `JAVA_HOME` to your JDK directory (JDK 17 works for this project), then install the Android SDK dependencies:

```powershell
dotnet build TaskApp.Android/TaskApp.Android.csproj `
  -t:InstallAndroidDependencies `
  -f net10.0-android `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
  -p:JavaSdkDirectory="$env:JAVA_HOME" `
  -p:AcceptAndroidSDKLicenses=True
```

## Build

Publish all release builds, including a sideloadable APK. The script replaces `dist` on every run, so close any app running from that directory before building.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-all.ps1 -Configuration Release
```

The signed APK is copied to:

```text
dist\Android\dev.hyi96.taskapp-Signed.apk
```

For an Android-only publish, which recreates `dist` with only the Android output:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-all.ps1 -Configuration Release -SkipDesktop
```
