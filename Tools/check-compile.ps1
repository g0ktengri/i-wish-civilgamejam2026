# Unity projesini batchmode'da acip C# derleme hatasi olup olmadigini kontrol eder.
# Push etmeden once calistirin (ekip kurali: sadece derlenen kodu push et).
#
# Kullanim:  powershell -ExecutionPolicy Bypass -File Tools\check-compile.ps1
# Cikis kodu: 0 = hata yok, 1 = derleme hatasi, 2 = calistirilamadi
#
# Not: Unity Editor bu projeyi acikken batchmode calisamaz. O durumda
# Editor'deki Console penceresine bakin (kirmizi hata olmamali).

param(
    [int]$TimeoutMinutes = 15
)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot

# Proje surumunu ProjectVersion.txt'den oku, Hub'in kurdugu editoru bul
$versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') |
    Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
$version = ($versionLine -split ':', 2)[1].Trim()

$unityExe = $null
foreach ($root in @("$env:ProgramFiles\Unity\Hub\Editor", "$env:ProgramFiles\Unity\Editor")) {
    $candidate = Join-Path $root "$version\Editor\Unity.exe"
    if (Test-Path $candidate) { $unityExe = $candidate; break }
}
if (-not $unityExe) {
    Write-Host "Unity $version bulunamadi. Unity Hub'dan bu surumu kurun." -ForegroundColor Red
    exit 2
}

# Editor projeyi aciksa Temp\UnityLockfile kilitlidir
$lockFile = Join-Path $projectPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    } catch {
        Write-Host "Proje Unity Editor'de acik. Editor'u kapatin ya da Console penceresine bakin." -ForegroundColor Yellow
        exit 2
    }
}

$logDir = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir 'check-compile.log'

Write-Host "Unity $version batchmode'da derleniyor (ilk calistirma Library olusturdugu icin uzun surebilir)..."
$proc = Start-Process -FilePath $unityExe -PassThru -WindowStyle Hidden -ArgumentList @(
    '-batchmode', '-nographics', '-quit',
    '-projectPath', "`"$projectPath`"",
    '-logFile', "`"$logFile`""
)
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $proc.Kill()
    Write-Host "Zaman asimi ($TimeoutMinutes dk). Log: $logFile" -ForegroundColor Red
    exit 2
}

$log = if (Test-Path $logFile) { Get-Content $logFile } else { @() }
$errors = $log | Where-Object { $_ -match 'error CS\d{4}' } | Sort-Object -Unique
$warnings = $log | Where-Object { $_ -match 'warning CS\d{4}' } | Sort-Object -Unique

if ($warnings) {
    Write-Host "$(@($warnings).Count) uyari:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
}

if ($errors) {
    Write-Host "$(@($errors).Count) derleme hatasi - PUSH ETMEYIN:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

if ($proc.ExitCode -ne 0) {
    Write-Host "Unity $($proc.ExitCode) koduyla cikti ama C# hatasi bulunamadi. Log: $logFile" -ForegroundColor Red
    $log | Select-Object -Last 30 | ForEach-Object { Write-Host "  $_" }
    exit 2
}

Write-Host "Derleme hatasi yok. Push edilebilir." -ForegroundColor Green
exit 0
