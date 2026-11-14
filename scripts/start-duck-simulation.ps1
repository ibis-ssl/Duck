# .SYNOPSIS
# Prepares and starts the normal Duck base simulation with the local Docker engine.
#
# .DESCRIPTION
# Session mode is the default and lets Aspire remove the containers when AppHost exits.
# Use -Persistent to retain containers for a later run with the same stable stack ID.
# Use -StopPersistent with that same ID to remove only its exact owner/stack/run containers.
# Game Controller match setup remains a manual step in the UI.
#
# .EXAMPLE
# .\scripts\start-duck-simulation.ps1
#
# .EXAMPLE
# .\scripts\start-duck-simulation.ps1 -Persistent -PersistentStackId local-dev
# .\scripts\start-duck-simulation.ps1 -Persistent -PersistentStackId local-dev
# .\scripts\start-duck-simulation.ps1 -StopPersistent -PersistentStackId local-dev
[CmdletBinding(DefaultParameterSetName = 'Start')]
param(
    [Parameter(ParameterSetName = 'Start')]
    [switch] $Persistent,

    [Parameter(ParameterSetName = 'Start')]
    [Parameter(ParameterSetName = 'StopPersistent', Mandatory = $true)]
    [string] $PersistentStackId = 'duck-local-development',

    [Parameter(ParameterSetName = 'StopPersistent', Mandatory = $true)]
    [switch] $StopPersistent
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$applicationHostProject = 'Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj'
$applicationHostExecutable = Join-Path $repositoryRoot 'Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.exe'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$dockerDesktopCli = Join-Path $env:LOCALAPPDATA 'Programs/DockerDesktop/resources/bin/docker.exe'
$dockerCommand = if (Test-Path -LiteralPath $dockerDesktopCli) {
    $dockerDesktopCli
} else {
    (Get-Command docker -ErrorAction SilentlyContinue | Select-Object -First 1).Source
}

if ([string]::IsNullOrWhiteSpace($dockerCommand)) {
    throw 'Docker CLI was not found. Start the already-installed Docker Desktop and ensure its CLI is available.'
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)][string] $Executable,
        [Parameter(Mandatory = $true)][string[]] $Arguments,
        [Parameter(Mandatory = $true)][string] $Step
    )

    Write-Host "==> $Step"
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

