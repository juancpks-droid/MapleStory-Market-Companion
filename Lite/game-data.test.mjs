import assert from 'node:assert/strict';
import {identity,meaningful} from './lib/game-data.ts';
assert.equal(identity('2040804').key,identity('classic:2040801').key);
assert.notEqual(identity('classic:2040801').key,identity('classic:2040802').key);
assert.equal(identity('2000000').key,'gms83:2000000');
assert.throws(()=>identity('../../private'));
assert.throws(()=>identity('unknown'));
assert.equal(meaningful('drops',{monsters:[]}),false);
assert.equal(meaningful('npc',{sell:null,buy:null}),false);
assert.equal(meaningful('npc',{sell:0,buy:null}),true);
assert.equal(meaningful('drops',{monsters:[{name:'Snail'}]}),true);
console.log('PASS: canonical cross-version keys, rate isolation, invalid IDs, empty-result protection and valid zero NPC value.');

const {validReference}=await import('./lib/reference-contract.ts');
const {default:seed}=await import('./lib/game-data-seed.json',{with:{type:'json'}});
for(const r of seed)assert.ok(validReference(r,r.item.key,r.kind),r.item.name);
const npc=seed.find(r=>r.kind==='npc');
for(const changed of [{checkedAt:'bad'},{checkedAt:new Date(Date.now()+86400000).toISOString()},{data:{...npc.data,sell:-1}},{data:{...npc.data,buy:'50'}},{data:{...npc.data,url:'https://evil.test'}}])assert.equal(validReference({...npc,...changed},npc.item.key,'npc'),false);
console.log('PASS: reference timestamps, source hosts, numeric prices and starter identities.');
