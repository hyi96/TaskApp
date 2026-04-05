# build-all.ps1
# Publishes .NET app for all major platforms and zips the output

param (
    [string]$Configuration = "Release",
    [string]$ProjectPath = "TaskApp/TaskApp.csproj"
)

# Define platforms and output folders
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

# Clean previous dist folder
if (Test-Path dist) { Remove-Item -Recurse -Force dist }
New-Item -ItemType Directory -Path dist | Out-Null

foreach ($rid in $targets.Keys) {
    $platformFolder = $targets[$rid]
    $outputPath = Join-Path -Path "dist/$platformFolder/$rid" -ChildPath ""

    if ($rid.StartsWith("win")) { $tfm = $windowsTfm } else { $tfm = $crossPlatTfm }

    Write-Host "Publishing for $rid (TFM: $tfm)..."

    dotnet publish $ProjectPath -c $Configuration -r $rid `
        --self-contained true /p:PublishSingleFile=true `
        /p:TargetFramework=$tfm `
        -o $outputPath

    # Zip the output
    $zipName = "$platformFolder-$rid.zip"
    $zipPath = Join-Path -Path "dist/$platformFolder" -ChildPath $zipName

    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path "$outputPath/*" -DestinationPath $zipPath
    Write-Host "Zipped to $zipPath`n"
}

Write-Host "✅ All platforms published and zipped in dist/"
