# Fails when an Inno Setup script does not declare UninstallDisplayIcon.
# Without that directive Inno leaves the uninstall key's DisplayIcon empty and
# Windows shows a generic icon in Settings > Apps, even though the installer,
# shortcuts and executable all carry the right icon.
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scripts = @(Get-ChildItem -Path $root -Filter '*.iss' -File)
if ($scripts.Count -eq 0) {
    Write-Host '::error::No Inno Setup script (*.iss) found in the repository root.'
    exit 1
}

$failed = $false
foreach ($script in $scripts) {
    $match = Select-String -LiteralPath $script.FullName -Pattern '^\s*UninstallDisplayIcon\s*=\s*\S+' -CaseSensitive:$false
    if ($match) {
        Write-Host "OK $($script.Name): $($match.Line.Trim())"
    }
    else {
        Write-Host "::error file=$($script.Name)::Missing UninstallDisplayIcon; Windows Apps would show a generic icon."
        $failed = $true
    }
}

if ($failed) {
    exit 1
}

Write-Host 'All installer scripts declare UninstallDisplayIcon.'
