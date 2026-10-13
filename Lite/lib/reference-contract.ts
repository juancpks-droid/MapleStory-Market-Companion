import {english} from './guide-contract.ts';
const sources:Record<string,string>={'CCPLZ':'ccplz.cc','NiaMeowDB':'meowdb.com','MapleStory Quest':'maplestory.quest'};
const url=(v:unknown,source:string)=>{try{if(typeof v!=='string'||v.length>1000)return false;const u=new URL(v);return u.protocol==='https:'&&u.hostname===sources[source]&&!u.username&&!u.password&&!u.port;}catch{return false}};
const price=(v:unknown)=>v==null||(typeof v==='number'&&Number.isSafeInteger(v)&&v>=0);
export function validReference(r:any,itemKey:string,kind:string){
 if(!r||r.schemaVersion!==1||r.item?.key!==itemKey||!r.item||!english(r.item.name)||r.kind!==kind||!['npc','drops'].includes(kind)||typeof r.checkedAt!=='string'||!(Date.parse(r.checkedAt)>=Date.UTC(2000,0,1)&&Date.parse(r.checkedAt)<=Date.now()+120000))return false;
 const d=r.data;if(!d||!sources[d.source]||!url(d.sourceUrl||d.url,d.source))return false;
 if(kind==='npc')return price(d.sell)&&price(d.buy)&&(d.sell!=null||d.buy!=null);
 return Array.isArray(d.monsters)&&d.monsters.length>0&&d.monsters.length<=100&&d.monsters.every((m:any)=>m&&english(m.name)&&m.name.length<=160&&url(m.url,d.source)&&english(m.evidence)&&Array.isArray(m.maps)&&m.maps.length<=100&&m.maps.every((p:any)=>p&&english(p.name)&&p.name.length<=200&&url(p.url,d.source)));
}
