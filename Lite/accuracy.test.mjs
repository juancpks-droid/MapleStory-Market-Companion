import assert from 'node:assert/strict';
import {hitChance,neededAcc} from './lib/accuracy.ts';
assert.equal(hitChance(30,0,22,13),0);assert.equal(hitChance(30,0,1,0),1);assert.ok(Math.abs(hitChance(30,51,30,20)-.5)<1e-12);assert.ok(hitChance(20,51,30,20)<.5);assert.equal(hitChance(30,10000,30,20),1);assert.ok(Number.isNaN(hitChance(0,51,30,20)));
for(const level of [1,20,30,100])for(const mob of [1,30,100])for(const avoid of [0,13,29,100])for(const target of [.9,.95,1]){const n=neededAcc(level,mob,avoid,target);assert.ok(hitChance(level,n,mob,avoid)>=target);assert.ok(n===0||hitChance(level,n-1,mob,avoid)<target);assert.ok(hitChance(level,n+7,mob,avoid)>=hitChance(level,n,mob,avoid));}
console.log('PASS: zero avoid/ACC, 50% midpoint, level penalty, invalid inputs and 144 minimum whole-number targets.');
