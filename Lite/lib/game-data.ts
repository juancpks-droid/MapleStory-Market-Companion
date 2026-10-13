import catalog from '../public/catalog.json' with {type:'json'};
import {lookupIdentity} from './scrolls.ts';
import {drops,npcReference} from './sources.ts';

export type Kind='drops'|'npc';
const catalogById=new Map(catalog.map(item=>[item.id,item]));
const pending=new Map<string,Promise<any>>();
const hot=new Map<string,any>();
const TTL=7*86400000;
export function identity(id:string){const item=catalogById.get(id);if(!item)throw Error('Unknown item');const source=lookupIdentity(item);return {key:('sourceId'in item?'classic:':'gms83:')+source.id,name:source.name,id:source.id,category:item.category};}
export function meaningful(kind:Kind,data:any){return kind==='drops'?Array.isArray(data?.monsters)&&data.monsters.length>0:data?.sell!=null||data?.buy!=null;}
let ready:Promise<D1Database>|undefined;
async function db(){return ready??=(async()=>{try{const database=(await import('cloudflare:workers')).env.DB;if(!database)throw Error('Database unavailable');await database.prepare('CREATE TABLE IF NOT EXISTS game_reference (key TEXT PRIMARY KEY, item_key TEXT NOT NULL, kind TEXT NOT NULL, body TEXT NOT NULL, checked_at INTEGER NOT NULL)').run();return database;}catch(e){ready=undefined;throw e;}})();}
export async function readRecord(id:string,kind:Kind){const item=identity(id),key=item.key+'/'+kind;let record=hot.get(key);if(!record){try{const row=await (await db()).prepare('SELECT body FROM game_reference WHERE key=?').bind(key).first<{body:string}>();if(row)record=JSON.parse(row.body);}catch{}if(record)remember(key,record);}return record?{...record,stale:Date.now()-Date.parse(record.checkedAt)>TTL}:null;}
function remember(key:string,record:any){if(hot.size>=200)hot.delete(hot.keys().next().value!);hot.set(key,record);}
export async function saveRecord(id:string,kind:Kind,data:any){if(!meaningful(kind,data))return null;const item=identity(id),key=item.key+'/'+kind;
 if(kind==='drops'&&data.monsters.some((m:any)=>m.mapsUnavailable||m.mapsPending)){const previous=await readRecord(id,kind);if(previous)return previous;}
 const checkedAt=data.checkedAt||new Date().toISOString();
 const record={schemaVersion:1,item,kind,checkedAt,stale:false,data:{...data,checkedAt},provenance:{source:data.source,sourceUrl:data.sourceUrl||data.url||'',coverage:'Reference data; community reports and historical evidence are labelled separately. Not independently verified Classic World facts.'}};
 try{await (await db()).prepare('INSERT INTO game_reference (key,item_key,kind,body,checked_at) VALUES (?,?,?,?,?) ON CONFLICT(key) DO UPDATE SET body=excluded.body,checked_at=excluded.checked_at').bind(key,item.key,kind,JSON.stringify(record),Date.parse(checkedAt)).run();}catch{/* Source results remain usable during a database outage. */}
 remember(key,record);return record;
}
export async function reference(id:string,kind:Kind,refresh=false){const cached=await readRecord(id,kind);if(cached&&!refresh)return cached;
 const item=identity(id),key=item.key+'/'+kind;if(pending.has(key))return pending.get(key);if(pending.size>=4){if(cached)return cached;throw Error('Reference lookups are busy');}
 const job=(async()=>{try{const data=kind==='drops'?await drops(item.name):await npcReference(item.name,item.id);const saved=await saveRecord(id,kind,data);return saved||cached||{schemaVersion:1,item,kind,checkedAt:new Date().toISOString(),stale:false,data};}catch(e){if(cached)return cached;throw e;}finally{pending.delete(key)}})();pending.set(key,job);return job;
}
export async function coverage(){const database=await db();const counts=await database.prepare('SELECT kind, COUNT(*) AS records FROM game_reference GROUP BY kind').all();return {schemaVersion:1,catalogItems:catalog.length,records:counts.results,refreshDays:7,scope:'NPC prices and monster/map references. Market offers are fetched separately.',sources:['NiaMeowDB','CCPLZ','MapleStory Quest']};}
