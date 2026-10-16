# Claude Code PreToolUse hook'u (.claude/settings.json).
# Claude bir git commit / push komutu calistirmadan once Tools\quick-compile.ps1'i calistirir.
# Derleme hatasi varsa komutu engeller (exit 2) ve hatalari Claude'a geri verir.
# Kontrol calistirilamazsa (Unity yok, Library yok) engellemez.

$raw = [Console]::In.ReadToEnd()
try { $command = ($raw | ConvertFrom-Json).tool_input.command } catch { exit 0 }
if (-not $command) { exit 0 }
if ($command -notmatch 'git' -or $command -notmatch '\b(commit|push)\b') { exit 0 }

$quick = Join-Path (Split-Path -Parent $PSScriptRoot) 'quick-compile.ps1'
$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $quick 2>&1 | ForEach-Object { "$_" }
$code = $LASTEXITCODE

if ($code -eq 1) {
    [Console]::Error.WriteLine("Commit/push engellendi: derleme hatasi var. Hatalari duzeltip tekrar deneyin.")
    $output | ForEach-Object { [Console]::Error.WriteLine($_) }
    exit 2
}
exit 0
