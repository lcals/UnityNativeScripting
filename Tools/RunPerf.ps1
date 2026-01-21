param(
    [int]$Bots = 1000,
    [int]$Frames = 3000,
    [double]$Dt = 0.0166667,

    [string]$OutFile = "",
    [string]$Tag = "",

    [switch]$NoReadme,
    [string]$ReadmeFile = "",
    [int]$ReadmeMaxRows = 20,
    [switch]$UpdateReadmeOnly,

    [string]$UnityVersion = "",
    [string]$UnityExe = "",
    [switch]$NoUnity,
    [switch]$NoUnityEditMode,
    [switch]$NoUnityIl2cpp,

    [switch]$NoBuild
)

Set-StrictMode -Version Latest

$ErrorActionPreference = "Stop"

try
{
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding = [System.Text.Encoding]::UTF8
}
catch
{
}

function New-Dir([string]$Path)
{
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Wait-FileUpdated(
    [string]$Path,
    [DateTime]$NotBeforeUtc,
    [int]$TimeoutSeconds = 600,
    [int]$MinLength = 1
)
{
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline)
    {
        if (Test-Path $Path)
        {
            try
            {
                $fi = Get-Item $Path
                if ($fi.Length -ge $MinLength -and $fi.LastWriteTimeUtc -ge $NotBeforeUtc)
                {
                    return $true
                }
            }
            catch
            {
            }
        }
        Start-Sleep -Milliseconds 500
    }
    return $false
}

function Try-Get([scriptblock]$Thunk)
{
    try { return & $Thunk } catch { return $null }
}

function Invoke-External(
    [string]$Name,
    [string]$FilePath,
    [string[]]$ArgumentList,
    [string]$WorkDir,
    [string]$OutputFile
)
{
    $started = Get-Date
    $exitCode = $null
    try
    {
        Write-Host "==> $Name"
        Push-Location $WorkDir
        if (-not [string]::IsNullOrWhiteSpace($OutputFile))
        {
            New-Dir (Split-Path -Parent $OutputFile)
            if (Test-Path $OutputFile) { Remove-Item -Force $OutputFile }
            & $FilePath @ArgumentList 2>&1 | Tee-Object -FilePath $OutputFile | Out-Host
        }
        else
        {
            & $FilePath @ArgumentList 2>&1 | Out-Host
        }
        $exitCode = $LASTEXITCODE
        Pop-Location
        Write-Host "<= $Name exitCode=$exitCode"

        return [ordered]@{
            ok         = ($exitCode -eq 0)
            name       = $Name
            file       = $FilePath
            args       = $ArgumentList
            workDir    = $WorkDir
            exitCode   = $exitCode
            startedUtc = $started.ToUniversalTime().ToString("o")
            endedUtc   = (Get-Date).ToUniversalTime().ToString("o")
            outputFile = $OutputFile
        }
    }
    catch
    {
        try { Pop-Location } catch { }
        $err = $_.Exception.ToString()
        if (-not [string]::IsNullOrWhiteSpace($OutputFile))
        {
            Set-Content -Path $OutputFile -Value $err -Encoding UTF8
        }
        return [ordered]@{
            ok         = $false
            name       = $Name
            file       = $FilePath
            args       = $ArgumentList
            workDir    = $WorkDir
            exitCode   = $exitCode
            startedUtc = $started.ToUniversalTime().ToString("o")
            endedUtc   = (Get-Date).ToUniversalTime().ToString("o")
            outputFile = $OutputFile
            error      = $err
        }
    }
}

