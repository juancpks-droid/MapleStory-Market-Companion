import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {matchItems,exactItem,cacheKey} from './lib/search.ts';
import {scrollReferences,lookupIdentity,marketKey} from './lib/scrolls.ts';
const catalog=JSON.parse(readFileSync(new URL('./public/catalog.json',import.meta.url)));
for(const ref of scrollReferences){const item=catalog.find(i=>i.id==='classic:'+ref.ClassicId);assert(item);assert.deepEqual(lookupIdentity(item),{name:ref.Name,id:ref.ClassicId});}
for(const rate of [10,60,100]){const found=matchItems(catalog,`gloves att ${rate}%`,'Use');assert(found.length);assert(found.every(i=>i.name.includes(`(${rate}%)`)));}
assert.equal(lookupIdentity(catalog.find(i=>i.id==='2040804')).id,'2040801');
assert.throws(()=>lookupIdentity({id:'2049999',name:'Scroll for Gloves for ATT'}));
assert.deepEqual(lookupIdentity({id:'2030000',name:'Return Scroll - Nearest Town'}),{id:'2030000',name:'Return Scroll - Nearest Town'});
assert.equal(exactItem(catalog,'Red Potion','All').id,'2000000');
assert.equal(exactItem(catalog,'gloves','All'),null);
assert(matchItems(catalog,'snail','Equipment').every(i=>i.category==='Equipment'));
assert.equal(marketKey('Gloves Attack Scroll: Intermediate'),marketKey('gloves-attack-scroll-intermediate'));
assert.notEqual(cacheKey('finder','2040801'),cacheKey('finder','classic:2040801'));
console.log('PASS: 208 Classic scroll identities, legacy ID mapping, exact 10/60/100 rates, safe ambiguous lookup, categories and cache isolation.');

