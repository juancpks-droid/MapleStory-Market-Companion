# MapleStory Market Companion

An unofficial Windows companion for MapleStory players: market observations, inventory slots, watchlists, trading records, item reference websites and in-app ad blocking.

## Install

Download **MapleStoryMarketCompanion-win-Setup.exe** from [the latest release](https://github.com/juancpks-droid/MapleStory-Market-Companion/releases/latest). Close any older copy, including its tray icon, then run Setup. Windows 10 build 19041 or newer (64-bit) is required. .NET is included; embedded websites require Microsoft Edge WebView2 Runtime.

The app is currently unsigned; Windows may show an unknown-publisher warning.

## Automatic updates

Version 1.20.3 checks this repository's stable releases at startup and every hour while online. A popup asks whether to download the update and restart now. Yes downloads and verifies the package, then restarts without an idle wait or countdown. Active refreshes and other app windows must finish first. From v1.20.7, unsaved Settings edits block restart until saved; clean Settings and Trading Sessions pages allow restart. Saved views restore even when setup was never marked complete. No keeps the app running and suppresses automatic prompts for that version for the rest of the session; Check for updates can offer it again. Automatic checks can be disabled in Settings. Offline mode prevents new checks.

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

180 core checks passed in the development workspace. Local updater tests passed for finding a newer release, download verification/staging, avoiding reinstalls and downgrades, and rejecting a corrupted package. The full update/restart cycle was subsequently tested in an isolated copy of the companion using the production updater logic and normal countdown. The test exposed and fixed two issues in 1.18.1: a focused Dashboard search box preventing restart and content obscuring the update banner. The updated process reopened Dashboard, retained a 73-item test inventory, wrote a recovery backup and released the previous process mutex. Settings, active refreshes, editing windows, Later and recent-input delay checks also passed. The lifecycle test used a local release feed and separate app identity/data; unauthenticated public GitHub download/verification was checked separately. This was tested on one Windows computer, not a broad device matrix. Reports accompany the release.

This is independent fan software, not affiliated with Nexon or marketplace operators. Original code is MIT licensed. Game names, artwork served by external services and marketplace content belong to their respective owners. See LICENSE.txt and ThirdPartyLicenses.




## Resume after restart

Version 1.20.4 saves your last tab, browsing searches and filters, chart range, bag category, and window size every five seconds and before closing or updating. Existing saved player records and dashboard customizations remain local. Unfinished trades remain drafts. This view-saving behavior starts after installing 1.20.4.



## Item drop references

From v1.20.8, select an item in Item Finder and open Monster drops & locations below the results. On-demand exact-name lookups show up to five monsters and three recorded maps each, credited and linked to NiaMeowDB. Community reports and historical MapleSEA evidence are labelled separately and may differ from your server. Missing information does not prove an item has no drops. The reference cache stays in memory for one hour; no drop database is bundled.