function Test-LinuxX64Executable {
    param([Parameter(Mandatory = $true)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $bytes = [IO.File]::ReadAllBytes($Path)
    return $bytes.Length -ge 20 -and
        $bytes[0] -eq 0x7f -and $bytes[1] -eq 0x45 -and
        $bytes[2] -eq 0x4c -and $bytes[3] -eq 0x46 -and
        $bytes[4] -eq 2 -and $bytes[5] -eq 1 -and
        [BitConverter]::ToUInt16($bytes, 18) -eq 62
}

function Get-NativeHelperSourceFiles {
    param([Parameter(Mandatory = $true)][string] $NativeSourceRoot)

    $sourceNames = @(
        'transport_core.c',
        'transport_control_frontend.c',
        'transport_path_policy.c',
        'transport_server_posix.c',
        'control_main_posix.c'
    )
    $files = foreach ($name in $sourceNames) {
        Join-Path $NativeSourceRoot $name
    }
    $files += Get-ChildItem -LiteralPath $NativeSourceRoot -Filter '*.h' -File |
        Select-Object -ExpandProperty FullName
    return @($files | ForEach-Object { Get-Item -LiteralPath $_ })
}

function Ensure-Cm4TransportHelper {
    $nativeRoot = Join-Path $repositoryRoot 'native/cm4-transport'
    $intermediateDirectory = Join-Path $repositoryRoot 'Testing/Duck.Testing.AppHost/obj/Debug/net10.0'
    $intermediateHelper = Join-Path $intermediateDirectory 'cm4-transport-control'
    $existingAppHostHelper = Join-Path $repositoryRoot 'Testing/Duck.Testing.AppHost/bin/Debug/net10.0/cm4-transport-control'
    $sources = Get-NativeHelperSourceFiles -NativeSourceRoot $nativeRoot
    $latestSourceUtc = ($sources | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum

    New-Item -ItemType Directory -Path $intermediateDirectory -Force | Out-Null
    if ((Test-LinuxX64Executable -Path $intermediateHelper) -and
        (Get-Item -LiteralPath $intermediateHelper).LastWriteTimeUtc -ge $latestSourceUtc) {
        return $intermediateHelper
    }

    if ((Test-LinuxX64Executable -Path $existingAppHostHelper) -and
        (Get-Item -LiteralPath $existingAppHostHelper).LastWriteTimeUtc -ge $latestSourceUtc) {
        Copy-Item -LiteralPath $existingAppHostHelper -Destination $intermediateHelper -Force
        return $intermediateHelper
    }

    $wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $wsl) {
        throw 'The CM4 helper is missing or stale. An existing WSL distribution with a C compiler is needed; no software will be installed.'
    }

    $repositoryLinuxPath = (& $wsl.Source --exec wslpath -a $repositoryRoot | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryLinuxPath)) {
        throw 'Could not map the repository path into the existing WSL distribution.'
    }
    $helperLinuxPath = (& $wsl.Source --exec wslpath -a $intermediateHelper | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($helperLinuxPath)) {
        throw 'Could not map the CM4 helper output path into the existing WSL distribution.'
    }

    $buildCommand = @'
set -eu
repository="$1"
output="$2"
cd "$repository"
command -v cc >/dev/null 2>&1 || { echo 'No C compiler is available in the existing WSL distribution; no installation was attempted.' >&2; exit 127; }
cc -std=c11 -O2 -Wall -Wextra -Werror -pedantic -static \
  -I native/cm4-transport \
  native/cm4-transport/transport_core.c \
  native/cm4-transport/transport_control_frontend.c \
  native/cm4-transport/transport_path_policy.c \
  native/cm4-transport/transport_server_posix.c \
  native/cm4-transport/control_main_posix.c \
  -o "$output"
'@
    Invoke-CheckedCommand -Executable $wsl.Source -Arguments @(
        '--exec', 'sh', '-c', $buildCommand, 'duck-cm4-helper', $repositoryLinuxPath, $helperLinuxPath
    ) -Step 'Build the Linux CM4 transport helper using the existing WSL toolchain'

    if (-not (Test-LinuxX64Executable -Path $intermediateHelper)) {
        throw 'The CM4 helper build did not produce a Linux x86-64 executable.'
    }
    return $intermediateHelper
}

if ($StopPersistent) {
    if ($PersistentStackId -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$') {
        throw 'PersistentStackId must be a 1-64 character Docker-safe name.'
    }
    $persistentRunId = $PersistentStackId
    $containerIds = @(& $dockerCommand ps -aq --no-trunc `
        --filter "label=duck.aspire.stack=$PersistentStackId" `
        --filter "label=duck.aspire.run=$persistentRunId")
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not list containers for the selected persistent Duck stack.'
    }

    $verifiedContainerIds = [Collections.Generic.List[string]]::new()
    foreach ($containerId in $containerIds) {
        $containerId = $containerId.Trim()
        if ([string]::IsNullOrWhiteSpace($containerId)) { continue }
        $inspection = & $dockerCommand inspect --format '{{json .Config.Labels}}' $containerId
        if ($LASTEXITCODE -ne 0) { throw "Could not verify container $containerId; it was left untouched." }
        $labels = $inspection | ConvertFrom-Json
        if ($labels.'duck.aspire.owner' -ne 'duck-apphost' -or
            $labels.'duck.aspire.stack' -ne $PersistentStackId -or
            $labels.'duck.aspire.run' -ne $persistentRunId) {
            throw "Container $containerId did not match the exact persistent stack and run labels; it was left untouched."
        }
        $verifiedContainerIds.Add($containerId)
    }

    foreach ($containerId in $verifiedContainerIds) {
        & $dockerCommand rm -f $containerId
        if ($LASTEXITCODE -ne 0) { throw "Could not remove selected persistent container $containerId." }
    }

    $remaining = @(& $dockerCommand ps -aq --no-trunc `
        --filter "label=duck.aspire.stack=$PersistentStackId" `
        --filter "label=duck.aspire.run=$persistentRunId")
    if ($LASTEXITCODE -ne 0 -or $remaining.Count -gt 0) {
        throw 'The selected persistent Duck stack still has containers after cleanup.'
    }
    Write-Host "Removed only containers with stack/run ID '$PersistentStackId'. Docker images were left untouched."
    exit 0
}

$sdkList = & $dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not ($sdkList -match '(^|\r?\n)10\.0\.')) {
    throw '.NET SDK 10.0 is required. The launcher does not install software.'
}

if ($Persistent) {
    if ($PersistentStackId -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$') {
        throw 'PersistentStackId must be a 1-64 character Docker-safe name.'
    }
    $stackId = $PersistentStackId
    $runId = $PersistentStackId
    $lifetimeMode = 'persistent'
} else {
    $stackId = 'local-{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
    $runId = $stackId
    $lifetimeMode = 'session'
}

