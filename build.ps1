param([switch]$Package, [switch]$Installer)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
dotnet test "$root\tests\ClipVault.Tests\ClipVault.Tests.csproj" -c Release
dotnet build "$root\src\ClipVault.App\ClipVault.App.csproj" -c Release -p:Platform=x64
dotnet publish "$root\src\ClipVault.App\ClipVault.App.csproj" -c Release -p:Platform=x64 -r win-x64 -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:SelfContained=true -p:PublishTrimmed=false -p:PublishReadyToRun=false -o "$root\artifacts\ClipVault-win-x64"
if ($Package) {
    dotnet publish "$root\src\ClipVault.App\ClipVault.App.csproj" -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
}
if ($Installer) {
    $compiler = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
    if (-not $compiler) {
        $compiler = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'
    }
    if (-not (Test-Path -LiteralPath $compiler)) {
        throw 'Inno Setup 7 is required. Install it with: winget install --id JRSoftware.InnoSetup.7 -e --source winget'
    }
    & $compiler "$root\installer\ClipVault.iss"

    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }

    # Keep the current user's Start Menu shortcut refreshed after every
    # successful installer build without launching or replacing the live app.
    $installedDirectory = Join-Path $env:LOCALAPPDATA 'Programs\ClipVault'
    $installedExecutable = Join-Path $installedDirectory 'ClipVault.App.exe'
    if (Test-Path -LiteralPath $installedExecutable) {
        $startMenuDirectory = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\ClipVault'
        $startMenuShortcut = Join-Path $startMenuDirectory 'ClipVault.lnk'
        New-Item -ItemType Directory -Path $startMenuDirectory -Force | Out-Null

        $shell = New-Object -ComObject WScript.Shell
        try {
            $shortcut = $shell.CreateShortcut($startMenuShortcut)
            $shortcut.TargetPath = $installedExecutable
            $shortcut.WorkingDirectory = $installedDirectory
            $shortcut.IconLocation = "$(Join-Path $installedDirectory 'Assets\AppIcon.ico'),0"
            $shortcut.Description = 'ClipVault — Advanced Clipboard Manager for Windows'
            $shortcut.Save()
        }
        finally {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
        }
    }
}
