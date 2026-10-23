import assert from 'node:assert/strict';
import {parseNia,resolveNia} from './lib/nia-market.ts';
const s={itemId:1,condition:'clean',usual:12345};
const r=parseNia(s,{listings:[{id:1,status:'active',side:'sell',price:2400,stats:{critRate:2}},{id:2,status:'completed',side:'buy',price:5},{id:3,status:'active',side:'buy',price:null}]},1,'Test','https://meowdb.com/msclassic/item-db/1');
assert.equal(r.usual,12345);assert.equal(r.offers.length,2);assert.equal(r.offers[1].price,null);assert.equal(r.offers[0].stats,'critRate: 2');
assert.throws(()=>parseNia(s,{listings:[]},2,'Test',''));
assert.throws(()=>resolveNia('<a href="/msclassic/item-db/1">A</a><a href="/msclassic/item-db/2">A</a>','A'));
assert.equal(resolveNia('<a href="/msclassic/item-db/1435">Work Gloves (Lv 10)</a>','Work Gloves'),'https://meowdb.com/msclassic/item-db/1435');
console.log('PASS: Nia exact prices, active offers, variants, unknown prices and identity guards');
