<#
.SYNOPSIS
    Checks the AJ AI Brain (skills, knowledge, scripts) for consistency drift - broken
    cross-references, missing frontmatter, an out-of-sync scripts README.

.DESCRIPTION
    This folder runs on a "living document" convention: skills, knowledge files, and scripts all
    cross-link each other and get updated in place over time. Nothing enforces those links stay valid
    as files get renamed, moved, split, or retired - this script is that enforcement, run on demand
    (part of a brain-self-maintain pass, or whenever something feels stale).

    Checks performed:
      1. Every skills/*/SKILL.md has YAML frontmatter with both "name" and "description".
      2. Every markdown-style relative link [text](path) inside skills/**/*.md,
         knowledge/*.md, scripts/**/*.md, and START-HERE.md resolves to a real file.
      3. Every .cs file under scripts/{filters,actions,recipes,creators,commands,examples}
         is mentioned in scripts/README.md, and every .cs path mentioned in that README
         actually exists on disk - catches drift between the index and the real folder contents.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\verify-consistency.ps1
#>

$ErrorActionPreference = "Stop"
$brainRoot = Split-Path -Parent $PSScriptRoot
$claudeDir = $brainRoot

$issues = New-Object System.Collections.Generic.List[string]

function Get-MarkdownLinkTargets {
    param([string]$Content)
    $matches = [regex]::Matches($Content, '\[[^\]]*\]\(([^)]+)\)')
    $targets = @()
    foreach ($m in $matches) {
        $target = $m.Groups[1].Value
        if ($target -match '^https?://' -or $target.StartsWith('#')) { continue }
        $targets += ($target -split '#')[0]
    }
    return $targets
}

Write-Host "=== 1. Skill frontmatter ===" -ForegroundColor Cyan
$skillFiles = Get-ChildItem -Path (Join-Path $claudeDir "skills") -Filter "SKILL.md" -Recurse -ErrorAction SilentlyContinue
foreach ($skill in $skillFiles) {
    $content = Get-Content -Path $skill.FullName -Raw
    if ($content -notmatch '(?s)^---\r?\n(.*?)\r?\n---') {
        $issues.Add("MISSING FRONTMATTER: " + $skill.FullName)
        continue
    }
    $frontmatter = $Matches[1]
    if ($frontmatter -notmatch '(?m)^name:\s*\S+') {
        $issues.Add("MISSING name in frontmatter: " + $skill.FullName)
    }
    if ($frontmatter -notmatch '(?m)^description:\s*\S+') {
        $issues.Add("MISSING description in frontmatter: " + $skill.FullName)
    }
}
Write-Host ("Checked {0} skill file(s)." -f $skillFiles.Count)

Write-Host "`n=== 2. Markdown link targets ===" -ForegroundColor Cyan
$mdFiles = @()
$mdFiles += Get-ChildItem -Path (Join-Path $claudeDir "skills") -Filter "*.md" -Recurse -ErrorAction SilentlyContinue
$mdFiles += Get-ChildItem -Path (Join-Path $claudeDir "knowledge") -Filter "*.md" -Recurse -ErrorAction SilentlyContinue
$mdFiles += Get-ChildItem -Path (Join-Path $claudeDir "scripts") -Filter "*.md" -Recurse -ErrorAction SilentlyContinue
$startHere = Join-Path $brainRoot "START-HERE.md"
if (Test-Path $startHere) { $mdFiles += Get-Item $startHere }
$setupDoc = Join-Path $brainRoot "SETUP.md"
if (Test-Path $setupDoc) { $mdFiles += Get-Item $setupDoc }
$agentSpec = Join-Path $brainRoot "AGENT-SPEC.md"
if (Test-Path $agentSpec) { $mdFiles += Get-Item $agentSpec }
$toolsReadme = Join-Path $brainRoot "mcp-server\tools\README.md"
if (Test-Path $toolsReadme) { $mdFiles += Get-Item $toolsReadme }

$linkCount = 0
foreach ($md in $mdFiles) {
    $content = Get-Content -Path $md.FullName -Raw
    $targets = Get-MarkdownLinkTargets -Content $content
    foreach ($target in $targets) {
        $linkCount++
        $resolved = Join-Path (Split-Path -Parent $md.FullName) $target
        if (-not (Test-Path $resolved)) {
            $issues.Add("BROKEN LINK in " + $md.FullName + ": '" + $target + "' -> " + $resolved)
        }
    }
}
Write-Host ("Checked {0} link(s) across {1} markdown file(s)." -f $linkCount, $mdFiles.Count)

Write-Host "`n=== 3. Scripts README vs folder contents ===" -ForegroundColor Cyan
$scriptsDir = Join-Path $claudeDir "scripts"
$readmePath = Join-Path $scriptsDir "README.md"
if (Test-Path $readmePath) {
    $readmeContent = Get-Content -Path $readmePath -Raw
    $subfolders = @("filters", "actions", "recipes", "creators", "commands", "examples")

    $onDisk = @()
    foreach ($folder in $subfolders) {
        $folderPath = Join-Path $scriptsDir $folder
        if (Test-Path $folderPath) {
            $onDisk += Get-ChildItem -Path $folderPath -Filter "*.cs" -Recurse | ForEach-Object {
                $relative = $_.FullName.Substring($folderPath.Length + 1) -replace '\\', '/'
                "$folder/$relative"
            }
        }
    }

    foreach ($file in $onDisk) {
        if ($readmeContent -notmatch [regex]::Escape($file)) {
            $issues.Add("SCRIPT NOT IN README: " + $file + " exists on disk but isn't mentioned in scripts/README.md")
        }
    }

    $readmeScriptRefs = [regex]::Matches($readmeContent, '\(((?:filters|actions|recipes|creators|commands|examples)/[^)]+\.cs)\)') |
        ForEach-Object { $_.Groups[1].Value }
    foreach ($ref in $readmeScriptRefs) {
        $refPath = Join-Path $scriptsDir $ref
        if (-not (Test-Path $refPath)) {
            $issues.Add("README REFERENCES MISSING SCRIPT: '" + $ref + "' listed in README.md but not found on disk")
        }
    }

    Write-Host ("Checked {0} script file(s) on disk against README.md." -f $onDisk.Count)
} else {
    $issues.Add("MISSING FILE: scripts/README.md not found")
}

Write-Host "`n=== Result ===" -ForegroundColor Cyan
if ($issues.Count -eq 0) {
    Write-Host "All checks passed - no drift found." -ForegroundColor Green
    exit 0
} else {
    Write-Host ("{0} issue(s) found:" -f $issues.Count) -ForegroundColor Yellow
    foreach ($issue in $issues) { Write-Host ("  - " + $issue) -ForegroundColor Yellow }
    exit 1
}
