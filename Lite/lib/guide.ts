import {load} from 'cheerio/slim';
import {key,page} from './sources.ts';

import {guidePath,english,kinds,validGuide} from './guide-contract.ts';
import type {Guide,GuideLink} from './guide-contract.ts';
export {guidePath,validGuide} from './guide-contract.ts';
export type {Guide,GuideLink} from './guide-contract.ts';
export const itemPath=(name:string)=>'/items/'+key(name).replace(/[^a-z0-9]+/g,'-').replace(/^-|-$/g,'');
const clean=(value:string)=>value.replace(/\s+/g,' ').trim();
export function parseGuide(html:string,path:string,expectedName=''):Guide{
 if(!guidePath(path))throw Error('Unsupported reference path');
 const $=load(html),root=$('article[data-ssr]').first(),name=clean(root.find('h1').first().text()),kind=kinds[path.split('/')[1]];
 if(!root.length||!english(name)||root.attr('data-ssr')!==kind||(expectedName&&key(name)!==key(expectedName)))throw Error('Reference identity mismatch');
 const fields:Guide['fields']=[],sections:Guide['sections']=[];
 root.find('dl dt').each((_,node)=>{const label=clean($(node).text()),value=clean($(node).next('dd').text());if(english(label)&&english(value)&&label.length<100&&value.length<300&&fields.length<60)fields.push({label,value});});
 root.find('section').each((_,section)=>{const title=clean($(section).children('h2,h3').first().text());if(!english(title)||sections.length>=20)return;const all:GuideLink[]=[];$(section).find('a[href]').each((_,a)=>{const path=$(a).attr('href')||'',name=clean($(a).text());if(!guidePath(path)||!english(name)||name.length>160)return;const row=$(a).closest('li,tr');const detail=clean(row.find('span').map((_,n)=>$(n).text()).get().join(' · ')).slice(0,250);if(!all.some(x=>x.path===path&&x.detail===detail))all.push({name,path,detail:english(detail)?detail:''});});if(all.length)sections.push({title,links:all.slice(0,60),total:all.length});});
 // Scalar recipe facts and structured ingredients are facts, not quest/story prose.
 try{const entity=JSON.parse($('#ccplz-entity').text())[kind],facts=entity?.facts||{};
  const names:Record<string,string>={meso:'Crafting fee (mesos)',resultCount:'Result quantity',skillLevel:'Required skill level',success:'Success rate (%)',reqLevel:'Required level',reqSTR:'Required STR',reqDEX:'Required DEX',reqINT:'Required INT',reqLUK:'Required LUK',tradeBlock:'Trade restriction flag',slotMax:'Stack limit'};
  if(kind==='item'){const prices=(entity?.graph?.incoming||[]).filter((x:any)=>x.canonical_relation_type==='sells_item'&&x.target?.entity_type==='shop'&&x.qualifiers?.currency==='Mesos'&&Number.isFinite(x.qualifiers?.price)&&x.qualifiers.price>=0).map((x:any)=>x.qualifiers.price);if(prices.length)fields.push({label:'NPC buy price (lowest recorded)',value:String(Math.min(...prices))+' mesos'});}
  for(const [field,label] of Object.entries(names))if(typeof facts[field]==='number'&&Number.isFinite(facts[field])&&!fields.some(x=>x.label===label))fields.push({label,value:String(facts[field])});
  if(kind==='recipe')for(const [field,title] of [['ingredients','Ingredients'],['result','Result'],['catalyst','Catalyst']] as const){const values=Array.isArray(entity?.[field])?entity[field]:entity?.[field]?[entity[field]]:[];const links=values.filter((x:any)=>english(x.name)&&guidePath('/items/'+x.canonical_slug)).map((x:any)=>({name:x.name,path:'/items/'+x.canonical_slug,detail:typeof x.quantity==='number'?`Quantity: ${x.quantity}`:''}));if(links.length&&!sections.some(x=>x.title===title))sections.push({title,links:links.slice(0,60),total:links.length});}
 }catch{}
 if(!fields.length&&!sections.length)throw Error('No factual reference available');
 const result:Guide={schemaVersion:1,path,kind,name,source:'CCPLZ',sourceUrl:'https://ccplz.cc'+path,checkedAt:new Date().toISOString(),notice:'CCPLZ reference; game versions may differ. Reported and historical drops are not independently confirmed Classic drops. Missing information is not proof of absence. Lists may be partial.',fields,sections};
 if(!validGuide(result,path,expectedName))throw Error('Invalid factual reference');return result;
}
export async function sourceGuide(path:string,name=''){return parseGuide(await page('https://ccplz.cc'+path),path,name);}
export const snapshotName=(path:string)=>path.slice(1).replace('/','--')+'.json';
