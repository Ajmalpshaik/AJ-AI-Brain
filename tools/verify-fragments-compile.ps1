<#
.SYNOPSIS
    Compile-checks every C# fragment in scripts/ against the real Revit API DLLs, without opening Revit.

.DESCRIPTION
    The gap this closes: 147 of the library's fragments have never been executed even once, and the repo
    has already been bitten by exactly what that allows. action-reload-links.cs referenced
    LinkLoadResultType.LinkNotNeeded - an enum member that does not exist on Revit 2020 - and carried
    that plain compile error for months. A static review flagged it and could not settle it; only a
    compile could. This script is that compile, for all 351 fragments at once, in about a minute.

    What it CAN catch: misspelled API members, wrong overloads, types that do not exist on this Revit
    version, missing casts, syntax errors - the CS#### family. That is the whole class of bug above.

    What it CANNOT catch: whether a fragment does the RIGHT thing. A fragment that compiles perfectly can
    still delete the wrong elements. Compiling is a floor, not a ceiling - "it compiled" is never a
    substitute for running it on one element and checking the real result.

    HOW IT WORKS
    Fragments are not standalone programs. They are pasted into a composed script where the bridge
    supplies `Document` and friends, and where a filter fragment has already declared `sb` and
    `elements`. So each fragment is wrapped in a small harness class that supplies exactly what it is
    missing, then compiled as a library and thrown away.

    Whether a fragment needs `sb`/`elements` injected is decided by looking at the CODE, not the header
    prose - the "ASSUMES:" lines are written for humans and come in a dozen wordings, while
    "does this file declare sb?" is unambiguous. Comment lines are stripped before that test, because
    several fragments mention `var sb = new ...` inside a comment.

.PARAMETER RevitPath
    Revit install folder holding RevitAPI.dll (e.g. "C:\Program Files\Autodesk\Revit 2020").
    Omit to auto-detect the newest installed Revit.

.PARAMETER CscPath
    Full path to csc.exe. Omit to auto-detect. MUST be the Roslyn compiler, not the old
    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe - 69 fragments use C# 7 pattern matching
    (`if (e is Wall wall)`) and every one of them uses string interpolation, so the old C# 5 compiler
    would report ~200 failures that are not real.

.PARAMETER DryRun
    Do everything except invoke the compiler: resolve each fragment, work out what it needs injected,
    and write the wrapper it would compile. Needs neither Revit nor csc, so the harness itself can be
    validated on a machine that has neither.

.PARAMETER Filter
    Wildcard against the fragment's path relative to scripts/ (e.g. "recipes/*", "*duct*").

.PARAMETER KeepWrappers
    Leave the generated wrappers on disk for inspection instead of deleting them.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\verify-fragments-compile.ps1
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\verify-fragments-compile.ps1 -RevitPath "C:\Program Files\Autodesk\Revit 2020"
.EXAMPLE
    pwsh tools/verify-fragments-compile.ps1 -DryRun -Filter "recipes/*"
#>

param(
    [string]$RevitPath,
    [string]$CscPath,
    [switch]$DryRun,
    [string]$Filter = "*",
    [switch]$KeepWrappers
)

$ErrorActionPreference = "Stop"
$brainRoot  = Split-Path -Parent $PSScriptRoot
$scriptsDir = Join-Path $brainRoot "scripts"
$workDir    = Join-Path ([System.IO.Path]::GetTempPath()) ("aj-fragment-compile-" + $PID)
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

