<#
.SYNOPSIS
    "Will my scripts work in this Revit?" — answered in about a minute, without opening Revit.

.DESCRIPTION
    Finds every Revit installed on this PC and compile-checks all the library's C# fragments against
    each one's real API files, then prints one plain line per version.

    THIS IS THE ANSWER TO THE VERSION WORRY. Ajmal, 2026-08-20, in his own words: "if i have proven
    things in revit 2020 and if i open any latest vertion revit and its getting error becose of anything
    that is also issue". You do not have to find out by hitting the error in the middle of a job. Run
    this once after installing a new Revit and you know before you start.

    It wraps tools/verify-fragments-compile.ps1, which already did this for ONE version and was
    effectively undiscoverable — mentioned once in a tools list and once in a footnote about %TEMP%.

    WHAT IT CATCHES: a script naming an API member that does not exist in that Revit — the whole class
    of "worked in 2020, errors in 2024". Misspellings, wrong overloads, missing casts, syntax errors.

    WHAT IT CANNOT CATCH: whether a script does the RIGHT thing. A script can compile perfectly and
    still act on the wrong elements. Passing here is a floor, not a ceiling — still run one element
    first and check the real result.

.EXAMPLE
    check-scripts                 check every Revit found on this PC
    check-scripts -Only 2024      check just that one
#>
param(
    [string]$Only,
    [string]$RevitRoot = "C:\Program Files\Autodesk"
)

$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$checker = Join-Path $here "verify-fragments-compile.ps1"

if (-not (Test-Path $checker)) {
    Write-Host "Cannot find verify-fragments-compile.ps1 next to this script." -ForegroundColor Red
    exit 1
}

$installs = @()
if (Test-Path $RevitRoot) {
    $installs = Get-ChildItem -Path $RevitRoot -Directory -Filter "Revit *" -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "RevitAPI.dll") } |
        Sort-Object Name
}
if ($Only) { $installs = $installs | Where-Object { $_.Name -like "*$Only*" } }

if (-not $installs -or $installs.Count -eq 0) {
    Write-Host "No Revit install found under $RevitRoot." -ForegroundColor Red
    Write-Host "If Revit is somewhere else, pass it:  check-scripts -RevitRoot 'D:\Autodesk'"
    exit 1
}

Write-Host ""
Write-Host "Checking the script library against every Revit on this PC" -ForegroundColor Cyan
Write-Host "(Revit is NOT opened. Nothing is changed. This only reads.)"
Write-Host ""

$results = @()
foreach ($install in $installs) {
    Write-Host ("--- {0} " -f $install.Name).PadRight(70, '-') -ForegroundColor Cyan
    $output = & powershell -NoProfile -ExecutionPolicy Bypass -File $checker -RevitPath $install.FullName 2>&1
    $summary = $output | Select-String -Pattern '^\s*(\d+) passed, (\d+) failed, of (\d+)' | Select-Object -Last 1
    if ($summary) {
        $pass = [int]$summary.Matches[0].Groups[1].Value
        $fail = [int]$summary.Matches[0].Groups[2].Value
        $tot  = [int]$summary.Matches[0].Groups[3].Value
        $results += [pscustomobject]@{ Revit = $install.Name; Pass = $pass; Fail = $fail; Total = $tot }
        if ($fail -gt 0) {
            Write-Host ("  {0} of {1} scripts would FAIL here:" -f $fail, $tot) -ForegroundColor Red
            $output | Select-String -Pattern '^\s*FAIL\s+(.+)$' | ForEach-Object {
                Write-Host ("     {0}" -f $_.Matches[0].Groups[1].Value) -ForegroundColor DarkYellow
            }
        } else {
            Write-Host ("  all {0} scripts compile here" -f $tot) -ForegroundColor Green
        }
    } else {
        $results += [pscustomobject]@{ Revit = $install.Name; Pass = 0; Fail = -1; Total = 0 }
        Write-Host "  could not read a result — the checker's own output follows:" -ForegroundColor Red
        $output | Select-Object -Last 12 | ForEach-Object { Write-Host ("     {0}" -f $_) }
    }
    Write-Host ""
}

Write-Host "=== In plain words ===" -ForegroundColor Cyan
foreach ($r in $results) {
    if ($r.Fail -lt 0)      { Write-Host ("  {0,-14} could not be checked — see above" -f $r.Revit) -ForegroundColor Red }
    elseif ($r.Fail -eq 0)  { Write-Host ("  {0,-14} SAFE — all {1} scripts work in this version" -f $r.Revit, $r.Total) -ForegroundColor Green }
    else                    { Write-Host ("  {0,-14} {1} script(s) would error. Send the FAIL list above to Claude and it gets fixed." -f $r.Revit, $r.Fail) -ForegroundColor Yellow }
}
Write-Host ""
Write-Host "Reminder: compiling is a floor, not a ceiling. A script can compile and still do the wrong"
Write-Host "thing — run it on ONE element and check the real result before trusting it on a batch."
Write-Host ""
