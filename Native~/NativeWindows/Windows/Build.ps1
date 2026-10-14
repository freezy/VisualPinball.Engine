param(
	[string] $UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor',
	[string] $OutputPath,
	[switch] $Test
)
$ErrorActionPreference = 'Stop'
$nativeRoot = $PSScriptRoot
$apiRoot = [System.IO.Path]::GetFullPath((Join-Path $nativeRoot '..'))
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $nativeRoot '..\..\..'))
$sdkHeaders = Join-Path $UnityEditor 'Data\PluginAPI'
if (!(Test-Path -LiteralPath (Join-Path $sdkHeaders 'IUnityGraphicsD3D11.h'))) {
	throw "Unity native plugin SDK headers were not found under $sdkHeaders. Supply -UnityEditor with the installed Editor directory."
}
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudioRoot = & $vswherePath -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (!$visualStudioRoot) { throw 'Install the Visual Studio C++ x64 toolchain before building.' }
$msvcRoot = Get-ChildItem -LiteralPath (Join-Path $visualStudioRoot 'VC\Tools\MSVC') -Directory | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty FullName
$windowsSdkRoot = (Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots').KitsRoot10
$sdkVersion = Get-ChildItem -LiteralPath (Join-Path $windowsSdkRoot 'Include') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um\dcomp.h') } | Sort-Object Name -Descending | Select-Object -First 1 -ExpandProperty Name
$compiler = Join-Path $msvcRoot 'bin\Hostx64\x64\cl.exe'
$linker = Join-Path $msvcRoot 'bin\Hostx64\x64\link.exe'
# Intermediates go to obj/, which the repository ignores.
$buildDirectory = Join-Path $nativeRoot 'obj'
$pluginDirectory = Join-Path $packageRoot 'VisualPinball.Unity\Plugins\win-x64'
New-Item -ItemType Directory -Force -Path $buildDirectory, $pluginDirectory | Out-Null
$includePaths = @((Join-Path $msvcRoot 'include'), $sdkHeaders, $apiRoot)
foreach ($part in @('um', 'shared', 'ucrt', 'winrt')) { $includePaths += Join-Path $windowsSdkRoot "Include\$sdkVersion\$part" }
$libraryPaths = @((Join-Path $msvcRoot 'lib\x64'), (Join-Path $windowsSdkRoot "Lib\$sdkVersion\um\x64"), (Join-Path $windowsSdkRoot "Lib\$sdkVersion\ucrt\x64"))
$source = Join-Path $nativeRoot $(if ($Test) { 'NativeWindowTests.cpp' } else { 'VisualPinballNativeWindows.cpp' })
$object = Join-Path $buildDirectory $(if ($Test) { 'NativeWindowTests.obj' } else { 'VisualPinballNativeWindows.obj' })
$output = $(if ($OutputPath) { [System.IO.Path]::GetFullPath($OutputPath) } elseif ($Test) { Join-Path $buildDirectory 'NativeWindowTests.exe' } else { Join-Path $pluginDirectory 'VisualPinballNativeWindows.dll' })
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$compileArguments = @('/nologo', '/c', '/std:c++17', '/permissive-', '/EHsc', '/W4', '/WX', '/O2', '/MD', '/utf-8', '/DUNICODE', '/D_UNICODE', '/D_WIN32_WINNT=0x0A00', "/Fo$object")
foreach ($path in $includePaths) { $compileArguments += "/I$path" }
& $compiler @compileArguments $source
if (!$Test -and $LASTEXITCODE -eq 0) {
	$bridgeObject = Join-Path $buildDirectory 'D3D12CopyBridge.obj'
	$bridgeArguments = $compileArguments | Where-Object { !($_ -like '/Fo*') }
	& $compiler @bridgeArguments "/Fo$bridgeObject" (Join-Path $nativeRoot 'D3D12CopyBridge.cpp')
}
if ($LASTEXITCODE -ne 0) { throw "C++ compilation failed with exit code $LASTEXITCODE." }
$linkArguments = @('/NOLOGO', '/MACHINE:X64', '/INCREMENTAL:NO', '/OPT:REF', '/OPT:ICF', "/OUT:$output", "/IMPLIB:$(Join-Path $buildDirectory 'VisualPinballNativeWindows.lib')", $object, 'd3d11.lib', 'd3d12.lib', 'dxgi.lib', 'dcomp.lib', 'd3dcompiler.lib', 'user32.lib')
foreach ($path in $libraryPaths) { $linkArguments += "/LIBPATH:$path" }
if (!$Test) { $linkArguments += @('/DLL', $bridgeObject) }
& $linker @linkArguments
if ($LASTEXITCODE -ne 0) { throw "C++ linking failed with exit code $LASTEXITCODE." }
Write-Host "Built $output"
if ($Test) {
	& $output
	if ($LASTEXITCODE -ne 0) { throw "Native integration tests failed with exit code $LASTEXITCODE." }
}
