param([string]$LuaPath)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $LuaPath) {
    $installedLua = Get-Command lua -ErrorAction SilentlyContinue
    if ($installedLua) {
        $LuaPath = $installedLua.Source
    } else {
        $LuaPath = Join-Path $repositoryRoot 'artifacts\lua-test-runtime\node_modules\.bin\fengari.cmd'
        if (-not (Test-Path -LiteralPath $LuaPath)) {
            throw 'Lua 5.1+ wird benoetigt. Interpreter mit -LuaPath angeben; siehe addon/FishHelper/README.md.'
        }
    }
}

Push-Location $repositoryRoot
try {
    & $LuaPath 'Tests/FishHelper.Tests.lua' 'addon/FishHelper/FishHelper.lua'
    if ($LASTEXITCODE -ne 0) { throw "FishHelper-Tests fehlgeschlagen (Exitcode $LASTEXITCODE)." }
} finally {
    Pop-Location
}
