import assert from 'node:assert/strict';
import {parseClassicFeed,matchClassicPrice,CLASSIC_FEED} from './lib/classic-market.ts';
import {keepSavedMarket,kerningMarket} from './lib/player-tools.ts';
const raw={updatedAt:'2026-10-15T10:00:00Z',items:[{n:'Work Gloves',s:'work-gloves',p:1999,sold:796,h:[['2026-10-14',1999,47],['2099-10-14',1,1],['2026-10-13',-1,1]]}]};
const rows=parseClassicFeed(raw);assert.equal(rows[0].history.length,1);assert.equal(matchClassicPrice(rows,'Work'),null);assert.equal(matchClassicPrice([...rows,...rows],'Work Gloves'),null);assert.equal(matchClassicPrice(rows,'Work Gloves').median,1999);assert.throws(()=>parseClassicFeed({...raw,updatedAt:'2099-01-01'}));
assert.equal(keepSavedMarket({classic:rows[0]},{classic:null,classicAvailable:false}).classic.median,1999);
assert.equal(kerningMarket({offers:[{source:'MapleStory-Classic.com',kind:'sell',price:1}],classic:rows[0]}).offers.length,0);
console.log('PASS: validation, exact/ambiguous identity, failure preservation and Kerning-only offers');
if(process.argv.includes('--live')){const r=await fetch(CLASSIC_FEED);assert(r.ok);const live=parseClassicFeed(await r.json());for(const name of ['Work Gloves','Orange Mushroom Cap','Iron Ore','Hat Accuracy Scroll: Intermediate','Hat Accuracy Scroll: Greater']){const p=matchClassicPrice(live,name);assert(p&&p.history.length);console.log(JSON.stringify({name,median:p.median,days:p.history.length,updated:p.updatedAt}));}assert.equal(matchClassicPrice(live,'Hat Accuracy Scroll'),null);console.log('PASS: live feed',live.length,'valid items');}
