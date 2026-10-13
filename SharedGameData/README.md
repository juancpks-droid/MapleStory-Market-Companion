# Shared game reference database — first release

PC 1.26.0 and Lite 0.3.0 share a versioned record format and five starter records in `records/`. PC reads these small public GitHub files; Lite imports the same records into its persistent database. It contains public game references only, never player inventories or personal settings.

The Lite host rejected direct non-browser API requests during testing. Therefore PC does not depend on that endpoint. Additional records learned by Lite are not automatically mirrored to GitHub yet; publishing a reviewed snapshot is currently required to expand the PC's shared coverage. Both clients retain their existing direct source readers for missing items. This is the foundation of a shared database, not a completed full-game import.

## Initial coverage

The existing 12,726-entry catalog is the identity index, primarily historical GMS 83, with explicitly mapped Classic scroll variants. This is **not** a claim of complete Classic World coverage. The first shared record types are NPC buy/sell prices and item → monster → map references. Records are filled on demand by the existing NiaMeowDB, CCPLZ and MapleStory Quest readers. No bulk crawler runs at startup. Other PC reference readers remain available as fallbacks.

Every record has a schema version, namespaced identity, original source, source URL, checked time and evidence labels. Community reports are not independently verified; historical MSEA/GMS information is not promoted to confirmed Classic data. The current Lite drop adapters return up to five monsters and three maps per monster, so these records are partial references, not exhaustive drop tables. Market offers are fetched separately and are never replaced by NPC prices.

## Performance and resilience

- One small indexed record per request; no full database download during searches.
- Bounded in-memory cache (200 entries) and at most four simultaneous reference retrievals per worker instance; identical in-flight item requests are shared.
- Persistent D1 records survive process restarts. Existing source request spacing and per-host limits still apply.
- Seven-day reference refresh age. Lite displays stale saved data before refreshing; source failures and empty results retain good records.
- PC checks saved per-item files first, supports those records offline, and checks the public GitHub snapshot during its existing debounce. It does not wait past that debounce; existing readers continue normally. Shared HTTP work has a 1.2-second limit and a 256 KiB response limit.
- UI result generations and cancellation still prevent an older search overwriting a newer item.

## Lite same-origin API

`GET /api/game-data?id=1051025&kind=drops`

`GET /api/game-data?id=2000000&kind=npc`

`GET /api/game-data?id=classic%3A2040801&kind=npc`

Add `cached=1` for a fast database-only read (204 if missing). `refresh=1` requests a source refresh; existing valid data is retained on failure. `GET /api/game-data?coverage=1` reports actual detailed-record counts separately from catalog size. Requests must use valid catalog IDs; arbitrary source URLs and client-written records are not accepted.

Game data and artwork remain the property of their respective owners. This database contains selected factual references and links, not a copy of source websites. Source attribution travels with each record. The app's source-code license does not grant ownership of third-party game content.
