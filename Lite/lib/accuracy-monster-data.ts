import {monsterCatalog} from './accuracy-monsters.ts';
export function parseMonster(raw:any,id:number){
 const m=monsterCatalog.find(m=>m.id===id),level=raw?.meta?.level,avoid=raw?.meta?.evasion;
 if(!m||raw?.id!==id||raw?.name!==m.name||!Number.isInteger(level)||level<1||level>200||!Number.isInteger(avoid)||avoid<0||avoid>100000)throw Error('Monster stats unavailable or identity mismatch');
 const fields=[{label:'Level',value:String(level)},{label:'Avoidability',value:String(avoid)},{label:'Catalog ID',value:String(id)}];
 for(const [key,label] of [['maxHP','HP'],['maxMP','MP'],['exp','EXP'],['physicalDamage','Attack'],['physicalDefense','Defense'],['accuracy','Accuracy']]){const n=raw.meta[key];if(Number.isSafeInteger(n)&&n>=0)fields.push({label,value:n.toLocaleString('en-US')});}
 return {id,level,avoid,fields};
}
