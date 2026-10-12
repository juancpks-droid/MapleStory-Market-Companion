# MapleStory Market Companion

An unofficial Windows companion for MapleStory players: market listings, watchlists, price graphs, monster drops, item reference websites and in-app ad blocking.

## Install

Download **MapleStoryMarketCompanion-win-Setup.exe** from [the latest release](https://github.com/juancpks-droid/MapleStory-Market-Companion/releases/latest). Close any older copy, including its tray icon, then run Setup. Windows 10 build 19041 or newer (64-bit) is required. .NET is included; embedded websites require Microsoft Edge WebView2 Runtime.

The app is currently unsigned; Windows may show an unknown-publisher warning.

## Automatic updates

Version 1.20.3 checks this repository's stable releases at startup and every hour while online. A popup asks whether to download the update and restart now. Yes downloads and verifies the package, then restarts without an idle wait or countdown. Active refreshes and other app windows must finish first. From v1.20.7, unsaved Settings edits block restart until saved; clean Settings and Trading Sessions pages allow restart. Saved views restore even when setup was never marked complete. No keeps the app running and suppresses automatic prompts for that version for the rest of the session; Check for updates can offer it again. Automatic checks can be disabled in Settings. Offline mode prevents new checks.

Older ZIP versions need this installer once. Saved data stays in `%LOCALAPPDATA%\MapleStoryClassicMarketCompanion`; the updater does not upload your inventory or journal. A personal recovery backup is written before an update. Do not run old and new copies together.

If an update cannot be downloaded, the current version remains usable. Settings has a manual retry button. If installation fails, download and run the latest Setup again. Use Backups inside Settings to export important personal records.

## Price limitations

Listing prices are asking prices, not guaranteed sale values. Rounded prices remain labelled. Reference websites opened inside the companion do not automatically contribute prices. Coverage and availability depend on third-party websites. The small ad-domain blocker does not remove every advertisement.

## Build and release

Install .NET SDK 10 and GitHub CLI. Sign into GitHub CLI as the repository owner. From this folder run:

```powershell
dotnet run --project src/Market.Tests -c Release
./Release.ps1 -Version 1.22.0
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


## Faster navigation and listing tools (1.22.0)

Use the header arrows (Alt+Left / Alt+Right) to revisit pages. Go to (Ctrl+Shift+P) finds pages, watched items, and recent searches. The Dashboard also has Recent items and Watched items menus. Recent searches keep the last 12 distinct queries and can be cleared from their menu.

Price Checker adds source and recent-only filters, compact cards with a Details button, Reset filters, and Copy details for listing price, quantity, player, notes, source and link. Recent-only requires an enabled source, fresh evidence and a retrieval within 30 minutes. These controls filter displayed listings; they do not change valuation rules. Filters, compact mode and recent searches are saved across restarts. The header shows lookup and offline status.
## Listing tools (1.23.0)

Price Checker now includes Listing tools. Search players or location notes, filter by listing side, minimum quantity or exact prices, sort columns, and compare two same-side listings with Ctrl+click. Unknown quantities do not produce stack totals. Approximate prices stay labelled. More room hides the overview cards while this tab is active. Double-click or Enter opens a selected source listing; Export CSV saves the filtered table. Source, price-range and recent-only filters from the listings tab also apply. The new filters and active tab restore after restarting.

## Watchlist shortcuts (1.24.0)

Search watched names and variants, filter by muted state or recent-price availability, and jump to Price Checker or Item Finder. Double-click or Enter checks the selected item's prices. Search and filter preferences survive restarting; selections stay on the same item when rows refresh. Recent-price filters require source freshness and retrieval within 30 minutes. Alert counts show per-item mute preferences; global notification settings still apply.


### 1.25.5 — Direct item references
- Item Finder > Item details loads MapleClassic Wiki facts inside the app, with an explicitly historical GMS 83 MapleStory.io fallback when a matching Wiki page is unavailable.
- Monster drops & locations has MapleClassic Wiki and Automatic sources. Automatic tries Wiki, CCPLZ, then MapleStory Quest; the source selector retains existing saved choices.
- Wiki NPC sell/shop prices are a third fallback after CCPLZ and Quest. Reference values never enter player-market history or estimates.
- Item details tab selection survives restart. Requests are on demand, cached for an hour, spaced, bounded, and cancellable; unavailable/mismatched data stays unknown.
- Wiki-derived content: MapleClassic Wiki contributors, CC BY-NC-SA 4.0. Selected facts reformatted; no drop rates inferred. See ThirdPartyLicenses/MapleClassic-Wiki.txt. GMS 83 is historical, not confirmed Classic World data.

Validation: 186 core tests; live Orange Mushroom Cap, Red Potion and Blue Moonlight reference checks; Wiki monster/map rendering and empty-result handling; close/reopen session test; isolated real package update 1.25.4 -> 1.25.5 with saved state, backup, graph and NPC lookup verified. The updater test uses a local feed and scripted confirmation; it does not modify the player's profile.

Additional source review: Sleepywood database failed to load and API docs returned 404; Planet Helper terms prohibit unauthorized scraping. Neither was enabled as an automatic provider.

### 1.25.6 — Item reference display correction
- Corrects garbled separators in item/drop reference messages.
- Long map lists show five names and a remaining count; clicking opens the full map menu. Monster names and evidence remain aligned at the top.
- Verified live references for Orange Mushroom Cap, Red Potion and Blue Moonlight, including monster maps and empty drops. Added display regression checks; session recovery and isolated package update/restart tested again.
