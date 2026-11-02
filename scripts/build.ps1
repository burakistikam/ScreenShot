param([switch]$Publish)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$dotnetCommand=Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe=if($dotnetCommand){$dotnetCommand.Source}else{Join-Path $env:USERPROFILE '.dotnet/dotnet.exe'}
if(-not(Test-Path $dotnetExe)){throw 'Install the .NET 10 SDK first: https://dotnet.microsoft.com/download/dotnet/10.0'}
Push-Location $repoRoot
try {
    & $dotnetExe build src/ScreenShot -c Release
    if($LASTEXITCODE -ne 0){throw 'Build failed'}
    & $dotnetExe run --project tests/ScreenShot.Tests -c Release
    if($LASTEXITCODE -ne 0){throw 'Core tests failed'}
    & $dotnetExe run --project tests/ScreenShot.IntegrationTests -c Release
    if($LASTEXITCODE -ne 0){throw 'Rendering tests failed'}
    if($Publish){
        & $dotnetExe publish src/ScreenShot -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/win-x64
        if($LASTEXITCODE -ne 0){throw 'Publish failed'}
        Copy-Item README.md artifacts/win-x64/README.md
        New-Item -ItemType Directory -Force artifacts/win-x64/docs/images | Out-Null
        Copy-Item docs/images/*.png artifacts/win-x64/docs/images/
        Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/ScreenShot-0.3.0-win-x64.zip -Force
    }
} finally {Pop-Location}
