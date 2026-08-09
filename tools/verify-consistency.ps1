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
         knowledge/*.md, scripts/**/*.md, and the root docs (START-HERE.md, SETUP.md,
         AGENT-SPEC.md, README.md, CLAUDE.md) resolves to a real file.
      3. Every .cs file under scripts/{filters,actions,recipes,creators,commands,examples}
         is mentioned in scripts/README.md, and every .cs path mentioned in that README
         actually exists on disk - catches drift between the index and the real folder contents.
      4. Every skills/ folder is named in README.md and START-HERE.md, and every literal
         "N skills" claim in README.md/SETUP.md/plugin.json matches the real folder count.
      5. AGENT-SPEC.md section 3.5's fragment counts match what's actually on disk.
      6. No file contains double-encoded (mojibake) text - the signature of a UTF-8 file
         read as ANSI and written back, which CLAUDE.md warns about.
      7. Every relative .md reference inside a scripts/**/*.cs fragment resolves - the
         "// SOURCE: ../../knowledge/..." headers that link a fragment to its reasoning.
         These are plain C# comments, not markdown links, so check 2 never saw them.
      8. Every "searches all N files" claim in CLAUDE.md/START-HERE.md/README.md matches
         the number of files the semantic index actually covers (scripts/knowledge/skills
         .md+.cs, the five root docs, and mcp-server/tools/README.md) - it drifted
         306-vs-309 for two days before this check existed.

    Checks 4-6 were added 2026-08-04 after an audit found the fire sprinkler skill missing
    from README/SETUP/plugin.json, AGENT-SPEC 58 fragments out of date, and a corrupted
    string literal silently breaking a sort - none of which checks 1-3 could see.
    Check 7 followed the same day, after 19 fragment SOURCE headers turned out to point at
    nothing (../knowledge/ from a folder whose depth needed ../../knowledge/).

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
foreach ($rootDoc in @("START-HERE.md", "SETUP.md", "AGENT-SPEC.md", "README.md", "CLAUDE.md")) {
    $rootDocPath = Join-Path $brainRoot $rootDoc
    if (Test-Path $rootDocPath) { $mdFiles += Get-Item $rootDocPath }
}
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
# Declared out here, not inside the if, because check 5 below reuses it - PowerShell has no block
# scoping, but an assignment that never runs still leaves it null.
$subfolders = @("filters", "actions", "recipes", "creators", "commands", "examples", "context", "lib")
if (Test-Path $readmePath) {
    $readmeContent = Get-Content -Path $readmePath -Raw
    # Recursive, and paths are always the FULL path relative to scripts/ (subfolders included, forward
    # slashes) — the old non-recursive listing went silently blind to every file one level deeper the
    # moment scripts/actions/ was reorganized into job-grouped subfolders (see brain-log.md 2026-07-23).
    # Kept in sync by hand with verify-consistency.mjs — if you change what one checks, change both.

    $onDisk = @()
    foreach ($folder in $subfolders) {
        $folderPath = Join-Path $scriptsDir $folder
        if (Test-Path $folderPath) {
            $onDisk += Get-ChildItem -Path $folderPath -Filter "*.cs" -Recurse | ForEach-Object {
                $relative = $_.FullName.Substring($scriptsDir.Length).TrimStart('\', '/')
                $relative -replace '\\', '/'
            }
        }
    }

    foreach ($file in $onDisk) {
        if ($readmeContent -notmatch [regex]::Escape($file)) {
            $issues.Add("SCRIPT NOT IN README: " + $file + " exists on disk but isn't mentioned in scripts/README.md")
        }
    }

    $readmeScriptRefs = [regex]::Matches($readmeContent, '\(((?:filters|actions|recipes|creators|commands|examples|context)/[^)]+\.cs)\)') |
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

Write-Host "`n=== 4. Skill coverage in entry documents ===" -ForegroundColor Cyan
# Adding a skill folder is not enough for anyone - human or agent - to find it. README.md's table and
# START-HERE.md's routing table are what a reader hits first, and check 2 can't see this: a link that
# resolves says nothing about a skill that was never linked at all.
# plugin.json/marketplace.json describe skills in prose ("HVAC terminal layout"), which no string check
# can verify - those are covered by the count check instead.
$skillNames = @($skillFiles | ForEach-Object { $_.Directory.Name })
$coverageTargets = @(
    @{ File = "README.md";     Label = "README.md skills table" },
    @{ File = "START-HERE.md"; Label = "START-HERE.md routing table" }
)
foreach ($target in $coverageTargets) {
    $targetPath = Join-Path $brainRoot $target.File
    if (-not (Test-Path $targetPath)) { continue }
    $content = [System.IO.File]::ReadAllText($targetPath, [System.Text.Encoding]::UTF8)
    foreach ($name in $skillNames) {
        if (-not $content.Contains($name)) {
            $issues.Add("SKILL NOT LISTED: '" + $name + "' exists in skills/ but isn't named in " + $target.Label + " (" + $target.File + ")")
        }
    }
}
# brain-log.md is deliberately excluded - its old counts are dated historical record, not claims about today.
foreach ($countFile in @("README.md", "SETUP.md", ".claude-plugin\plugin.json")) {
    $countPath = Join-Path $brainRoot $countFile
    if (-not (Test-Path $countPath)) { continue }
    $content = [System.IO.File]::ReadAllText($countPath, [System.Text.Encoding]::UTF8)
    foreach ($m in [regex]::Matches($content, '(\d+)\s+skills\b')) {
        if ([int]$m.Groups[1].Value -ne $skillNames.Count) {
            $issues.Add("SKILL COUNT DRIFT in " + $countFile + ": says `"" + $m.Value + "`" but skills/ holds " + $skillNames.Count)
        }
    }
}
Write-Host ("Checked {0} skill(s) against {1} entry document(s)." -f $skillNames.Count, $coverageTargets.Count)

Write-Host "`n=== 5. AGENT-SPEC fragment counts ===" -ForegroundColor Cyan
# AGENT-SPEC.md advertises itself as the complete self-contained reference, so a stale fragment count
# there is a wrong answer, not a cosmetic nit - it drifted to 206-vs-264 before this check existed.
$specPath = Join-Path $brainRoot "AGENT-SPEC.md"
if (Test-Path $specPath) {
    $specFlat = [regex]::Replace([System.IO.File]::ReadAllText($specPath, [System.Text.Encoding]::UTF8), '\s+', ' ')
    $countRe = '(\d+) real C# fragments in `scripts/` \((\d+) filters, (\d+) actions, (\d+) creators, (\d+) commands, (\d+) recipes, (\d+) examples, (\d+) read-only `context/` fragments, (\d+) shared'
    $specMatch = [regex]::Match($specFlat, $countRe)
    if (-not $specMatch.Success) {
        $issues.Add("AGENT-SPEC COUNT SENTENCE NOT FOUND: section 3.5's fragment-count sentence was reworded - re-anchor this check in tools/verify-consistency.* or the counts go unchecked")
    } else {
        $claimed = [ordered]@{
            total = [int]$specMatch.Groups[1].Value; filters = [int]$specMatch.Groups[2].Value
            actions = [int]$specMatch.Groups[3].Value; creators = [int]$specMatch.Groups[4].Value
            commands = [int]$specMatch.Groups[5].Value; recipes = [int]$specMatch.Groups[6].Value
            examples = [int]$specMatch.Groups[7].Value; context = [int]$specMatch.Groups[8].Value
            lib = [int]$specMatch.Groups[9].Value
        }
        $actual = [ordered]@{ total = 0 }
        foreach ($bucket in $subfolders) {
            $bucketPath = Join-Path $scriptsDir $bucket
            $n = 0
            if (Test-Path $bucketPath) { $n = @(Get-ChildItem -Path $bucketPath -Filter "*.cs" -Recurse).Count }
            $actual[$bucket] = $n
            $actual["total"] = [int]$actual["total"] + $n
        }
        foreach ($key in $claimed.Keys) {
            if ($claimed[$key] -ne $actual[$key]) {
                $issues.Add("AGENT-SPEC COUNT DRIFT (section 3.5): claims " + $claimed[$key] + " " + $key + ", disk has " + $actual[$key])
            }
        }
        Write-Host "Checked 8 fragment-count claim(s) in AGENT-SPEC.md section 3.5 against disk."
    }
} else {
    $issues.Add("MISSING FILE: AGENT-SPEC.md not found")
}

Write-Host "`n=== 6. Text encoding ===" -ForegroundColor Cyan
# Windows PowerShell 5.1 reads UTF-8-without-BOM as ANSI, so a scripted read-modify-write double-encodes
# every non-ASCII character in the file (see CLAUDE.md). That corrupted 41 files once, and a survivor
# went unnoticed for months inside a C# string literal, where a Replace() silently matched nothing and
# every round duct size sorted as 0.
#
# Two deliberate choices keep this check from becoming the very bug it hunts:
#   - Files are read with an EXPLICIT UTF-8 decoder, never Get-Content (which would itself misread a
#     UTF-8-no-BOM file as ANSI here and flag every em dash in the repo as corrupt).
#   - The characters are built from CODE POINTS, not typed literally, so this script stays pure ASCII
#     and can't be mis-parsed no matter how PowerShell decodes it.
$nonAsciiCodePoints = @(
    0x2014, 0x2013, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026, 0x2194, 0x2193, 0x2191, 0x2192, 0x2190,
    0x2713, 0x2714, 0x2717, 0x2022, 0x2248, 0x2265, 0x2264, 0x2122, 0x20AC,
    0x00A7, 0x00B0, 0x00B2, 0x00B3, 0x00B1, 0x00AB, 0x00BB, 0x00B7, 0x00BD, 0x00BC,
    0x00F8, 0x00D7, 0x00F7, 0x00E9, 0x00E8, 0x00E1, 0x00ED, 0x00F3, 0x00FA, 0x00FC, 0x00F1, 0x00E7,
    0x00E2, 0x00E4, 0x00E5, 0x00EE, 0x00F4, 0x00C5, 0x1F4C4
)
$cp1252 = [System.Text.Encoding]::GetEncoding(1252)
$mojibake = @()
foreach ($cp in $nonAsciiCodePoints) {
    $ch = [char]::ConvertFromUtf32($cp)
    # UTF-8 encode, then decode those same bytes as cp1252 - literally simulating the corruption.
    $corrupt = $cp1252.GetString([System.Text.Encoding]::UTF8.GetBytes($ch))
    # Undefined cp1252 bytes decode to U+FFFD; those have no stable corrupted form worth matching.
    if ($corrupt.Contains([char]0xFFFD)) { continue }
    $mojibake += $corrupt
}

$textExtensions = @(".md", ".cs", ".json", ".js", ".mjs", ".ps1")
# -Force is required: without it PowerShell treats dot-prefixed entries as hidden on Linux/macOS and
# silently skips .claude-plugin/plugin.json, .claude-plugin/marketplace.json, .claude/settings.json and
# .mcp.json - while scanning them on Windows, where "hidden" is a file attribute instead. The explicit
# .git/node_modules exclusion below is what keeps -Force from dragging those in.
$textFiles = Get-ChildItem -Path $brainRoot -Recurse -File -Force -ErrorAction SilentlyContinue | Where-Object {
    $textExtensions -contains $_.Extension -and
    $_.Name -ne "package-lock.json" -and
    $_.FullName -notmatch '[\\/](\.git|node_modules)[\\/]'
}
foreach ($file in $textFiles) {
    $lines = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8) -split '\r?\n'
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($seq in $mojibake) {
            if ($lines[$i].Contains($seq)) {
                $rel = $file.FullName.Substring($brainRoot.Length).TrimStart('\', '/')
                $issues.Add("MOJIBAKE in " + $rel + ":" + ($i + 1) + ": found '" + $seq + "' - a UTF-8 character double-encoded by an ANSI read-modify-write; restore the real character (see CLAUDE.md)")
                break
            }
        }
    }
}
Write-Host ("Checked {0} text file(s) for double-encoded characters." -f @($textFiles).Count)

Write-Host "`n=== 7. Cross-references inside script fragments ===" -ForegroundColor Cyan
# Fragment headers point at the knowledge file that explains them ("// SOURCE: ../../knowledge/..."),
# which is how an agent gets from a piece of code to the reasoning behind it. These are plain C#
# comments, NOT markdown links, so check 2 never saw them - and 19 were silently broken, every
# recipe/command/context/creator fragment using ../knowledge/ where its depth needed ../../knowledge/.
$fragRefCount = 0
foreach ($frag in Get-ChildItem -Path $scriptsDir -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue) {
    $content = [System.IO.File]::ReadAllText($frag.FullName, [System.Text.Encoding]::UTF8)
    foreach ($m in [regex]::Matches($content, '\.\.[./]*[\w./-]*\.md')) {
        $fragRefCount++
        $resolved = [System.IO.Path]::GetFullPath((Join-Path $frag.DirectoryName $m.Value))
        if (-not (Test-Path $resolved)) {
            $rel = $frag.FullName.Substring($brainRoot.Length).TrimStart('\', '/')
            $issues.Add("BROKEN FRAGMENT REF in " + $rel + ": '" + $m.Value + "' does not resolve (check the ../ depth for this file's folder)")
        }
    }
}
Write-Host ("Checked {0} cross-reference(s) inside script fragments." -f $fragRefCount)

Write-Host "`n=== 8. Semantic index coverage claims ===" -ForegroundColor Cyan
# CLAUDE.md, START-HERE.md and README.md each state how many files ask-brain-hybrid searches
# ("searches all N files"). That number drifted 306-vs-309 for two days before anyone noticed -
# three knowledge notes were added and no doc moved. The rules below mirror what the indexer
# actually includes (semantic-index/brain_common.py: INDEX_TARGETS + ROOT_DOCS + FILE_EXTENSIONS);
# if you change the covered set there, change this count AND the three docs in the same turn.
$indexable = 0
foreach ($folder in @("scripts", "knowledge", "skills")) {
    $folderPath = Join-Path $brainRoot $folder
    $indexable += (Get-ChildItem -Path $folderPath -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -eq ".md" -or $_.Extension -eq ".cs" }).Count
}
foreach ($rel in @("AGENT-SPEC.md", "START-HERE.md", "README.md", "SETUP.md", "CLAUDE.md",
                   "mcp-server\tools\README.md")) {
    if (Test-Path (Join-Path $brainRoot $rel)) { $indexable++ }
}
$claimCount = 0
foreach ($file in @("CLAUDE.md", "START-HERE.md", "README.md")) {
    $p = Join-Path $brainRoot $file
    if (-not (Test-Path $p)) { continue }
    $content = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)
    foreach ($m in [regex]::Matches($content, 'searches all (\d+) files')) {
        $claimCount++
        if ([int]$m.Groups[1].Value -ne $indexable) {
            $issues.Add("INDEX COUNT DRIFT in " + $file + ": says `"" + $m.Value + "`" but the indexable set on disk holds " + $indexable + " files")
        }
    }
}
Write-Host ("Checked {0} 'searches all N files' claim(s) against {1} indexable file(s) on disk." -f $claimCount, $indexable)

Write-Host "`n=== Result ===" -ForegroundColor Cyan
if ($issues.Count -eq 0) {
    Write-Host "All checks passed - no drift found." -ForegroundColor Green
    exit 0
} else {
    Write-Host ("{0} issue(s) found:" -f $issues.Count) -ForegroundColor Yellow
    foreach ($issue in $issues) { Write-Host ("  - " + $issue) -ForegroundColor Yellow }
    exit 1
}