function Find-UnityExe()
{
    if (-not [string]::IsNullOrWhiteSpace($UnityExe) -and (Test-Path $UnityExe))
    {
        return (Resolve-Path $UnityExe).Path
    }

    $hub = "C:\Program Files\Unity\Hub\Editor"
    if (-not (Test-Path $hub))
    {
        return $null
    }

    $candidates = Get-ChildItem $hub -Directory | ForEach-Object {
        $exe = Join-Path $_.FullName 'Editor\Unity.exe'
        if (Test-Path $exe)
        {
            [ordered]@{
                version = $_.Name
                exe     = $exe
            }
        }
    } | Where-Object { $_ -ne $null }

    if (-not $candidates -or $candidates.Count -eq 0)
    {
        return $null
    }

    if (-not [string]::IsNullOrWhiteSpace($UnityVersion))
    {
        $match = $candidates | Where-Object { $_.version -eq $UnityVersion } | Select-Object -First 1
        if ($match) { return $match.exe }
    }

    $testHasIl2cpp = {
        param([string]$unityExePath)
        try
        {
            $editorDir = Split-Path -Parent $unityExePath # ...\<ver>\Editor
            $variations = @(
                Join-Path $editorDir "Data\\PlaybackEngines\\windowsstandalonesupport\\Variations"
                Join-Path $editorDir "Data\\PlaybackEngines\\WindowsStandaloneSupport\\Variations"
            )

            foreach ($v in $variations)
            {
                if (Test-Path $v)
                {
                    if (Test-Path (Join-Path $v "il2cpp")) { return $true }
                    $any = Get-ChildItem $v -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "il2cpp" } | Select-Object -First 1
                    if ($any) { return $true }
                }
            }
        }
        catch
        {
        }
        return $false
    }

    $preferred = $candidates | Where-Object { $_.version -eq "6000.0.40f1" } | Select-Object -First 1
    if ($preferred)
    {
        if ($NoUnityIl2cpp -or (& $testHasIl2cpp $preferred.exe))
        {
            return $preferred.exe
        }
    }

    if (-not $NoUnityIl2cpp)
    {
        # Prefer an Editor that actually has Windows IL2CPP variations installed.
        $withIl2cpp = $candidates | Where-Object { & $testHasIl2cpp $_.exe } | Sort-Object { $_.version } | Select-Object -Last 1
        if ($withIl2cpp) { return $withIl2cpp.exe }
    }

    return (($candidates | Sort-Object { $_.version } | Select-Object -Last 1).exe)
}

function Get-UnityIl2cppBuildFailureReason([string]$LogPath)
{
    if (-not (Test-Path $LogPath))
    {
        return $null
    }

    $tail = (Get-Content $LogPath -Tail 400) -join "`n"
    if ([string]::IsNullOrWhiteSpace($tail))
    {
        return $null
    }

    if ($tail -like "*IL2CPP) is not installed*")
    {
        return "IL2CPP module is not installed (Unity Hub -> Install -> Windows Build Support (IL2CPP))."
    }

    if ($tail -like "*Build Finished, Result: Failure*")
    {
        return "Unity player build failed (see build log)."
    }

    if ($tail -like "*Error building Player*")
    {
        return "Unity error building player (see build log)."
    }

    if ($tail -like "*Project has invalid dependencies:*")
    {
        return "Unity package resolution failed (Project has invalid dependencies; see build log)."
    }

    return $null
}

function Get-UnityProjectLockReason([string]$ConsoleOutputPath)
{
    if (-not (Test-Path $ConsoleOutputPath))
    {
        return $null
    }

    $text = Get-Content $ConsoleOutputPath -Raw
    if ([string]::IsNullOrWhiteSpace($text))
    {
        return $null
    }

    if ($text -like "*another Unity instance is running with this project open*")
    {
        return "Unity project is already open (close the Editor instance for Tests/unity, or run on a separate project copy)."
    }

    return $null
}

function Parse-RobotHostOutput([string]$Text)
{
    $getInt = {
        param([string]$pattern)
        $m = [regex]::Match($Text, $pattern)
        if ($m.Success) { return [long]$m.Groups[1].Value }
        return $null
    }
    $getDouble = {
        param([string]$pattern)
        $m = [regex]::Match($Text, $pattern)
        if ($m.Success) { return [double]$m.Groups[1].Value }
        return $null
    }

    return [ordered]@{
        elapsed_s       = & $getDouble "\[all\] elapsed: ([0-9.]+) s"
        alloc_bytes     = & $getInt "\[all\] allocated \(thread\): (-?\d+) bytes"
        total_commands  = & $getInt "\[all\] total commands handled: (\d+)"
        commands_per_s  = & $getDouble "\[all\] commands/sec: (\d+)"
        asset_requests  = & $getInt "\[all\] total asset requests: (\d+)"
        logs            = & $getInt "\[all\] total logs: (\d+)"
        spawns          = & $getInt "\[all\] total spawns: (\d+)"
        transforms      = & $getInt "\[all\] total transforms: (\d+)"
        destroys        = & $getInt "\[all\] total destroys: (\d+)"
    }
}

