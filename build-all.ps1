# build-all.ps1
# Publishes .NET app for all major platforms and zips the output

param (
    [string]$Configuration = "Release",
    [string]$ProjectPath = "TaskApp/TaskApp.csproj"
)

# Define platforms and output folders
$targets = @{
    "win-x64"      = "Windows"
    "win-x86"      = "Windows"
    "osx-x64"      = "macOS"
    "osx-arm64"    = "macOS"
    "linux-x64"    = "Linux"
    "linux-arm"    = "Linux"
    "linux-arm64"  = "Linux"
    "linux-musl-x64" = "Linux"
}

# Clean previous dist folder
if (Test-Path dist) { Remove-Item -Recurse -Force dist }
New-Item -ItemType Directory -Path dist | Out-Null

foreach ($rid in $targets.Keys) {
    $platformFolder = $targets[$rid]
    $outputPath = Join-Path -Path "dist/$platformFolder/$rid" -ChildPath ""
    
    Write-Host "Publishing for $rid..."
    
    dotnet publish $ProjectPath -c $Configuration -r $rid `
        --self-contained true /p:PublishSingleFile=true `
        -o $outputPath

    # Zip the output
    $zipName = "$platformFolder-$rid.zip"
    $zipPath = Join-Path -Path "dist/$platformFolder" -ChildPath $zipName

    if (Test-Path $zipPath) { Remove-Item $zipPath }
    Compress-Archive -Path "$outputPath/*" -DestinationPath $zipPath
    Write-Host "Zipped to $zipPath`n"
}

Write-Host "✅ All platforms published and zipped in dist/"
