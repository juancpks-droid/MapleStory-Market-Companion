// Bounded public-reference publisher. Runs on GitHub, never on players' devices.
import {mkdir,readFile,writeFile,rename,readdir} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {createHash} from 'node:crypto';
import {priorityPaths} from '../lib/collection-priority.ts';
import {validReference} from '../lib/reference-contract.ts';
import catalog from '../public/catalog.json' with {type:'json'};
import {lookupIdentity} from '../lib/scrolls.ts';
import {sourceGuide,itemPath,guidePath,validGuide,snapshotName} from '../lib/guide.ts';
const target=resolve(process.argv[2]||'../SharedGameData'),budget=Math.min(24,Math.max(1,Number(process.env.REFERENCE_BATCH)||18));
await mkdir(join(target,'guides'),{recursive:true});await mkdir(join(target,'records'),{recursive:true});
const read=async p=>{try{return JSON.parse(await readFile(p,'utf8'))}catch{return null}};
const save=async(p,value)=>{await writeFile(p+'.tmp',JSON.stringify(value,null,2)+'\n');await rename(p+'.tmp',p)};
const state=await read(join(target,'collector-state.json'))||{cursor:0,queue:['/items/blue-moonlight','/items/snail-shell','/items/orange-mushroom-cap','/items/gloves-attack-scroll-intermediate','/items/red-potion','/monsters/snail','/maps/snail-hunting-ground-i','/quests/sam-s-suggestion','/recipes/craft-brown-skullcap']};
const candidates=catalog.filter(x=>!x.aliasOf&&x.category!=='Cash'&&x.category!=='Set-up'),names=new Map();for(const item of candidates){try{const identity=lookupIdentity(item),path=itemPath(identity.name);if(!names.has(path))names.set(path,{item,identity});}catch{}}
for(let n=0;n<6;n++){const item=candidates[state.cursor++%candidates.length];try{state.queue.push(itemPath(lookupIdentity(item).name))}catch{}}
state.cursor%=candidates.length;
const old=[];for(const file of await readdir(join(target,'guides'))){if(!file.endsWith('.json'))continue;const record=await read(join(target,'guides',file));if(record&&Date.parse(record.checkedAt)<Date.now()-7*86400000)old.push(record)}
old.sort((a,b)=>Date.parse(a.checkedAt)-Date.parse(b.checkedAt));
const priority=priorityPaths(candidates,state.priorityCursor||0);state.priorityCursor=priority.cursor;
const queue=[...new Set([...old.slice(0,4).map(x=>x.path),...priority.paths,...state.queue])].filter(guidePath);let attempted=0,updated=0;const failed=[];
while(queue.length&&attempted<budget){const path=queue.shift(),file=join(target,'guides',snapshotName(path)),previous=await read(file);if(validGuide(previous,path)&&Date.parse(previous.checkedAt)>Date.now()-7*86400000)continue;attempted++;
 try{const known=names.get(path),record=await sourceGuide(path,known?.identity.name||'');if(!validGuide(record,path,known?.identity.name||''))throw Error('Invalid source record');if(previous&&previous.fields.length>0&&!record.fields.length)throw Error('Incomplete source record');await save(file,record);updated++;
  for(const section of record.sections)for(const link of section.links)if(!queue.includes(link.path)&&queue.length<2000)queue.push(link.path);
  // Exact source-name match; retain the catalog namespace, never join on an equal numeric ID alone.
  if(known){const sellField=record.fields.find(f=>f.label==='NPC sell price'),sell=sellField?.value.match(/^([\d,]+) mesos?$/);if(sell){const {item,identity}=known,id=item.sourceId?'classic:'+identity.id:identity.id,key=item.sourceId?'classic:'+identity.id:'gms83:'+identity.id;const data={sell:Number(sell[1].replaceAll(',','')),buy:(record.fields.find(f=>f.label==='NPC buy price (lowest recorded)')?.value.match(/^([\d,]+) mesos?$/)?.[1]||'').replaceAll(',','')||null,source:'CCPLZ',url:record.sourceUrl,checkedAt:record.checkedAt};if(data.buy!==null)data.buy=Number(data.buy);const previousNpc=await read(join(target,'records','npc-'+id.replace(':','-')+'.json'));if(previousNpc?.data?.buy!=null&&data.buy===null)continue;await save(join(target,'records','npc-'+id.replace(':','-')+'.json'),{schemaVersion:1,item:{key,name:identity.name,id:identity.id,category:item.category},kind:'npc',checkedAt:record.checkedAt,data,provenance:{source:'CCPLZ',sourceUrl:record.sourceUrl,coverage:record.notice}});}}
  console.log('Updated '+path);
 }catch(e){failed.push(path);console.log('Kept previous data: '+path+' ('+e.message+')');}
}
state.queue=[...new Set([...queue,...failed])].slice(0,2000);
const indexEntries=[];const entries=[];for(const directory of ['guides','records'])for(const file of await readdir(join(target,directory))){if(!file.endsWith('.json'))continue;const bytes=await readFile(join(target,directory,file)),value=JSON.parse(bytes);if(bytes.length>262144)throw Error('Record exceeds size budget');if(directory==='guides'&&!validGuide(value,value.path))throw Error('Invalid published guide');if(directory==='records'&&!validReference(value,value.item?.key,value.kind))throw Error('Invalid published reference: '+file);if(directory==='guides')indexEntries.push({path:value.path,name:value.name,kind:value.kind});entries.push({file:directory+'/'+file,bytes:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex'),checkedAt:value.checkedAt,kind:value.kind});}
await save(join(target,'manifest.json'),{schemaVersion:1,publishedAt:new Date().toISOString(),catalogItems:catalog.length,guideRecords:entries.filter(x=>x.file.startsWith('guides/')).length,referenceRecords:entries.filter(x=>x.file.startsWith('records/')).length,entries,partial:true,notice:'Selected factual references; coverage is incomplete. Market listings are separate.'});
console.log(JSON.stringify({attempted,updated,failed:failed.length,queued:state.queue.length,records:entries.length}));

const index={schemaVersion:1,partial:true,entries:indexEntries.sort((a,b)=>a.name.localeCompare(b.name)).slice(0,2000)};while(Buffer.byteLength(JSON.stringify(index,null,2))>250000)index.entries.pop();await save(join(target,'guide-index.json'),index);
await save(join(target,'collector-state.json'),state);