function Parse-RobotRunnerOutput([string]$Text)
{
    $getInt = {
        param([string]$pattern)
        $m = [regex]::Match($Text, $pattern)
        if ($m.Success) { return [long]$m.Groups[1].Value }
        return $null
    }
    $getDouble = {
        param([string]$pattern)
        $m = [regex]::Match($Text, $pattern)
        if ($m.Success) { return [double]$m.Groups[1].Value }
        return $null
    }

    return [ordered]@{
        elapsed_s      = & $getDouble "elapsed: ([0-9.]+) s"
        total_commands = & $getInt "total commands parsed: (\d+)"
        commands_per_s = & $getDouble "commands/sec: (\d+)"
        ticks          = & $getInt "ticks: (\d+)"
        asset_requests = & $getInt "total asset requests: (\d+)"
    }
}

function Parse-UnityEditModePerf([string]$PerfJsonPath)
{
    if (-not (Test-Path $PerfJsonPath))
    {
        return $null
    }

    $json = Get-Content $PerfJsonPath -Raw | ConvertFrom-Json
    $results = @()

    foreach ($r in ($json.Results | Where-Object { $_ -ne $null }))
    {
        $time = $r.SampleGroups | Where-Object Name -eq "Time" | Select-Object -First 1
        $gc = $r.SampleGroups | Where-Object Name -eq "GC.Alloc.Bytes" | Select-Object -First 1

        $results += [ordered]@{
            name           = $r.Name
            time_avg_ms    = Try-Get { [double]$time.Average }
            time_median_ms = Try-Get { [double]$time.Median }
            gc_avg_bytes   = Try-Get { [double]$gc.Average }
        }
    }

    return [ordered]@{
        testSuite = $json.TestSuite
        editor    = $json.Editor
        hardware  = $json.Hardware
        results   = $results
    }
}

function Parse-BridgePerfLog([string]$LogPath)
{
    if (-not (Test-Path $LogPath))
    {
        return $null
    }

    $lines = Get-Content $LogPath
    $re = [regex]::new("##bridgeperf: mode=(\S+) ticks_per_sec=([0-9.]+) mib_per_sec=([0-9.]+) bots=(\d+) frames=(\d+) elapsed_ms=([0-9.]+) alloc_bytes=(-?\d+) total_bytes=(\d+)")

    $items = @{}
    foreach ($line in $lines)
    {
        $m = $re.Match($line)
        if (-not $m.Success) { continue }

        $bots = [int]$m.Groups[4].Value
        if ($items.ContainsKey($bots)) { continue }

        $items[$bots] = [ordered]@{
            mode         = $m.Groups[1].Value
            ticks_per_s  = [double]$m.Groups[2].Value
            mib_per_s    = [double]$m.Groups[3].Value
            bots         = $bots
            frames       = [int]$m.Groups[5].Value
            elapsed_ms   = [double]$m.Groups[6].Value
            alloc_bytes  = [long]$m.Groups[7].Value
            total_bytes  = [long]$m.Groups[8].Value
        }
    }

    return ($items.Values | Sort-Object bots)
}

