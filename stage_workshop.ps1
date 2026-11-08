param(
    [string]$GameDir = "G:\Program Files (x86)\Steam\steamapps\common\Sea Power",
    [string]$ModFolderName = "CecMod"
)

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
$Csproj = Join-Path $RepoRoot "CecMod\CecMod.csproj"
$BuildOut = Join-Path $RepoRoot "CecMod\bin\Release"
$WorkshopAssets = Join-Path $RepoRoot "workshop"
$DistDir = Join-Path $RepoRoot "dist\$ModFolderName"
$StreamingAssetsTarget = Join-Path $GameDir "Sea Power_Data\StreamingAssets\$ModFolderName"

Write-Host "[1/3] Building CecMod (Release)..." -ForegroundColor Cyan
dotnet build $Csproj -c Release "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "[2/3] Populating dist\$ModFolderName..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
Get-ChildItem -Path $DistDir -Force | Remove-Item -Recurse -Force

Copy-Item (Join-Path $BuildOut "CecMod.dll") -Destination $DistDir
if (Test-Path (Join-Path $BuildOut "CecMod.pdb")) {
    Copy-Item (Join-Path $BuildOut "CecMod.pdb") -Destination $DistDir
}
Copy-Item (Join-Path $WorkshopAssets "_info.ini") -Destination $DistDir
Copy-Item (Join-Path $WorkshopAssets "preview.jpg") -Destination $DistDir

$previewItem = Get-Item (Join-Path $DistDir "preview.jpg")
if ($previewItem.Length -gt 1000000) {
    throw "preview.jpg ($($previewItem.Length) bytes) exceeds Sea Power's 1,000,000-byte Workshop preview limit!"
}

Write-Host "[3/3] Staging to Sea Power StreamingAssets ($StreamingAssetsTarget)..." -ForegroundColor Cyan
if (Test-Path (Join-Path $GameDir "Sea Power_Data\StreamingAssets")) {
    New-Item -ItemType Directory -Force -Path $StreamingAssetsTarget | Out-Null
    Get-ChildItem -Path $StreamingAssetsTarget -Force | Remove-Item -Recurse -Force
    Copy-Item -Path (Join-Path $DistDir "*") -Destination $StreamingAssetsTarget -Recurse -Force
    Write-Host "Staged successfully to: $StreamingAssetsTarget" -ForegroundColor Green
} else {
    Write-Warning "StreamingAssets path not found at '$GameDir'. Only dist\$ModFolderName was updated."
}

Write-Host "`nStaged files:" -ForegroundColor Green
Get-ChildItem $DistDir | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
