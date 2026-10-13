export type Item={id:string;name:string;category:string;iconId?:string;classic?:boolean;aliasOf?:string;sourceName?:string;sourceId?:string};
export const searchKey=(s:string)=>s.toLowerCase().replace(/\b(?:for)\b/g,' ').replace(/\b(?:att|watk|atk)\b/g,'attack').replace(/[^a-z0-9]+/g,' ').trim().replace(/\s+/g,' ');
export function matchItems(catalog:Item[],query:string,category:string){
 const terms=searchKey(query).split(' ').filter(Boolean);
 return catalog.filter(i=>(category==='All'||i.category===category)&&(!i.aliasOf||i.id===query.trim())&&terms.every(t=>(/^\d+$/.test(t)?searchKey(i.name).split(' ').includes(t):searchKey(i.name).includes(t))||i.id===query.trim())).slice(0,40);
}
export function exactItem(catalog:Item[],query:string,category:string){
 const key=searchKey(query);if(!key)return null;
 const matches=catalog.filter(i=>(category==='All'||i.category===category)&&(i.id===query.trim()||searchKey(i.name)===key));
 return matches.length===1?matches[0]:null;
}
export const cacheKey=(tab:string,id:string)=>`maple-lite-v3-${tab}-${id}`;