function Update-PerfReadme(
    [string]$ReadmePath,
    [string]$HistoryPath,
    [int]$MaxRows
)
{
    if ([string]::IsNullOrWhiteSpace($ReadmePath))
    {
        return
    }

    if (-not (Test-Path $ReadmePath))
    {
        return
    }

    $text = Get-Content $ReadmePath -Raw
    $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

    $start = "<!-- PERF_TABLE_START -->"
    $end = "<!-- PERF_TABLE_END -->"

    if (-not $text.Contains($start) -or -not $text.Contains($end))
    {
        $section = @(
            "### 性能摘要（自动追加）",
            "",
            "下表由 `Tools/RunPerf.ps1` 自动追加（更完整的数据仍以 `build/perf_history.jsonl` 为准）。",
            "",
            $start,
            "| tsUtc | runId | tag | git | robothost_null cmd/s | robot_runner cmd/s | il2cpp_source 1k ticks/s | il2cpp_source 10k ticks/s |",
            "|---|---|---|---|---:|---:|---:|---:|",
            $end,
            ""
        ) -join $nl

        $text = $text.TrimEnd() + ($nl + $nl) + $section
    }

    $tableHeader = "| tsUtc | runId | tag | git | robothost_null cmd/s | robot_runner cmd/s | il2cpp_source 1k ticks/s | il2cpp_source 10k ticks/s |"
    $tableSep = "|---|---|---|---|---:|---:|---:|---:|"

    $rows = New-Object System.Collections.Generic.List[string]

    if (Test-Path $HistoryPath)
    {
        $tailCount = [Math]::Max(1, [Math]::Min(5000, $MaxRows * 50))
        $lines = Get-Content $HistoryPath -Tail $tailCount

        $seen = New-Object System.Collections.Generic.HashSet[string]
        for ($i = $lines.Count - 1; $i -ge 0; $i--)
        {
            if ($rows.Count -ge $MaxRows) { break }

            $line = $lines[$i]
            if ([string]::IsNullOrWhiteSpace($line)) { continue }

            $rec = $null
            try { $rec = $line | ConvertFrom-Json } catch { continue }
            if (-not $rec) { continue }

            $runId = Try-Get { [string]$rec.runId }
            if ([string]::IsNullOrWhiteSpace($runId)) { continue }
            if (-not $seen.Add($runId)) { continue }

            $tsUtcObj = Try-Get { $rec.tsUtc }
            $tsUtc = $null
            if ($tsUtcObj -is [DateTime])
            {
                $tsUtc = $tsUtcObj.ToUniversalTime().ToString("o")
            }
            else
            {
                $tsUtc = Try-Get { [string]$tsUtcObj }
            }
            if ([string]::IsNullOrWhiteSpace($tsUtc)) { $tsUtc = "n/a" }

            $tag = Try-Get { [string]$rec.tag }
            if ([string]::IsNullOrWhiteSpace($tag)) { $tag = "" }

            $commit = Try-Get { [string]$rec.git.commit }
            if (-not [string]::IsNullOrWhiteSpace($commit) -and $commit.Length -gt 7) { $commit = $commit.Substring(0, 7) }
            if ([string]::IsNullOrWhiteSpace($commit)) { $commit = "n/a" }
            $dirty = Try-Get { [bool]$rec.git.dirty }
            if ($dirty) { $commit = "$commit*" }

            $rhNull = Try-Get { [double]$rec.results.robothost_null.commands_per_s }
            $rr = Try-Get { [double]$rec.results.robot_runner.commands_per_s }

            $il2 = Try-Get { $rec.results.unity_il2cpp_source }
            $il2_1k = $null
            $il2_10k = $null
            try
            {
                if ($il2 -is [System.Array])
                {
                    $il2_1k = ($il2 | Where-Object bots -eq 1000 | Select-Object -First 1).ticks_per_s
                    $il2_10k = ($il2 | Where-Object bots -eq 10000 | Select-Object -First 1).ticks_per_s
                }
            }
            catch
            {
            }

            $fmt0 = { param($v) if ($null -eq $v) { "n/a" } else { "{0:0}" -f $v } }
            $fmt2 = { param($v) if ($null -eq $v) { "n/a" } else { "{0:0.00}" -f $v } }

            $rows.Add(@(
                    "| $tsUtc | $runId | $tag | $commit |",
                    " $(& $fmt0 $rhNull) |",
                    " $(& $fmt0 $rr) |",
                    " $(& $fmt2 $il2_1k) |",
                    " $(& $fmt2 $il2_10k) |"
                ) -join "")
        }
    }

    $blockLines = New-Object System.Collections.Generic.List[string]
    $blockLines.Add($start)
    $blockLines.Add($tableHeader)
    $blockLines.Add($tableSep)
    foreach ($r in $rows) { $blockLines.Add($r) }
    $blockLines.Add($end)

    $block = ($blockLines -join $nl)
    $pattern = "(?s)" + [regex]::Escape($start) + ".*?" + [regex]::Escape($end)
    if ([regex]::IsMatch($text, $pattern))
    {
        $text = [regex]::Replace($text, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $block }, 1)
    }
    else
    {
        $text = $text.TrimEnd() + ($nl + $nl) + $block + $nl
    }

    Set-Content -Path $ReadmePath -Value $text -Encoding UTF8
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

