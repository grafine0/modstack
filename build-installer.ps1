param(
    [string]$Version = "1.0.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$DistDir = Join-Path $RepoRoot "installer\dist\$Runtime"
$ReleasesDir = Join-Path $RepoRoot "releases"
$IssPath = Join-Path $RepoRoot "installer\ModStack.iss"
$GuiProject = Join-Path $RepoRoot "ModStack.GUI\ModStack.GUI.csproj"

Write-Host "==> [1/4] Cleaning previous publish artifacts..." -ForegroundColor Cyan
if (Test-Path $DistDir) {
    Remove-Item -Path $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null

Write-Host "==> [2/4] Publishing ModStack.GUI ($Configuration | $Runtime | Self-Contained)..." -ForegroundColor Cyan
dotnet publish $GuiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $DistDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "==> [3/4] Locating Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Cyan
$IsccCandidates = @(
    "iscc.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$IsccPath = $null
foreach ($Candidate in $IsccCandidates) {
    $Cmd = Get-Command $Candidate -ErrorAction SilentlyContinue
    if ($Cmd) {
        $IsccPath = $Cmd.Source
        break
    }
}

if (-not $IsccPath) {
    throw "Inno Setup 6 (ISCC.exe) was not found. Install it via: winget install JRSoftware.InnoSetup"
}

Write-Host "    Found ISCC: $IsccPath" -ForegroundColor Gray
Write-Host "==> [4/4] Building Setup Wizard (ModStack-Setup-$Version.exe)..." -ForegroundColor Cyan
& $IsccPath "/DMyAppVersion=$Version" $IssPath

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup compilation failed with exit code $LASTEXITCODE."
}

$InstallerPath = Join-Path $ReleasesDir "ModStack-Setup-$Version.exe"
if (-not (Test-Path $InstallerPath)) {
    throw "Expected installer was not found at: $InstallerPath"
}

$HashObj = Get-FileHash -Path $InstallerPath -Algorithm SHA256
$HashFile = "$InstallerPath.sha256"
"$($HashObj.Hash.ToLowerInvariant())  ModStack-Setup-$Version.exe" | Set-Content -Path $HashFile -Encoding ASCII

$SizeMB = [Math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
Write-Host ""
Write-Host "=========================================================" -ForegroundColor Green
Write-Host " Setup Wizard built successfully!" -ForegroundColor Green
Write-Host " Installer : $InstallerPath ($SizeMB MB)" -ForegroundColor Green
Write-Host " SHA-256   : $($HashObj.Hash.ToLowerInvariant())" -ForegroundColor Green
Write-Host " Checksum  : $HashFile" -ForegroundColor Green
Write-Host "=========================================================" -ForegroundColor Green

