# Maple Market Lite

A separate iPhone Home Screen web app with only Item Finder and Price Checker. Install from Safari using Share > Add to Home Screen.

Built with the Sites Vinext starter. Run `npm run dev`; build with `npm run build`. Server lookups use public source pages with request spacing, timeouts, robots checks, bounded caches and item identity checks. Sources: Kerning (player offers), CCPLZ and MapleStory Quest (NPC references), NiaMeowDB with CCPLZ fallback (drops/maps). Missing information stays unavailable. Historical references are labelled.

Selected item/tab and recent results are stored locally on each device. No player account or Windows data synchronization. Network required for new data. Browser testing is not a substitute for physical iPhone testing.

Tests: `node --experimental-strip-types sources.test.mjs` and `npx tsc --noEmit`.
