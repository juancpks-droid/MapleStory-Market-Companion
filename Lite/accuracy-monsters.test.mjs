import assert from 'node:assert/strict';
import {monsterCatalog} from './lib/accuracy-monsters.ts';
import {parseMonster} from './lib/accuracy-monster-data.ts';
assert.equal(monsterCatalog.length,1548);
assert.equal(new Set(monsterCatalog.map(m=>m.id)).size,monsterCatalog.length);
assert.ok(monsterCatalog.filter(m=>m.name==='Jr. Balrog').length>1);
for(const m of monsterCatalog){assert.ok(m.name.trim());assert.ok(m.level>=1&&m.level<=200);assert.ok(/^[\x20-\x7e]+$/.test(m.name));}
const snail={id:100100,name:'Snail',meta:{level:1,evasion:0}};
const parsed=parseMonster(snail,100100);assert.deepEqual({id:parsed.id,level:parsed.level,avoid:parsed.avoid},{id:100100,level:1,avoid:0});
for(const bad of [{...snail,id:100101},{...snail,name:'Blue Snail'},{...snail,meta:{level:1}},{...snail,meta:{level:1,evasion:null}},{...snail,meta:{level:1,evasion:-1}}])assert.throws(()=>parseMonster(bad,100100));
console.log('PASS: catalog coverage, unique IDs, duplicate names, English labels, zero avoid and missing/wrong stats rejection.');