if ([string]::IsNullOrWhiteSpace($OutFile))
{
    $OutFile = Join-Path $repoRoot "build\\perf_history.jsonl"
}

$readmePath = $null
if (-not $NoReadme)
{
    if ([string]::IsNullOrWhiteSpace($ReadmeFile))
    {
        $readmePath = Join-Path $repoRoot "README.md"
    }
    else
    {
        $readmePath = (Resolve-Path $ReadmeFile).Path
    }
}

if ($UpdateReadmeOnly)
{
    if ($readmePath)
    {
        Update-PerfReadme -ReadmePath $readmePath -HistoryPath $OutFile -MaxRows $ReadmeMaxRows
        Write-Host "Updated: $readmePath"
    }
    else
    {
        Write-Host "README update disabled (-NoReadme)"
    }
    exit 0
}

$runId = (Get-Date -Format "yyyyMMdd_HHmmss")
$runDir = Join-Path $repoRoot ("build\\perf_runs\\$runId")
New-Dir $runDir
New-Dir (Split-Path $OutFile)

Write-Host "RunId: $runId"
Write-Host "RunDir: $runDir"
Write-Host "OutFile: $OutFile"

$git = $null
if (Get-Command git -ErrorAction SilentlyContinue)
{
    try
    {
        Push-Location $repoRoot
        $commit = (& git rev-parse HEAD 2>$null).Trim()
        $branch = (& git rev-parse --abbrev-ref HEAD 2>$null).Trim()
        $dirty = ((& git status --porcelain 2>$null) | Measure-Object).Count -gt 0
        Pop-Location
        $git = [ordered]@{ commit = $commit; branch = $branch; dirty = $dirty }
    }
    catch
    {
        try { Pop-Location } catch { }
    }
}

