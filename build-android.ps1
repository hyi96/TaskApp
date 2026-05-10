param (
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$AndroidSdkDirectory = (Join-Path $env:LOCALAPPDATA "Android\Sdk"),
    [string]$JavaSdkDirectory = "",
    [switch]$BuildOnly
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($JavaSdkDirectory)) {
    if (-not [string]::IsNullOrWhiteSpace($env:JAVA_HOME)) {
        $JavaSdkDirectory = $env:JAVA_HOME
    } else {
        $codexToolsRoot = Join-Path $env:USERPROFILE ".codex\android-build-tools"
        $jdkCandidate = Get-ChildItem -Path $codexToolsRoot -Directory -Filter "jdk-*" -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1

        if ($jdkCandidate) {
            $JavaSdkDirectory = $jdkCandidate.FullName
        }
    }
}

if (-not (Test-Path -LiteralPath $AndroidSdkDirectory)) {
    throw "Android SDK not found at '$AndroidSdkDirectory'. Run the setup commands in docs/android-apk.md."
}

if ([string]::IsNullOrWhiteSpace($JavaSdkDirectory) -or -not (Test-Path -LiteralPath (Join-Path $JavaSdkDirectory "bin\java.exe"))) {
    throw "JDK not found. Set JAVA_HOME or pass -JavaSdkDirectory. See docs/android-apk.md."
}

$project = "TaskApp.Android/TaskApp.Android.csproj"
$properties = @(
    "-p:AndroidSdkDirectory=$AndroidSdkDirectory",
    "-p:JavaSdkDirectory=$JavaSdkDirectory"
)

if ($BuildOnly) {
    dotnet build $project -c $Configuration -f net10.0-android @properties
    $artifactRoot = "TaskApp.Android/bin/$Configuration/net10.0-android"
} else {
    dotnet publish $project -c $Configuration -f net10.0-android -p:AndroidPackageFormat=apk @properties
    $artifactRoot = "TaskApp.Android/bin/$Configuration/net10.0-android/publish"
}

$apk = Get-ChildItem -Path $artifactRoot -Filter "*-Signed.apk" -Recurse -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($apk) {
    Write-Host "APK: $($apk.FullName)"
} else {
    Write-Host "Build completed, but no signed APK was found under $artifactRoot."
}
