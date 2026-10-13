import seed from './guide-seed.json' with {type:'json'};
import type {Guide} from './guide.ts';
import {guidePath,validGuide,sourceGuide,snapshotName} from './guide.ts';
import {validEntry} from './guide-contract.ts';
import type {GuideEntry} from './guide-contract.ts';
const memory=new Map<string,Guide>(),pending=new Map<string,Promise<Guide>>(),misses=new Map<string,number>();
const week=7*86400000;
export const snapshotBase='https://raw.githubusercontent.com/juancpks-droid/MapleStory-Market-Companion/main/SharedGameData/';
let ready:Promise<D1Database>|undefined;
async function db(){return ready??=(async()=>{try{const d=(await import('cloudflare:workers')).env.DB;if(!d)throw Error('Database unavailable');await d.prepare('CREATE TABLE IF NOT EXISTS guide_reference (path TEXT PRIMARY KEY, body TEXT NOT NULL, checked_at INTEGER NOT NULL)').run();return d;}catch(e){ready=undefined;throw e;}})();}
export async function smallSnapshot(file:string){try{const r=await fetch(snapshotBase+file,{redirect:'error',signal:AbortSignal.timeout(1200)});if(!r.ok)return null;const reader=r.body!.getReader();const chunks:Uint8Array[]=[];let size=0;try{for(;;){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>262144)return null;chunks.push(value);}}finally{await reader.cancel()}const bytes=new Uint8Array(size);let at=0;for(const c of chunks){bytes.set(c,at);at+=c.length}return JSON.parse(new TextDecoder().decode(bytes));}catch{return null;}}
export async function savedGuide(path:string,name=''){let saved=memory.get(path);if(!saved)try{const row=await(await db()).prepare('SELECT body FROM guide_reference WHERE path=?').bind(path).first<{body:string}>();if(row)saved=JSON.parse(row.body);}catch{}if(!validGuide(saved,path,name))saved=seed.find(r=>validGuide(r,path,name)) as Guide|undefined;return validGuide(saved,path,name)?saved:null;}
async function put(record:Guide){if(memory.size>=100)memory.delete(memory.keys().next().value!);memory.set(record.path,record);try{await(await db()).prepare('INSERT INTO guide_reference(path,body,checked_at) VALUES(?,?,?) ON CONFLICT(path) DO UPDATE SET body=excluded.body,checked_at=excluded.checked_at').bind(record.path,JSON.stringify(record),Date.parse(record.checkedAt)).run()}catch{}return record;}
export async function guide(path:string,name='',refresh=false):Promise<Guide>{
 if(!guidePath(path))throw Error('Invalid reference');
 const saved=await savedGuide(path,name);if(saved&&!refresh)return saved;
 if(pending.has(path)){const result=await pending.get(path)!;if(!validGuide(result,path,name))throw Error('Reference identity mismatch');return result;}
 if(pending.size>=4){if(saved)return saved;throw Error('Reference lookups are busy. Retry shortly.');}
 if((misses.get(path)||0)>Date.now()){if(saved)return saved;throw Error('Reference unavailable. Retry in a few minutes.');}
 const job=(async()=>{try{const published=await smallSnapshot('guides/'+snapshotName(path));if(validGuide(published,path,name)&&(!saved||Date.parse(published.checkedAt)>Date.parse(saved.checkedAt)))return await put(published);const fresh=await sourceGuide(path,name);return await put(fresh);}catch(e){if(misses.size>=200)misses.delete(misses.keys().next().value!);misses.set(path,Date.now()+300000);if(saved)return saved;throw e;}finally{pending.delete(path)}})();pending.set(path,job);return job;
}
export const isOld=(record:Guide)=>Date.now()-Date.parse(record.checkedAt)>week;
let directory:GuideEntry[]|undefined,indexAt=0,indexPending:Promise<GuideEntry[]>|undefined;
export async function guideDirectory(){
 if(directory&&Date.now()-indexAt<3600000)return directory;
 return indexPending??=(async()=>{try{const value=await smallSnapshot('guide-index.json');
 if(value?.schemaVersion===1&&Array.isArray(value.entries)&&value.entries.length<=2000&&value.entries.every(validEntry))directory=value.entries;
 return directory??=seed.filter(r=>validGuide(r,r.path)).map(({path,name,kind})=>({path,name,kind}));
 }finally{indexAt=Date.now();indexPending=undefined;}})();
}
