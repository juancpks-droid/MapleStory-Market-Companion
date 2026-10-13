export type Item={id:string;name:string;category:string;iconId?:string;classic?:boolean;aliasOf?:string;sourceName?:string;sourceId?:string};
export const searchKey=(s:string)=>s.toLowerCase().replace(/\b(?:for)\b/g,' ').replace(/\b(?:att|watk|atk)\b/g,'attack').replace(/[^a-z0-9]+/g,' ').trim().replace(/\s+/g,' ');
const indexes=new WeakMap<Item[],{item:Item;name:string;tokens:Set<string>}[]>();
function indexed(catalog:Item[]){let rows=indexes.get(catalog);if(!rows){rows=catalog.map(item=>{const name=searchKey(item.name);return {item,name,tokens:new Set(name.split(' '))}});indexes.set(catalog,rows)}return rows;}
export function matchItems(catalog:Item[],query:string,category:string){
 const terms=searchKey(query).split(' ').filter(Boolean);
 const matches:Item[]=[],id=query.trim();for(const row of indexed(catalog)){const i=row.item;if((category==='All'||i.category===category)&&(!i.aliasOf||i.id===id)&&terms.every(t=>(/^\d+$/.test(t)?row.tokens.has(t):row.name.includes(t))||i.id===id)){matches.push(i);if(matches.length===40)break;}}return matches;
}
export function exactItem(catalog:Item[],query:string,category:string){
 const key=searchKey(query);if(!key)return null;
 const matches=indexed(catalog).filter(row=>(category==='All'||row.item.category===category)&&(row.item.id===query.trim()||row.name===key)).map(row=>row.item);
 return matches.length===1?matches[0]:null;
}
export const cacheKey=(tab:string,id:string)=>`maple-lite-v3-${tab}-${id}`;
