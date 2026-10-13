import assert from 'node:assert/strict';
import {parseGuide,validGuide,guidePath,itemPath} from './lib/guide.ts';
import seed from './lib/guide-seed.json' with {type:'json'};
const fixture=(kind,name,body)=>`<article data-ssr="${kind}"><h1>${name}</h1>${body}</article>`;
const facts='<dl><dt>HP</dt><dd>45</dd></dl>';
const record=parseGuide(fixture('monster','Snail',facts+'<section><h2>Maps</h2><li><a href="/maps/snail-hunting-ground-i">Snail Hunting Ground I</a><span>40 spawn points</span></li><li><a href="https://evil.test">Bad</a></li></section>'),'/monsters/snail');
assert.equal(record.fields[0].value,'45');assert.equal(record.sections[0].links.length,1);assert.equal(record.sections[0].links[0].detail,'40 spawn points');assert.ok(validGuide(record,record.path));
for(const path of ['/items/../../secret','https://ccplz.cc/items/sword','/items/a?url=x','/items/a%2fb','/items/a/extra','/items/'+ 'x'.repeat(180)])assert.equal(guidePath(path),false);
assert.throws(()=>parseGuide(fixture('item','Blue Moonlight',facts),'/items/blue-moonlight','Red Moonlight'));
assert.throws(()=>parseGuide(fixture('monster','Snail',facts),'/items/snail'));
assert.throws(()=>parseGuide(fixture('item','Sword',''),'/items/sword'));
assert.throws(()=>parseGuide(fixture('item','검',facts),'/items/sword'));
assert.equal(validGuide({...record,sourceUrl:'https://evil.test'},record.path),false);
assert.equal(validGuide({...record,checkedAt:'bad'},record.path),false);
assert.equal(validGuide({...record,checkedAt:new Date(Date.now()+86400000).toISOString()},record.path),false);
assert.equal(validGuide({...record,sections:[{title:'Bad',links:[{name:'X',path:'/../secret',detail:''}]}]},record.path),false);
assert.equal(itemPath('Gloves Attack Scroll: Intermediate'),'/items/gloves-attack-scroll-intermediate');
for(const r of seed){assert.ok(validGuide(r,r.path),r.path);assert.ok(Buffer.byteLength(JSON.stringify(r))<262144);}
const many=Array.from({length:100},(_,i)=>`<li><a href="/maps/map-${i}">Map ${i}</a></li>`).join('');const limited=parseGuide(fixture('monster','Snail',`<section><h2>Maps</h2>${many}</section>`),'/monsters/snail');assert.equal(limited.sections[0].links.length,60);assert.equal(limited.sections[0].total,100);
const start=performance.now();for(let i=0;i<500;i++)parseGuide(fixture('monster','Snail',facts),'/monsters/snail');assert.ok(performance.now()-start<2000,'Small factual parser should remain fast');
console.log(`PASS: ${seed.length} published references; item/monster/map/quest/recipe validation, English names, limits, unsafe-path rejection, identity mismatch and parser performance.`);

for(const bad of [null,{}, {...record,notice:null}, {...record,fields:[null]}, {...record,fields:[{label:'HP',value:'검'}]}, {...record,sections:[null]}, {...record,sections:[{title:'Maps',total:-1,links:[]}]}, {...record,checkedAt:'1999-01-01'}])assert.equal(validGuide(bad,record.path),false);
const {searchGuide}=await import('./lib/guide-contract.ts');
const directory=seed.map(({path,name,kind})=>({path,name,kind}));assert.ok(searchGuide(directory,'snail','monster').every(e=>e.kind==='monster'&&e.name.toLowerCase().includes('snail')));assert.equal(searchGuide(directory,'not a real thing').length,0);assert.ok(searchGuide(directory,'').length<=40);
const structured=fixture('recipe','Test Hat','<dl><dt>Skill</dt><dd>Tailoring</dd></dl>')+'<script id="ccplz-entity">'+JSON.stringify({recipe:{facts:{meso:300,resultCount:1,success:60},ingredients:[{name:'Snail Shell',canonical_slug:'snail-shell',quantity:5}]}})+'</script>';
const recipe=parseGuide(structured,'/recipes/test-hat');assert.equal(recipe.fields.find(f=>f.label==='Crafting fee (mesos)').value,'300');assert.equal(recipe.sections.find(s=>s.title==='Ingredients').links[0].detail,'Quantity: 5');
console.log('PASS: malformed caches, English facts, directory filtering, and structured recipe details.');