$artifactDirectory = Join-Path $repositoryRoot (Join-Path 'artifacts/duck-simulation' $stackId)
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$launchId = '{0}-{1}' -f [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$fixtureManifest = Join-Path $artifactDirectory "crane-fixture-$launchId.json"
$diagnosticsPath = Join-Path $artifactDirectory "crane-probe-$launchId.jsonl"
$safeTracePath = Join-Path $artifactDirectory "identity-handoff-$launchId.jsonl"
$stackLock = Join-Path ([IO.Path]::GetTempPath()) 'duck-aspire-stack.lock'

[IO.File]::WriteAllLines((Join-Path $artifactDirectory "startup-mode-$launchId.txt"), @(
    "mode=$lifetimeMode",
    "stack_id=$stackId",
    "run_id=$runId",
    "started_at_utc=$([DateTime]::UtcNow.ToString('o'))",
    'testing_mode=base',
    'game_controller_ui=manual'
), (New-Object Text.UTF8Encoding($false)))

$environment = [ordered]@{
    'Testing__Mode' = 'base'
    'Testing__MatchReadinessOnly' = 'false'
    'Testing__ContainerMode' = 'true'
    'RefereeDriver__Enabled' = 'true'
    'Testing__IdentityControl__DesktopBridgeEnabled' = 'true'
    'Testing__DebugHost__VisionObserverEnabled' = 'true'
    'Testing__ContainerLifetime__Mode' = $lifetimeMode
    'Testing__StackOwnership__StackId' = $stackId
    'Testing__StackOwnership__LockPath' = $stackLock
    'Testing__Diagnostics__RunId' = $runId
    'Testing__Crane__FixtureManifestPath' = $fixtureManifest
    'Testing__Crane__DiagnosticsPath' = $diagnosticsPath
    'Testing__IdentityControl__SafeTracePath' = $safeTracePath
    'DUCK_ASPIRE_STACK_ID' = $stackId
    'DUCK_ASPIRE_FIXTURE_MANIFEST' = $fixtureManifest
    'Testing__IdentityControl__DockerContext' = $null
    'Testing__IdentityControl__ExpectedEngineId' = $null
    'Testing__IdentityControl__DockerExecutable' = $null
}
$previousEnvironment = @{}
$workingDirectoryChanged = $false

Push-Location $repositoryRoot
$workingDirectoryChanged = $true
try {
    foreach ($name in $environment.Keys) {
        $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
    }

    Ensure-Cm4TransportHelper | Out-Null
    Invoke-CheckedCommand -Executable $dotnet -Arguments @(
        'publish', 'Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj', '--configuration', 'Release', '-m:1', '/nr:false'
    ) -Step 'Publish the Duck runtime container context'
    Invoke-CheckedCommand -Executable $dotnet -Arguments @(
        'publish', 'Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj', '--configuration', 'Release', '-m:1', '/nr:false'
    ) -Step 'Publish the Vision observer container context'
    Invoke-CheckedCommand -Executable $dotnet -Arguments @(
        'publish', 'Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj', '--configuration', 'Release', '-m:1', '/nr:false'
    ) -Step 'Publish the RefereeDriver container context'
    Invoke-CheckedCommand -Executable $dotnet -Arguments @(
        'build', $applicationHostProject, '--configuration', 'Debug', '-m:1', '/nr:false'
    ) -Step 'Build AppHost and its Linux Crane launch overlay'

    foreach ($expectedAsset in @(
        'Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll',
        'Tracker/Tracker.DebugHost/bin/Release/net10.0/publish/Tracker.DebugHost.dll',
        'Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish/Duck.Testing.RefereeDriver.Host.dll',
        'Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.exe',
        'Testing/Duck.Testing.AppHost/bin/Debug/net10.0/cm4-transport-control',
        'Testing/Duck.Testing.AppHost/bin/Debug/net10.0/crane-launch-overlay'
    )) {
        if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $expectedAsset) -PathType Leaf)) {
            throw "Startup preparation did not produce required asset '$expectedAsset'."
        }
    }

    Write-Host "Starting Duck base simulation stack '$stackId' ($lifetimeMode lifetime)."
    Write-Host 'Docker context and Engine ID are resolved by AppHost; no match settings or browser actions are automated.'
    $appHostLogPath = Join-Path $artifactDirectory "apphost-$launchId.stdout-stderr.log"
    $appHostTranscriptStarted = $false
    try {
        Start-Transcript -LiteralPath $appHostLogPath -Force | Out-Null
        $appHostTranscriptStarted = $true
        & $applicationHostExecutable
        $appHostExitCode = $LASTEXITCODE
    } finally {
        if ($appHostTranscriptStarted) {
            Stop-Transcript | Out-Null
        }
    }
    if ($appHostExitCode -ne 0) {
        throw "AppHost exited with code $appHostExitCode. See '$artifactDirectory'."
    }

    if ($Persistent) {
        Write-Host "Persistent stack '$stackId' remains available for reuse. Re-run with -Persistent -PersistentStackId '$stackId'."
        Write-Host "Remove only this stack with -StopPersistent -PersistentStackId '$stackId'."
    } else {
        Write-Host "AppHost ended in Session mode; Aspire owns teardown for stack '$stackId'."
    }
} finally {
    foreach ($name in $environment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
    if ($workingDirectoryChanged) {
        Pop-Location
    }
}
