import {monsterCatalog} from './accuracy-monsters.ts';
export const displayMonster=(name:string)=>/[a-z]/i.test(name)&&!(/transparent|\btest\b|placeholder/i.test(name));
export const finderCatalog=monsterCatalog.filter(m=>displayMonster(m.name));
export function fallbackMonster(name:string,stats?:{id:number;level:number;avoid:number;fields?:{label:string;value:string}[]}){
 const matches=finderCatalog.filter(m=>m.name===name),levels=[...new Set(matches.map(m=>m.level))].sort((a,b)=>a-b);
 const verified=matches.length===1&&stats?.id===matches[0].id;
 return {name,source:'Historical GMS 83 · MapleStory.io',notice:'Historical fallback stats; may differ from Classic World. Drop and map reference unavailable; this does not mean there are none.'+(matches.length>1?' Multiple historical variants share this name. Choose an ID in Accuracy for exact variant stats.':''),fields:verified?stats.fields||[{label:'Level',value:String(stats.level)},{label:'Avoidability',value:String(stats.avoid)}]:[{label:'Level',value:levels.join(' / ')||'Unknown'},{label:'Avoidability',value:'Unknown'},{label:'Catalog IDs',value:matches.map(m=>m.id).join(', ')}],sections:[]};
}
