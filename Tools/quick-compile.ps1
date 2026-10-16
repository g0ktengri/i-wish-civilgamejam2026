# Hizli derleme kontrolu: Assets altindaki C# kodunu Unity'nin kendi derleyicisiyle
# (Roslyn) Unity'yi acmadan derler. Unity Editor acikken de calisir, birkac saniye surer.
#
# Iki gecis yapar:
#   1) Oyun kodu  : Editor klasorleri haric, UnityEditor referansi YOK
#                   (Windows build'i kiracak "oyun kodunda UnityEditor kullanimi" hatasini yakalar)
#   2) Editor kodu: butun dosyalar, UnityEditor referansiyla
#
# Kullanim:  powershell -ExecutionPolicy Bypass -File Tools\quick-compile.ps1
# Cikis kodu: 0 = hata yok, 1 = derleme hatasi, 2 = calistirilamadi
#
# Kesin kontrol degildir (asmdef, kod uretimi vb. kapsamaz). Build oncesi ve
# supheli durumlarda Tools\check-compile.ps1 (batchmode) ya da Editor Console esastir.

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot

$versionLine = Get-Content (Join-Path $projectPath 'ProjectSettings\ProjectVersion.txt') |
    Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1
$version = ($versionLine -split ':', 2)[1].Trim()

$data = $null
foreach ($root in @("$env:ProgramFiles\Unity\Hub\Editor", "$env:ProgramFiles\Unity\Editor")) {
    $candidate = Join-Path $root "$version\Editor\Data"
    if (Test-Path $candidate) { $data = $candidate; break }
}
if (-not $data) {
    Write-Host "Unity $version bulunamadi. Unity Hub'dan bu surumu kurun." -ForegroundColor Red
    exit 2
}

$dotnet = Join-Path $data 'NetCoreRuntime\dotnet.exe'
$csc = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
$scriptAssemblies = Join-Path $projectPath 'Library\ScriptAssemblies'
if (-not (Test-Path $scriptAssemblies)) {
    Write-Host "Library\ScriptAssemblies yok. Projeyi bir kez Unity'de acin ya da Tools\check-compile.ps1 calistirin." -ForegroundColor Red
    exit 2
}

$assetsPath = Join-Path $projectPath 'Assets'
if (Get-ChildItem $assetsPath -Recurse -Filter *.asmdef -ErrorAction SilentlyContinue) {
    Write-Host "Uyari: Assets altinda .asmdef var; hizli kontrol bunlari tek assembly gibi derler." -ForegroundColor Yellow
}

$allFiles = @(Get-ChildItem $assetsPath -Recurse -Filter *.cs | Select-Object -ExpandProperty FullName)
if ($allFiles.Count -eq 0) {
    Write-Host "Derlenecek C# dosyasi yok." -ForegroundColor Green
    exit 0
}
# Unity kurali: adi "Editor" olan herhangi bir klasorun altindaki kod sadece editorde derlenir
$runtimeFiles = @($allFiles | Where-Object { $_ -notmatch '[\\/]Editor[\\/]' })

# Define'lar: Unity'nin urettigi csproj varsa oradan, yoksa temel liste
$defines = @('UNITY_6000_0_OR_NEWER', 'UNITY_6000_3_OR_NEWER', 'UNITY_STANDALONE_WIN', 'UNITY_STANDALONE',
             'ENABLE_INPUT_SYSTEM', 'NET_STANDARD_2_1', 'NETSTANDARD2_1', 'DEBUG', 'TRACE', 'UNITY_ASSERTIONS')
$csproj = Join-Path $projectPath 'Assembly-CSharp.csproj'
if (Test-Path $csproj) {
    $m = Select-String -Path $csproj -Pattern '<DefineConstants>(.*)</DefineConstants>' | Select-Object -First 1
    if ($m) { $defines = $m.Matches[0].Groups[1].Value -split ';' | Where-Object { $_ } }
}
$runtimeDefines = @($defines | Where-Object { $_ -notlike 'UNITY_EDITOR*' })
$editorDefines = @($runtimeDefines + 'UNITY_EDITOR', 'UNITY_EDITOR_64', 'UNITY_EDITOR_WIN')

$engineDlls = @(Get-ChildItem (Join-Path $data 'Managed\UnityEngine') -Filter *.dll | Select-Object -ExpandProperty FullName)
$packageDlls = @(Get-ChildItem $scriptAssemblies -Filter *.dll |
    Where-Object { $_.Name -notlike 'Assembly-CSharp*' } | Select-Object -ExpandProperty FullName)
$netstandard = Join-Path $data 'NetStandard\ref\2.1.0\netstandard.dll'

$runtimeRefs = @($netstandard) +
    @($engineDlls | Where-Object { (Split-Path $_ -Leaf) -notlike 'UnityEditor*' }) +
    @($packageDlls | Where-Object { (Split-Path $_ -Leaf) -notmatch 'Editor|TestRunner|CodeGen' })
$editorRefs = @($netstandard) + $engineDlls + $packageDlls

$outDir = Join-Path $projectPath 'Temp\QuickCompile'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Invoke-Pass($name, $files, $refs, $defs) {
    if ($files.Count -eq 0) { return @() }
    $rsp = Join-Path $outDir "$name.rsp"
    $lines = @('/nologo', '/noconfig', '/nostdlib+', '/target:library', '/langversion:9.0', '/warn:0', '/preferreduilang:en-US',
               "/out:`"$(Join-Path $outDir "$name.dll")`"", "/define:$($defs -join ';')")
    $lines += $refs | ForEach-Object { "/reference:`"$_`"" }
    $lines += $files | ForEach-Object { "`"$_`"" }
    [System.IO.File]::WriteAllLines($rsp, $lines, (New-Object System.Text.UTF8Encoding $false))
    $output = & $dotnet $csc "@$rsp" 2>&1 | ForEach-Object { "$_" }
    return @($output | Where-Object { $_ -match 'error CS\d{4}' })
}

$runtimeErrors = Invoke-Pass 'Runtime' $runtimeFiles $runtimeRefs $runtimeDefines
$editorErrors = Invoke-Pass 'Editor' $allFiles $editorRefs $editorDefines

$prefix = $projectPath.TrimEnd('\') + '\'
$errors = @(@($runtimeErrors) + @($editorErrors) | ForEach-Object { $_.Replace($prefix, '') } | Sort-Object -Unique)

if ($errors.Count -gt 0) {
    Write-Host "$($errors.Count) derleme hatasi - PUSH ETMEYIN:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "Hizli kontrol: $($allFiles.Count) dosya, derleme hatasi yok." -ForegroundColor Green
exit 0
