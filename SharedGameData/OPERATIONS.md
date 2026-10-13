# Shared reference publishing

The PC and Lite clients consume the same small, public, source-labelled JSON records. The scheduled GitHub job collects factual references centrally; no background crawler runs on a player's PC or phone. On-demand source results may remain in each client's cache until the central collector reaches that reference. This is eventual shared coverage, not real-time upload of player searches or Lite's database.

## Coverage

Supported guide entities: items, monsters, maps, NPCs, shops, quests and crafting recipes. Source-rendered scalar facts, factual relation lists, quantities and prices are extracted. Quest story text, entire articles, private pages and account data are not copied. Missing fields and partial lists are explicitly retained as limitations. Some sources disagree; CCPLZ guide facts and the existing NiaMeowDB/historical drop results remain separately attributed.

The catalog is an identity/search index, not a claim that every item has complete details. The initial shared snapshot has 20 guide pages plus the existing NPC/drop records. Additional coverage is collected gradually. The actual counts and per-file hashes are in `manifest.json`.

## Collector

`.github/workflows/game-data.yml` runs daily or manually. It is installed and enabled on the main branch. It processes at most 18 source pages per run, obeys the existing 3-second source spacing, robots checks, hourly limit and backoff, and queues discovered relations. It never follows arbitrary hosts or logs credentials. Empty, mismatched and invalid records do not replace good copies. Market listings are excluded from these slow-changing snapshots.

The collector validates its records before one Git commit publishes the batch. Failed lookups retain their previous versions and remain queued. Run from the repository root after installing Lite's dependencies:

```
node Lite/guide.test.mjs
node Lite/scripts/collect-game-data.mjs SharedGameData
```

## Client performance

- Search names and numeric tokens are indexed once in Lite; only 40 suggestions render.
- Details are loaded on selection or when the guide is opened, never on each keystroke.
- Each public snapshot is limited to 256 KiB and a 1.2-second network budget before source fallback.
- PC reads per-page saved files/bundled starters and keeps a maximum of 100 guide files. No full database load at startup.
- Lite retains 30 guide pages on the phone, 100 hot server guide records, and at most four active guide jobs. Repeated same-page requests share a job.
- Reference refresh age is seven days. Existing data remains visible through errors/refreshes. Market prices retain their separate freshness rules.
- Local selections, filters and guide navigation remain device-local. No settings or personal inventories enter GitHub.

## Recovery

Every published batch is a normal Git commit. Revert an identified bad data commit and push the revert; do not rewrite public history. Clients reject wrong namespaces, unsafe paths, future timestamps, oversized or malformed guide records. Reload a specific reference to obtain the corrected record. Existing valid local copies and bundled starters remain available when a source is unavailable. App binaries and user settings are separate from this data pipeline.

## Verification for 1.27.0 / Lite 0.4.0

196 core checks passed, including guide identity, corrupt-cache recovery, offline starters, scroll rate isolation, and bounded lists. Lite type checking, parser, source, search and shared-record checks passed. Eight local guide requests spanning all seven entity types returned usable records; saved local responses were 20–90 ms. Indexed searches over 12,726 catalog entries averaged 0.63 ms in a 100-search local run. These are local measurements, not promises about every phone/network.

An isolated Velopack 1.26.0→1.27.0 package test downloaded and verified an update, blocked restart for unsaved Settings, restarted after saving, and preserved seeded session, settings, watchlist, alerts, theme, history and legacy data. It also read the bundled guide offline after restart. This used an isolated local feed and scripted confirmation, not a friend's actual installation. Physical iPhone testing remains outside this automated check.

## First scheduled-pipeline verification

The manual run on October 13, 2026 passed validation, retrieved 18 of 18 attempted pages, and published commit 34ea6ab. The resulting snapshot contains 38 guide pages and 16 NPC/drop records. The daily schedule is enabled at 09:23 UTC; GitHub may delay scheduled runs. Run evidence: https://github.com/juancpks-droid/MapleStory-Market-Companion/actions/runs/38423585273

## 1.28.0 / Lite 0.5.0 completion checklist

This checklist describes implemented support, not exhaustive game-data coverage.

1. Shared versioned records: same guide shape on PC, Lite and GitHub.
2. Item identity: namespaced catalog IDs and scroll success-rate isolation.
3. Item facts: stats, requirements and NPC reference prices where supplied.
4. Drops: source-labelled reported and historical evidence remain distinct.
5. Monsters: facts and related items/maps in the guide.
6. Maps: monster/NPC/connected-map references inside the guide.
7. NPCs: factual details and location/shop links where supplied.
8. Shops: source-reported stock and prices; separate from market offers.
9. Quests: requirements/rewards/relationships without copying story prose.
10. Crafting: ingredients, quantities, result, costs and scalar requirements; PC direct parsing now matches Lite.
11. Navigation: bounded searchable reference directory by entity type, plus related-page navigation and Back.
12. Offline/session support: 38 bundled guide pages; bounded saved pages; Lite restores guide query/type/path alongside existing app search/filter state.
13. Performance: no full reference download at startup, 40 search results, 256 KiB public-record cap, 1.2-second snapshot timeout, queued source work and bounded client caches.
14. Publishing/recovery: daily bounded GitHub collector, manifest hashes, verified directory targets, schema/source validation, previous good data retained on failure, normal Git rollback.
15. Validation/releases: 199 PC tests passed, Lite type/build/search/source/guide/reference checks passed. Browser QA at 390x844 restored Blue Moonlight, Brown Skullcap recipe, query 'skullcap' and recipe filter. Local API tests covered all seven types. An isolated 1.27.0→1.28.0 package update/restart preserved seeded user data/session and recovery backup. Physical iPhone and friends' own installations remain untested.

The catalog still indexes 12,726 items; only the records counted in the live manifest have detailed shared coverage. Historical sources can differ from Classic World. The directory lists published pages (up to 2,000 and 250 KiB) rather than claiming that an absent reference does not exist. No guarantee can be made that every external source will always be available. On-demand page reads and the daily collector fill missing supported references over time.

The upgraded publisher passed on October 13, 2026: 18/18 new pages retrieved, 56 guide pages and 19 NPC/drop records validated (including SHA256 hashes), and normal commit 1bc0690 published. Run: https://github.com/juancpks-droid/MapleStory-Market-Companion/actions/runs/38436053439


## Player improvements: 1.29.0 / Lite 0.6.0

Maple Market 1.29.0 / Lite 0.6.0

- Clearer lookup progress and saved results while supported sources refresh.
- Easy access to recent items and watched favorites on PC; recent items and favorites on Lite.
- Clearer price age and evidence labels, with NPC values separate from player asks.
- Combined item summary with available market observations, NPC references, drops, maps and guide details.
- Rotating common-item and distinct scroll-tier priorities in the bounded daily shared-data collector.
- Faster repeat lookups with bounded saved results; unavailable market sources retain clearly labelled saved offers on Lite.
- Copyable problem reports without seller names, listing notes, local file paths or device identifiers.

Validation: 199 PC core tests passed. Lite search/source/guide/reference/player-tool checks, type checking and production build passed. Phone-width browser checks restored search/filter/favorite state. An isolated Windows 1.28.0 to 1.29.0 update downloaded, restarted and retained seeded settings, search/filter/session state, watchlist, market history and recovery backup. A physical iPhone and friends' individual installations were not tested. Shared detail coverage remains partial and source-labelled; historical references may differ from Classic World.
