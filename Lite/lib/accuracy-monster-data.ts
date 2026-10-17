import {monsterCatalog} from './accuracy-monsters.ts';
export function parseMonster(raw:any,id:number){
 const m=monsterCatalog.find(m=>m.id===id),level=raw?.meta?.level,avoid=raw?.meta?.evasion;
 if(!m||raw?.id!==id||raw?.name!==m.name||!Number.isInteger(level)||level<1||level>200||!Number.isInteger(avoid)||avoid<0||avoid>100000)throw Error('Monster stats unavailable or identity mismatch');
 return {id,level,avoid};
}
