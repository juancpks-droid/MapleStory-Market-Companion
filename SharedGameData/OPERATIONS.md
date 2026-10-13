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
