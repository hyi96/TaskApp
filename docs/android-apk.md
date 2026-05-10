# Android APK

`TaskApp.Android` is the first Android slice. It is a small native Android bootstrap client that proves login and profile download before the full desktop UI is ported.

The app defaults to `https://taskapp-api.hyi96.dev`. It does not embed secrets. First create/login/upload from desktop, then enter the same account ID and account secret in Android. `List profiles` fills the first profile ID when the field is empty, and `Download profile` confirms the phone can read the desktop-uploaded snapshot.

This matches the current cloud model: one account contains one or more TaskApp profiles. Desktop user switching remains profile switching under the same account.

## Setup

Install the Android workload:

```powershell
dotnet workload restore TaskApp.Android/TaskApp.Android.csproj
```

Install Android SDK dependencies. Pass an explicit JDK path if Java is not discoverable from `JAVA_HOME`.

```powershell
dotnet build TaskApp.Android/TaskApp.Android.csproj `
  -t:InstallAndroidDependencies `
  -f net10.0-android `
  -p:AndroidSdkDirectory="$env:LOCALAPPDATA\Android\Sdk" `
  -p:JavaSdkDirectory="C:\Users\hao\.codex\android-build-tools\jdk-17.0.19+10" `
  -p:AcceptAndroidSDKLicenses=True
```

On this development machine, the JDK is installed at:

```text
C:\Users\hao\.codex\android-build-tools\jdk-17.0.19+10
```

## Build

Publish all release builds, including a side-loadable APK:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-all.ps1 -Configuration Release
```

The signed APK is copied to:

```text
dist\Android\dev.hyi96.taskapp-Signed.apk
```

For an Android-only publish:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build-all.ps1 -Configuration Release -SkipDesktop
```