# ---------------------------------------------------------------------------------------------------
# 1. Locate the Revit API assemblies
# ---------------------------------------------------------------------------------------------------
$revitApi = $null; $revitApiUi = $null
$frameworkRefs = @()   # .NET ref-pack DLLs; stays empty for a .NET Framework Revit
if (-not $DryRun) {
    if (-not $RevitPath) {
        $candidates = @()
        foreach ($base in @("${env:ProgramFiles}\Autodesk", "${env:ProgramW6432}\Autodesk")) {
            if (Test-Path $base) {
                $candidates += Get-ChildItem -Path $base -Directory -Filter "Revit *" -ErrorAction SilentlyContinue
            }
        }
        # Newest version wins - the name sorts correctly because it always ends in a 4-digit year.
        $RevitPath = ($candidates | Sort-Object Name -Descending | Select-Object -First 1).FullName
    }
    if (-not $RevitPath -or -not (Test-Path $RevitPath)) {
        Write-Host "Could not find a Revit install. Pass -RevitPath 'C:\Program Files\Autodesk\Revit 2020'." -ForegroundColor Red
        exit 2
    }
    $revitApi   = Join-Path $RevitPath "RevitAPI.dll"
    $revitApiUi = Join-Path $RevitPath "RevitAPIUI.dll"
    foreach ($dll in @($revitApi, $revitApiUi)) {
        if (-not (Test-Path $dll)) {
            Write-Host "Not found: $dll — is -RevitPath the folder that holds RevitAPI.dll?" -ForegroundColor Red
            exit 2
        }
    }
    Write-Host ("Revit API : {0}" -f $RevitPath) -ForegroundColor Cyan

    # ---- .NET-based Revit (2025+) needs the .NET reference assemblies, not Framework's -------------
    # Revit 2025 moved the add-in surface to .NET 8 and 2027 to .NET 10. csc's DEFAULT references are
    # the .NET Framework ones, so RevitAPI.dll's own reference to System.Runtime 10.0.0.0 cannot be
    # resolved and EVERY fragment fails with CS0012 "The type Object is defined in an assembly that is
    # not referenced". That is a harness fault, not 283 broken fragments - it was read as the latter
    # once (2026-08-20) before this block existed. Detection is the runtimeconfig.json Autodesk ships
    # beside RevitAPI.dll: present means .NET, absent means Framework.
    $rtConfig = Join-Path $RevitPath "RevitAPI.runtimeconfig.json"
    if (Test-Path $rtConfig) {
        $tfm = "net10.0"; $major = 10
        try {
            $cfg = Get-Content $rtConfig -Raw | ConvertFrom-Json
            if ($cfg.runtimeOptions.tfm) { $tfm = $cfg.runtimeOptions.tfm }
            $fx = $cfg.runtimeOptions.frameworks | Where-Object { $_.name -eq "Microsoft.NETCore.App" } | Select-Object -First 1
            if ($fx.version) { $major = [int]($fx.version -split '\.')[0] }
        } catch { }

        # Highest installed ref pack whose major matches what this Revit asks for.
        $packRoot = Join-Path $env:ProgramFiles "dotnet\packs"
        foreach ($packName in @("Microsoft.NETCore.App.Ref", "Microsoft.WindowsDesktop.App.Ref")) {
            $verDir = Get-ChildItem (Join-Path $packRoot $packName) -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -like "$major.*" } |
                Sort-Object { [version]$_.Name } | Select-Object -Last 1
            if (-not $verDir) { continue }
            $refDir = Join-Path $verDir.FullName "ref\$tfm"
            if (-not (Test-Path $refDir)) {
                $refDir = (Get-ChildItem (Join-Path $verDir.FullName "ref") -Directory -ErrorAction SilentlyContinue |
                           Select-Object -Last 1).FullName
            }
            if ($refDir -and (Test-Path $refDir)) {
                $frameworkRefs += (Get-ChildItem $refDir -Filter *.dll -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
            }
        }

        if ($frameworkRefs.Count -eq 0) {
            Write-Host ("This Revit targets {0}, but no matching .NET {1} reference pack is installed." -f $tfm, $major) -ForegroundColor Red
            Write-Host "  Every fragment would report a false CS0012 failure, so the run is stopped instead." -ForegroundColor Yellow
            Write-Host ("  Fix: install the .NET {0} SDK (it brings the ref pack), then re-run." -f $major) -ForegroundColor Yellow
            exit 2
        }
        Write-Host ("Target    : {0} - using {1} reference assemblies" -f $tfm, $frameworkRefs.Count) -ForegroundColor Cyan
    } else {
        Write-Host "Target    : .NET Framework - using csc's default references" -ForegroundColor Cyan
    }
}