$machine = [ordered]@{
    computerName = $env:COMPUTERNAME
    osVersion    = [System.Environment]::OSVersion.VersionString
    dotnet       = Try-Get { (& dotnet --version 2>$null).Trim() }
    cmake        = Try-Get { ((& cmake --version 2>$null) | Select-Object -First 1).Trim() }
    cpu          = Try-Get { (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim() }
}

$steps = [ordered]@{}
$results = [ordered]@{}

if (-not $NoBuild)
{
    if (-not (Test-Path (Join-Path $repoRoot "build\\CMakeCache.txt")))
    {
        $steps.cmake_configure = Invoke-External `
            -Name "cmake_configure" `
            -FilePath "cmake" `
            -ArgumentList @("-S", ".", "-B", "build", "-DCMAKE_BUILD_TYPE=Release") `
            -WorkDir $repoRoot `
            -OutputFile (Join-Path $runDir "cmake_configure.log")
    }

    $steps.cmake_build = Invoke-External `
        -Name "cmake_build" `
        -FilePath "cmake" `
        -ArgumentList @("--build", "build", "--config", "Release") `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $runDir "cmake_build.log")

    $steps.dotnet_build_bridgecore = Invoke-External `
        -Name "dotnet_build_bridgecore" `
        -FilePath "dotnet" `
        -ArgumentList @("build", "Core\\csharp\\Bridge.Core\\Bridge.Core.csproj", "-c", "Release") `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $runDir "dotnet_build_bridgecore.log")

    $steps.dotnet_build_robothost = Invoke-External `
        -Name "dotnet_build_robothost" `
        -FilePath "dotnet" `
        -ArgumentList @("build", "Tests\\csharp\\RobotHost\\RobotHost.csproj", "-c", "Release") `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $runDir "dotnet_build_robothost.log")
}

$steps.robothost_null = Invoke-External `
    -Name "robothost_null" `
    -FilePath "dotnet" `
    -ArgumentList @("run", "--project", "Tests\\csharp\\RobotHost\\RobotHost.csproj", "-c", "Release", "--", "$Bots", "$Frames", "$Dt", "--host", "null") `
    -WorkDir $repoRoot `
    -OutputFile (Join-Path $runDir "robothost_null.txt")

if ($steps.robothost_null.ok)
{
    $results.robothost_null = Parse-RobotHostOutput (Get-Content (Join-Path $runDir "robothost_null.txt") -Raw)
}

$steps.robothost_full = Invoke-External `
    -Name "robothost_full" `
    -FilePath "dotnet" `
    -ArgumentList @("run", "--project", "Tests\\csharp\\RobotHost\\RobotHost.csproj", "-c", "Release", "--", "$Bots", "$Frames", "$Dt", "--host", "full") `
    -WorkDir $repoRoot `
    -OutputFile (Join-Path $runDir "robothost_full.txt")

if ($steps.robothost_full.ok)
{
    $results.robothost_full = Parse-RobotHostOutput (Get-Content (Join-Path $runDir "robothost_full.txt") -Raw)
}

$robotExe = Join-Path $repoRoot "build\\bin\\Release\\bridge_robot_runner.exe"
if (Test-Path $robotExe)
{
    $steps.robot_runner = Invoke-External `
        -Name "robot_runner" `
        -FilePath $robotExe `
        -ArgumentList @("$Bots", "$Frames", "$Dt") `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $runDir "robot_runner.txt")

    if ($steps.robot_runner.ok)
    {
        $results.robot_runner = Parse-RobotRunnerOutput (Get-Content (Join-Path $runDir "robot_runner.txt") -Raw)
    }
}
else
{
    $steps.robot_runner = [ordered]@{ ok = $false; error = "missing: $robotExe" }
}

$unity = $null
if (-not $NoUnity)
{
    $unity = Find-UnityExe
    if (-not $unity)
    {
        $steps.unity = [ordered]@{ ok = $false; error = "Unity.exe not found. Pass -UnityExe or install via Unity Hub." }
    }
    else
    {
        Write-Host "Unity: $unity"
    }
}

if ($unity -and -not $NoUnityEditMode)
{
    $proj = Join-Path $repoRoot "Tests\\unity"
    $unityEditModeXml = Join-Path $runDir "unity-editmode-test-results.xml"
    $unityEditModePerf = Join-Path $runDir "unity-editmode-perf-results.json"
    $unityEditModeLog = Join-Path $runDir "unity-editmode-test.log"

    $steps.unity_editmode = Invoke-External `
        -Name "unity_editmode" `
        -FilePath $unity `
        -ArgumentList @("-runTests", "-batchmode", "-nographics", "-projectPath", $proj, "-testPlatform", "EditMode", "-testResults", $unityEditModeXml, "-perfTestResults", $unityEditModePerf, "-logFile", $unityEditModeLog) `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $runDir "unity_editmode_console.txt")

    if ($steps.unity_editmode.ok)
    {
        $startedUtc = [DateTime]::Parse($steps.unity_editmode.startedUtc).ToUniversalTime()
        if (Wait-FileUpdated -Path $unityEditModePerf -NotBeforeUtc $startedUtc -TimeoutSeconds 1800 -MinLength 32)
        {
            $results.unity_editmode = Parse-UnityEditModePerf $unityEditModePerf
        }
        else
        {
            $results.unity_editmode = [ordered]@{
                error    = "unity editmode perf results not produced in time"
                perfJson = $unityEditModePerf
            }
        }
    }
    else
    {
        $lock = Get-UnityProjectLockReason -ConsoleOutputPath $steps.unity_editmode.outputFile
        if ($lock)
        {
            $steps.unity_editmode.error = $lock
            $results.unity_editmode = [ordered]@{ error = $lock }
        }
    }
}

if ($unity -and -not $NoUnityIl2cpp)
{
    $proj = Join-Path $repoRoot "Tests\\unity"
    $rutttDir = Join-Path $runDir "ruttt_il2cpp_source"
    New-Dir $rutttDir
    $rutttExe = Join-Path $rutttDir "test.exe"
    $rutttBuildLog = Join-Path $rutttDir "build.log"
    $rutttRunLog = Join-Path $rutttDir "run.log"

    $steps.unity_ruttt_build_il2cpp = Invoke-External `
        -Name "unity_ruttt_build_il2cpp" `
        -FilePath $unity `
        -ArgumentList @("-quit", "-batchmode", "-nographics", "-projectPath", $proj, "-executeMethod", "Bridge.Core.Unity.Editor.BridgeCoreRuntimeUnitTestBuild.BuildUnitTest", "/ScriptBackend", "IL2CPP", "/BuildTarget", "StandaloneWindows64", "/buildPath", $rutttExe, "-logFile", $rutttBuildLog) `
        -WorkDir $repoRoot `
        -OutputFile (Join-Path $rutttDir "unity_build_console.txt")

    $lock = Get-UnityProjectLockReason -ConsoleOutputPath $steps.unity_ruttt_build_il2cpp.outputFile

    $reason = Get-UnityIl2cppBuildFailureReason -LogPath $rutttBuildLog
    $exeReady = (Test-Path $rutttExe) -and ((Get-Item $rutttExe).Length -ge 1024)

    if ($lock)
    {
        $steps.unity_ruttt_build_il2cpp.ok = $false
        $steps.unity_ruttt_build_il2cpp.error = $lock
    }
    elseif ($reason)
    {
        $steps.unity_ruttt_build_il2cpp.ok = $false
        $steps.unity_ruttt_build_il2cpp.error = $reason
    }
    elseif (-not $exeReady)
    {
        # Unity sometimes returns exitCode=0 even on build failure; treat missing exe as failure.
        $steps.unity_ruttt_build_il2cpp.ok = $false
        $steps.unity_ruttt_build_il2cpp.error = "unity il2cpp test.exe not produced (see build log)."
    }

    if ($steps.unity_ruttt_build_il2cpp.ok)
    {
        $steps.unity_ruttt_run_il2cpp = Invoke-External `
            -Name "unity_ruttt_run_il2cpp" `
            -FilePath $rutttExe `
            -ArgumentList @("-batchmode", "-nographics", "-logFile", $rutttRunLog) `
            -WorkDir $rutttDir `
            -OutputFile (Join-Path $rutttDir "player_console.txt")

        if ($steps.unity_ruttt_run_il2cpp.ok)
        {
            $runStartedUtc = Try-Get { [DateTime]::Parse($steps.unity_ruttt_run_il2cpp.startedUtc).ToUniversalTime() }
            if ($runStartedUtc -and (Wait-FileUpdated -Path $rutttRunLog -NotBeforeUtc $runStartedUtc -TimeoutSeconds 1800 -MinLength 256))
            {
                $results.unity_il2cpp_source = Parse-BridgePerfLog $rutttRunLog
            }
            else
            {
                $results.unity_il2cpp_source = [ordered]@{
                    error  = "unity il2cpp run.log not produced in time"
                    runLog = $rutttRunLog
                }
            }
        }
    }
    else
    {
        $err = Try-Get { [string]$steps.unity_ruttt_build_il2cpp.error }
        if ([string]::IsNullOrWhiteSpace($err)) { $err = "unity il2cpp build failed" }
        $results.unity_il2cpp_source = [ordered]@{
            error  = $err
            exe    = $rutttExe
            log    = $rutttBuildLog
        }
    }
}

$record = [ordered]@{
    tsUtc   = (Get-Date).ToUniversalTime().ToString("o")
    runId   = $runId
    tag     = $Tag
    git     = $git
    machine = $machine
    params  = [ordered]@{ bots = $Bots; frames = $Frames; dt = $Dt }
    runDir  = $runDir
    steps   = $steps
    results = $results
}

$line = $record | ConvertTo-Json -Depth 32 -Compress
Add-Content -Path $OutFile -Value $line -Encoding UTF8

Write-Host "Appended: $OutFile"

if ($readmePath)
{
    try
    {
        Update-PerfReadme -ReadmePath $readmePath -HistoryPath $OutFile -MaxRows $ReadmeMaxRows
        Write-Host "Updated: $readmePath"
    }
    catch
    {
        Write-Warning ("Failed to update README perf table: " + $_.Exception.Message)
    }
}
