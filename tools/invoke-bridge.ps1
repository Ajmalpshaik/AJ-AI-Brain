<#
.SYNOPSIS
    Fast local caller for the AJ Tools AJ AI bridge.

.DESCRIPTION
    Use this only when the native MCP tool (`mcp__aj-tools-aj-ai__ping` /
    `mcp__aj-tools-aj-ai__run_csharp`) is not exposed in the current agent
    session. It reads the Revit-side discovery file, sends one C# snippet through
    the named pipe, prints the bridge JSON response, and exits.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-bridge.ps1 -Ping

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tools\invoke-bridge.ps1 -CodeFile .\tmp-query.cs
#>

[CmdletBinding(DefaultParameterSetName = "Code")]
param(
    [Parameter(ParameterSetName = "Ping")]
    [switch]$Ping,

    [Parameter(ParameterSetName = "Code")]
    [string]$Code,

    [Parameter(ParameterSetName = "Code")]
    [string]$CodeFile,

    [Parameter(ParameterSetName = "Code")]
    [switch]$AllowDestructive,

    [int]$ConnectTimeoutMs = 5000
)

$ErrorActionPreference = "Stop"

$discoveryFile = Join-Path $env:APPDATA "AJTools\ajai-bridge.json"
if (-not (Test-Path -LiteralPath $discoveryFile)) {
    throw "AJ AI Bridge is not connected. In Revit, open AJ AI and click Connect AJ AI Bridge, then try again."
}

$info = Get-Content -LiteralPath $discoveryFile -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($info.pipeName) -or [string]::IsNullOrWhiteSpace($info.token)) {
    throw "AJ AI Bridge connection file is malformed. Reconnect from the AJ AI pane in Revit."
}

if ($Ping) {
    $Code = '"pong"'
    $AllowDestructive = $false
} elseif (-not [string]::IsNullOrWhiteSpace($CodeFile)) {
    $Code = Get-Content -LiteralPath $CodeFile -Raw
} elseif ([string]::IsNullOrWhiteSpace($Code)) {
    throw "Provide -Ping, -Code, or -CodeFile."
}

$pipe = $null
$writer = $null
$reader = $null

try {
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(
        ".",
        [string]$info.pipeName,
        [System.IO.Pipes.PipeDirection]::InOut)

    $pipe.Connect($ConnectTimeoutMs)

    # No-BOM UTF8 on purpose: [System.Text.Encoding]::UTF8 makes StreamWriter emit a 3-byte BOM at the
    # start of the stream, which the proven Node client (mcp-server/bridge-connection.js) never sends —
    # if the Revit-side listener parses raw bytes rather than BOM-stripping, that BOM lands in front of
    # the first JSON request. Matching the Node client byte-for-byte removes the difference.
    $writer = [System.IO.StreamWriter]::new($pipe, [System.Text.UTF8Encoding]::new($false))
    $writer.AutoFlush = $true
    $reader = [System.IO.StreamReader]::new($pipe, [System.Text.Encoding]::UTF8)

    $request = @{
        token = [string]$info.token
        code = $Code
        allowDestructive = [bool]$AllowDestructive
    } | ConvertTo-Json -Compress

    $writer.WriteLine($request)
    $reader.ReadLine()
}
finally {
    foreach ($obj in @($reader, $writer, $pipe)) {
        if ($null -ne $obj) {
            try { $obj.Dispose() } catch {}
        }
    }
}