# ---------------------------------------------------------------------------------------------------
# 2. Locate a ROSLYN csc.exe - see the CscPath note above for why the Framework one is not acceptable
# ---------------------------------------------------------------------------------------------------
if (-not $DryRun) {
    if (-not $CscPath) {
        $roslynGlobs = @(
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\*\*\MSBuild\Current\Bin\Roslyn\csc.exe",
            "${env:ProgramFiles(x86)}\MSBuild\*\Bin\Roslyn\csc.exe",
            "${env:ProgramFiles}\dotnet\sdk\*\Roslyn\bincore\csc.dll"
        )
        foreach ($glob in $roslynGlobs) {
            $hit = Get-ChildItem -Path $glob -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
            if ($hit) { $CscPath = $hit.FullName; break }
        }
    }
    if (-not $CscPath) {
        Write-Host "No Roslyn csc.exe found." -ForegroundColor Red
        Write-Host "  The old C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe will NOT do: it is a" -ForegroundColor Yellow
        Write-Host "  C# 5 compiler, and 69 fragments use C# 7 pattern matching plus string interpolation," -ForegroundColor Yellow
        Write-Host "  so it would report a few hundred failures that are not real." -ForegroundColor Yellow
        Write-Host "  Fix: install 'Build Tools for Visual Studio' (free), or pass -CscPath to a Roslyn csc.exe." -ForegroundColor Yellow
        exit 2
    }
    Write-Host ("Compiler  : {0}" -f $CscPath) -ForegroundColor Cyan
}

