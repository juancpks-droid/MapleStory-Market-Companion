# MapleStory Market Companion

An unofficial Windows companion for MapleStory players: market observations, inventory slots, watchlists, trading records, item reference websites and in-app ad blocking.

## Install

Download **MapleStoryMarketCompanion-win-Setup.exe** from [the latest release](https://github.com/juancpks-droid/MapleStory-Market-Companion/releases/latest). Close any older copy, including its tray icon, then run Setup. Windows 10 build 19041 or newer (64-bit) is required. .NET is included; embedded websites require Microsoft Edge WebView2 Runtime.

The app is currently unsigned; Windows may show an unknown-publisher warning.

## Automatic updates

Version 1.18 checks this repository's stable releases at startup and every four hours while online. It downloads and verifies a newer package, then offers Restart now or Later. Automatic restart waits for Dashboard, no other app windows, no active refresh, and 30 seconds without typing or clicking, followed by a 30-second countdown. Updates can be disabled in Settings. Offline mode prevents new checks.

Older ZIP versions need this installer once. Saved data stays in `%LOCALAPPDATA%\MapleStoryClassicMarketCompanion`; the updater does not upload your inventory or journal. A personal recovery backup is written before an update. Do not run old and new copies together.

If an update cannot be downloaded, the current version remains usable. Settings has a manual retry button. If installation fails, download and run the latest Setup again. Use the Backups page to export important personal records.

## Price limitations

Listing prices are asking prices, not guaranteed sale values. Rounded prices remain labelled. Reference websites opened inside the companion do not automatically contribute prices. Coverage and availability depend on third-party websites. The small ad-domain blocker does not remove every advertisement.

## Build and release

Install .NET SDK 10 and GitHub CLI. Sign into GitHub CLI as the repository owner. From this folder run:

```powershell
dotnet run --project src/Market.Tests -c Release
./Release.ps1 -Version 1.18.1
```

The release script tests, builds and packages the Windows app and creates a **draft** GitHub release. Review it on GitHub and publish when ready. Publishing makes that version available to all clients with automatic updates enabled. Versions must increase. No access token is embedded in the application.

The bundled test project has optional historical website fixtures that are not included here; its normal synthetic tests run without them. The initial release was additionally checked against local historical fixtures.

## Validation for 1.18

180 core checks passed in the development workspace. Local updater tests passed for finding a newer release, download verification/staging, avoiding reinstalls and downgrades, and rejecting a corrupted package. The complete installed restart cycle has not been validated: automated execution of the isolated test was blocked. Reports accompany the release.

This is independent fan software, not affiliated with Nexon or marketplace operators. Original code is MIT licensed. Game names, artwork served by external services and marketplace content belong to their respective owners. See LICENSE.txt and ThirdPartyLicenses.
