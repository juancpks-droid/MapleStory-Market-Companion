param([Parameter(Mandatory=$true)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
function Assert-Success { if($LASTEXITCODE -ne 0){ throw "Command failed with exit code $LASTEXITCODE" } }
dotnet run --project src/Market.Tests -c Release
Assert-Success
dotnet tool install vpk --version 1.2.161 --tool-path .tools --configfile NuGet.Config
if($LASTEXITCODE -ne 0 -and !(Test-Path .tools/vpk.exe)){throw 'Could not install packaging tool'}
$publish="build/$Version"
dotnet publish src/Market.App -c Release -r win-x64 --self-contained true -p:Version=$Version -p:AssemblyVersion="$Version.0" -o $publish
Assert-Success
Copy-Item settings-defaults.json $publish
Copy-Item LICENSE.txt $publish
Copy-Item ThirdPartyLicenses $publish -Recurse -Force
$releases="Releases/$Version"
& .tools/vpk.exe pack -u MapleStoryMarketCompanion -v $Version -p $publish -e MapleStoryClassicMarketCompanion.exe --packTitle 'MapleStory Market Companion' --packAuthors juancpks-droid -o $releases
Assert-Success
$assets=Get-ChildItem $releases -File | Where-Object Name -ne 'assets.win.json' | ForEach-Object FullName
gh release create "v$Version" @assets --repo juancpks-droid/MapleStory-Market-Companion --draft --title "MapleStory Market Companion $Version" --notes 'Windows update. Review the changes and test the installer before publishing.'
Assert-Success
Write-Host 'Draft uploaded. Review and publish it on GitHub to make it available to players.'
