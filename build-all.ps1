# build-all.ps1
# Publishes TaskApp desktop clients and the Android APK into dist/.

param (
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$ProjectPath = "TaskApp/TaskApp.csproj",
    [string]$AndroidProjectPath = "TaskApp.Android/TaskApp.Android.csproj",
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA "Android\Sdk"),
    [string]$JavaSdkDirectory = "",
    [switch]$SkipDesktop,
    [switch]$SkipAndroid
)

$ErrorActionPreference = "Stop"

# Define platforms and output folders.
$targets = @{
    "win-x64"        = "Windows"
    "win-x86"        = "Windows"
    "osx-x64"        = "macOS"
    "osx-arm64"      = "macOS"
    "linux-x64"      = "Linux"
    "linux-arm"      = "Linux"
    "linux-arm64"    = "Linux"
    "linux-musl-x64" = "Linux"
}

# Windows builds use the Windows TFM (required by Microsoft.Toolkit.Uwp.Notifications).
# Non-Windows builds use the plain net10.0 TFM so the output is free of Windows SDK references.
$windowsTfm = "net10.0-windows10.0.19041.0"
$crossPlatTfm = "net10.0"

function Resolve-JavaSdkDirectory {
    param ([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return $RequestedPath
    }

    if (-not [string]::IsNullOrWhiteSpace($env:JAVA_HOME)) {
        return $env:JAVA_HOME
    }

    $codexToolsRoot = Join-Path $env:USERPROFILE ".codex\android-build-tools"
    $jdkCandidate = Get-ChildItem -Path $codexToolsRoot -Directory -Filter "jdk-*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1

    if ($jdkCandidate) {
        return $jdkCandidate.FullName
    }

    return ""
}

function Publish-DesktopClients {
    foreach ($rid in $targets.Keys) {
        $platformFolder = $targets[$rid]
        $outputPath = Join-Path -Path "dist/$platformFolder/$rid" -ChildPath ""
        New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

        if ($rid.StartsWith("win")) { $tfm = $windowsTfm } else { $tfm = $crossPlatTfm }

        Write-Host "Publishing desktop for $rid (TFM: $tfm)..."

        dotnet publish $ProjectPath -c $Configuration -r $rid `
            --self-contained true /p:PublishSingleFile=true `
            /p:TargetFramework=$tfm `
            -o $outputPath

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed for $rid."
        }

        $zipName = "$platformFolder-$rid.zip"
        $zipPath = Join-Path -Path "dist/$platformFolder" -ChildPath $zipName

        if (Test-Path $zipPath) { Remove-Item $zipPath }
        Compress-Archive -Path "$outputPath/*" -DestinationPath $zipPath
        Write-Host "Zipped to $zipPath`n"
    }
}

function Publish-AndroidApk {
    if (-not (Test-Path -LiteralPath $AndroidSdkDirectory)) {
        throw "Android SDK not found at '$AndroidSdkDirectory'. Run the setup commands in docs/android-apk.md."
    }

    $resolvedJavaSdkDirectory = Resolve-JavaSdkDirectory -RequestedPath $JavaSdkDirectory
    if ([string]::IsNullOrWhiteSpace($resolvedJavaSdkDirectory) -or -not (Test-Path -LiteralPath (Join-Path $resolvedJavaSdkDirectory "bin\java.exe"))) {
        throw "JDK not found. Set JAVA_HOME or pass -JavaSdkDirectory. See docs/android-apk.md."
    }

    $properties = @(
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory",
        "-p:JavaSdkDirectory=$resolvedJavaSdkDirectory"
    )

    Write-Host "Publishing Android APK..."
    dotnet publish $AndroidProjectPath -c $Configuration -f net10.0-android -p:AndroidPackageFormat=apk @properties

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for Android."
    }

    $artifactRoot = "TaskApp.Android/bin/$Configuration/net10.0-android/publish"
    $apkFiles = Get-ChildItem -Path $artifactRoot -Filter "*.apk" -File -ErrorAction SilentlyContinue
    if ($apkFiles.Count -eq 0) {
        throw "Android publish completed, but no APK was found under $artifactRoot."
    }

    $androidDist = Join-Path "dist" "Android"
    New-Item -ItemType Directory -Path $androidDist -Force | Out-Null

    foreach ($apk in $apkFiles) {
        $destination = Join-Path $androidDist $apk.Name
        Copy-Item -LiteralPath $apk.FullName -Destination $destination -Force
        Write-Host "Copied APK to $destination"
    }

    $signedApk = $apkFiles |
        Where-Object { $_.Name -like "*-Signed.apk" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($signedApk) {
        Write-Host "Signed APK: $(Join-Path $androidDist $signedApk.Name)`n"
    }
}

if (Test-Path dist) { Remove-Item -Recurse -Force dist }
New-Item -ItemType Directory -Path dist | Out-Null

if (-not $SkipDesktop) {
    Publish-DesktopClients
}

if (-not $SkipAndroid) {
    Publish-AndroidApk
}

Write-Host "All requested builds published in dist/"