# ---------------------------------------------------------------------------------------------------
# 3. Wrap and compile each fragment
# ---------------------------------------------------------------------------------------------------
$fragments = Get-ChildItem -Path $scriptsDir -Filter "*.cs" -Recurse |
    Sort-Object FullName |
    Where-Object { ($_.FullName.Substring($scriptsDir.Length).TrimStart('\','/') -replace '\\','/') -like $Filter }

Write-Host ("Fragments : {0}{1}" -f $fragments.Count, $(if ($DryRun) { "  (DRY RUN - generating wrappers, not compiling)" } else { "" })) -ForegroundColor Cyan
Write-Host ""

$pass = 0; $fail = 0; $failures = @()

foreach ($frag in $fragments) {
    $rel = ($frag.FullName.Substring($scriptsDir.Length).TrimStart('\','/')) -replace '\\','/'
    $src = [System.IO.File]::ReadAllText($frag.FullName, [System.Text.Encoding]::UTF8)

    # Detection runs on code only. Several fragments quote "var sb = new ..." inside a comment, and
    # lib/prelude.cs explicitly discusses `sb` in prose - matching those would inject nothing and the
    # fragment would fail to compile for a reason that is purely an artefact of this harness.
    $code = ($src -split "`r?`n" | Where-Object { -not $_.TrimStart().StartsWith('//') }) -join "`n"

    # The trailing [=;] matters: three filters declare `List<Element> elements;` with NO initialiser,
    # and an '=' only pattern missed all three. The harness then injected a second `elements` on top of
    # the fragment's own and reported CS0128 - three false failures that looked exactly like real bugs.
    # Fixed 2026-08-04 after the first full run.
    $declaresSb       = $code -match '\b(var|System\.Text\.StringBuilder|StringBuilder)\s+sb\s*[=;]'
    $declaresElements = $code -match '\b(var|List<Element>|IList<Element>)\s+elements\s*[=;]'
    $usesElements     = $code -match '\belements\b'

    # prelude-smoke-test.cs exists to exercise lib/prelude.cs and its own header says to paste the
    # prelude first. Compiling it alone is therefore guaranteed to fail on every helper name - which is
    # a harness artefact, not a defect in either file. Prepending the prelude is what the documented
    # run does, and it turns this into a real check of the two together.
    $prefix = ""
    if ($rel -eq 'examples/prelude-smoke-test.cs') {
        $preludePath = Join-Path $scriptsDir 'lib/prelude.cs'
        if (Test-Path $preludePath) {
            $prefix = [System.IO.File]::ReadAllText($preludePath, [System.Text.Encoding]::UTF8)
        }
    }

    $inject = New-Object System.Text.StringBuilder
    if (-not $declaresSb)                        { [void]$inject.AppendLine('        var sb = new System.Text.StringBuilder();') }
    if (-not $declaresElements -and $usesElements) { [void]$inject.AppendLine('        var elements = new List<Element>();') }

    # The harness mirrors what the bridge provides. Fields named the same as their type (Document
    # Document) are legal C# - the "Color Color" rule - and this is how fragments already address them.
    # `doc`/`uidoc` are aliases: 3 fragments use the lowercase form, and a fragment that declares its own
    # local of that name simply shadows the field, which is also legal.
    $wrapper = @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.DB.Architecture;

public class AjFragmentHarness
{
    public Document Document;
    public Document doc;
    public UIDocument UIDocument;
    public UIDocument uidoc;
    public Autodesk.Revit.ApplicationServices.Application Application;
    public UIApplication UIApplication;

    public string Run()
    {
$($inject.ToString())
$prefix
#line 1 "$rel"
$src
#line default
        return sb.ToString();
    }
}
"@

    $wrapperPath = Join-Path $workDir (($rel -replace '[\\/]', '_') + ".wrapper.cs")
    [System.IO.File]::WriteAllText($wrapperPath, $wrapper, (New-Object System.Text.UTF8Encoding($false)))

    if ($DryRun) {
        $needs = @()
        if (-not $declaresSb) { $needs += "sb" }
        if (-not $declaresElements -and $usesElements) { $needs += "elements" }
        $needsText = if ($needs.Count) { "injects " + ($needs -join "+") } else { "self-contained" }
        Write-Host ("  {0,-62} {1}" -f $rel, $needsText)
        $pass++
        continue
    }

    $outDll = Join-Path $workDir "out.dll"
    $args = @("/nologo", "/target:library", "/langversion:latest", "/warn:0", "/out:$outDll")
    if ($frameworkRefs.Count -gt 0) {
        # /nostdlib+ so csc does not ALSO pull in Framework's mscorlib alongside the .NET ref pack -
        # having both is how you get "predefined type is defined twice" on every single fragment.
        $args += "/nostdlib+"
        foreach ($r in $frameworkRefs) { $args += "/reference:$r" }
    }
    $args += @("/reference:$revitApi", "/reference:$revitApiUi", $wrapperPath)
    $result = & $CscPath @args 2>&1
    # Only CS#### *errors* count. Warnings are suppressed by /warn:0 anyway, but a fragment that already
    # ends in `return sb.ToString();` makes the harness's own trailing return unreachable - a warning,
    # never a failure.
    $errors = @($result | Where-Object { $_ -match ':\s*error\s+CS\d+' })

    if ($errors.Count -eq 0) {
        $pass++
        Write-Host ("  PASS  {0}" -f $rel) -ForegroundColor Green
    } else {
        $fail++
        Write-Host ("  FAIL  {0}" -f $rel) -ForegroundColor Red
        foreach ($e in $errors | Select-Object -First 4) { Write-Host ("          {0}" -f $e) -ForegroundColor DarkYellow }
        $failures += [pscustomobject]@{ Fragment = $rel; Errors = ($errors -join "`n") }
    }
}

# ---------------------------------------------------------------------------------------------------
# 4. Report
# ---------------------------------------------------------------------------------------------------
Write-Host ""
Write-Host "=== Result ===" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host ("Dry run: {0} wrapper(s) generated in {1}" -f $pass, $workDir) -ForegroundColor Green
    Write-Host "Nothing was compiled. Re-run without -DryRun on a machine with Revit + Roslyn csc."
    if (-not $KeepWrappers) { Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue }
    exit 0
}

Write-Host ("{0} passed, {1} failed, of {2} fragment(s)." -f $pass, $fail, $fragments.Count)
if ($fail -gt 0) {
    $reportPath = Join-Path $brainRoot "fragment-compile-failures.txt"
    $failures | ForEach-Object { "=== $($_.Fragment)`n$($_.Errors)`n" } |
        Set-Content -Path $reportPath -Encoding UTF8
    Write-Host ("Full errors written to {0}" -f $reportPath) -ForegroundColor Yellow
    Write-Host "Fix the fragment, not the harness - unless the error names a type the harness failed to" -ForegroundColor Yellow
    Write-Host "supply, in which case add the missing using/field above and say so in brain-log.md." -ForegroundColor Yellow
} elseif ($Filter -eq "*") {
    # A clean FULL run means every failure in an old report is fixed - delete it, or the stale file
    # sits at the repo root describing failures that no longer exist. (Found the hard way: the
    # 2026-08-04 fix run never cleared the pre-fix report, and it read as a live failure for days.)
    # A filtered run leaves the report alone: it only proved a subset.
    $reportPath = Join-Path $brainRoot "fragment-compile-failures.txt"
    if (Test-Path $reportPath) {
        Remove-Item $reportPath -Force
        Write-Host "Stale fragment-compile-failures.txt removed - this clean full run supersedes it." -ForegroundColor Green
    }
}
if (-not $KeepWrappers) { Remove-Item -Recurse -Force $workDir -ErrorAction SilentlyContinue }
exit $(if ($fail -gt 0) { 1 } else { 0 })
