#requires -Version 7.0
param(
	[Parameter(Mandatory = $true)][string] $OutputDirectory,
	[string] $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$nativeRoot = $PSScriptRoot
$engineRoot = [IO.Path]::GetFullPath((Join-Path $nativeRoot '..\..'))
$sampleRoot = Join-Path $engineRoot 'VisualPinball.Unity\Samples~\AudioRouting'
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$project = Join-Path $taskOutput 'ProbeProject'
$marker = Join-Path $project '.vpe-audio-routing-probe'
if ((Test-Path -LiteralPath $project) -and !(Test-Path -LiteralPath $marker)) {
	throw "Refusing to modify an existing unmarked Unity project: $project"
}
if (!(Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
New-Item -ItemType Directory -Force -Path $taskOutput, $project | Out-Null
Set-Content -LiteralPath $marker -Value 'VPE isolated audio routing prototype' -Encoding utf8
$nativeBuild = Join-Path $taskOutput 'native-build'
& cmake -S $nativeRoot -B $nativeBuild -G 'Visual Studio 17 2022' -A x64
if ($LASTEXITCODE -ne 0) { throw 'Native configuration failed.' }
& cmake --build $nativeBuild --config Release
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
& ctest --test-dir $nativeBuild -C Release --output-on-failure
if ($LASTEXITCODE -ne 0) { throw 'Offline routing fixtures failed.' }
foreach ($relative in @('Assets\Runtime', 'Assets\Editor', 'Assets\Plugins\x86_64', 'Packages', 'ProjectSettings')) {
	New-Item -ItemType Directory -Force -Path (Join-Path $project $relative) | Out-Null
}
Copy-Item -LiteralPath (Join-Path $sampleRoot 'Runtime\AudioRoutingProbe.cs') -Destination (Join-Path $project 'Assets\Runtime\AudioRoutingProbe.cs')
Copy-Item -LiteralPath (Join-Path $sampleRoot 'Editor\AudioRoutingProbeBuild.cs') -Destination (Join-Path $project 'Assets\Editor\AudioRoutingProbeBuild.cs')
Copy-Item -LiteralPath (Join-Path $nativeBuild 'Release\AudioPluginVpeRouting.dll') -Destination (Join-Path $project 'Assets\Plugins\x86_64\AudioPluginVpeRouting.dll')
$manifest = @{ dependencies = @{ 'com.unity.modules.audio' = '1.0.0'; 'com.unity.modules.imgui' = '1.0.0'; 'com.unity.modules.jsonserialize' = '1.0.0' } } | ConvertTo-Json
[IO.File]::WriteAllText((Join-Path $project 'Packages\manifest.json'), $manifest)
$playerPath = Join-Path $taskOutput 'player\VpeAudioProbe.exe'
$logPath = Join-Path $taskOutput 'unity-build.log'
$start = [Diagnostics.ProcessStartInfo]::new($UnityEditor)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
foreach ($argument in @('-batchmode', '-quit', '-projectPath', $project, '-executeMethod', 'VisualPinball.Unity.Editor.AudioRoutingProbeBuild.Build', '-probeOutput', $playerPath, '-logFile', $logPath)) {
	$start.ArgumentList.Add($argument)
}
$process = [Diagnostics.Process]::Start($start)
Write-Output "Unity build process $($process.Id); log: $logPath"
$process.WaitForExit()
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $playerPath)) { throw "Unity probe build failed. Read $logPath" }
Write-Output "Built standalone probe: $playerPath"
Write-Output 'Run with --layout stereo or --layout 7.1, --report <absolute JSON path> and optionally --quit-on-complete.'
