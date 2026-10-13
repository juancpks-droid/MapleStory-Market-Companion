// Shared by the browser and server; deliberately excludes source readers.
export type GuideLink={name:string;path:string;detail:string};
export type Guide={schemaVersion:1;path:string;kind:string;name:string;source:string;sourceUrl:string;checkedAt:string;notice:string;fields:{label:string;value:string}[];sections:{title:string;links:GuideLink[];total:number}[]};
export const kinds:Record<string,string>={items:'item',monsters:'monster',maps:'map',npcs:'npc',shops:'shop',quests:'quest',recipes:'recipe'};
export const guidePath=(path:unknown):path is string=>typeof path==='string'&&path.length<180&&/^\/(items|monsters|maps|npcs|shops|quests|recipes)\/[a-z0-9]+(?:-[a-z0-9]+)*$/.test(path);
export const english=(value:unknown):value is string=>typeof value==='string'&&!!value.trim()&&!/[\u1100-\u11ff\u2e80-\u9fff\uac00-\ud7af]/u.test(value);
const nameKey=(s:string)=>s.normalize().replace(/’/g,"'").replace(/\s*\(Lv\.?\s*\d+\)\s*$/i,'').trim().toLowerCase().replace(/\s+/g,' ');
export function validGuide(v:any,path:unknown,expectedName=''):v is Guide{
 return !!v&&guidePath(path)&&v.schemaVersion===1&&v.path===path&&v.kind===kinds[path.split('/')[1]]&&english(v.name)&&v.name.length<=160&&(!expectedName||nameKey(v.name)===nameKey(expectedName))&&v.source==='CCPLZ'&&v.sourceUrl==='https://ccplz.cc'+path&&typeof v.notice==='string'&&v.notice.length<=2000&&typeof v.checkedAt==='string'&&Date.parse(v.checkedAt)>=Date.UTC(2000,0,1)&&Date.parse(v.checkedAt)<=Date.now()+120000&&Array.isArray(v.fields)&&v.fields.length<=70&&v.fields.every((f:any)=>f&&english(f.label)&&f.label.length<100&&english(f.value)&&f.value.length<300)&&Array.isArray(v.sections)&&v.sections.length<=23&&v.sections.every((s:any)=>s&&english(s.title)&&s.title.length<=160&&Number.isSafeInteger(s.total)&&s.total>=s.links?.length&&Array.isArray(s.links)&&s.links.length<=60&&s.links.every((l:any)=>l&&english(l.name)&&l.name.length<=160&&typeof l.detail==='string'&&l.detail.length<=250&&(!l.detail||english(l.detail))&&guidePath(l.path)))&&(v.fields.length>0||v.sections.length>0);
}
export type GuideEntry={path:string;name:string;kind:string};
export function validEntry(v:any):v is GuideEntry{return !!v&&guidePath(v.path)&&english(v.name)&&v.name.length<=160&&v.kind===kinds[v.path.split('/')[1]];}
export function searchGuide(entries:GuideEntry[],query:string,kind='all'){
 const tokens=query.trim().toLowerCase().split(/\s+/).filter(Boolean);
 return entries.filter(e=>(kind==='all'||e.kind===kind)&&tokens.every(t=>e.name.toLowerCase().includes(t))).slice(0,40);
}
