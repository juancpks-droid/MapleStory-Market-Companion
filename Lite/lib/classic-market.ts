import {marketKey} from './scrolls.ts';
export const CLASSIC_FEED='https://maplestory-classic.com/market.json';
export type ClassicPrice={name:string;median:number;sales:number;updatedAt:string;history:{date:string;median:number;sales:number}[];source:string};
const positive=(n:unknown):n is number=>typeof n==='number'&&Number.isFinite(n)&&n>0&&n<=1e12;
export function parseClassicFeed(raw:any,now=Date.now()):ClassicPrice[]{
 const updated=Date.parse(raw?.updatedAt);
 if(!Number.isFinite(updated)||updated>now+300000||updated<Date.UTC(2020,0,1)||!Array.isArray(raw.items)||raw.items.length>10000)throw Error('Invalid price snapshot');
 const items:ClassicPrice[]=[];
 for(const r of raw.items){
  if(typeof r.n!=='string'||!r.n.trim()||r.n.length>200||typeof r.s!=='string'||!/^[a-z0-9-]+$/.test(r.s)||!positive(r.p)||!Number.isInteger(r.sold)||r.sold<1||!Array.isArray(r.h))continue;
  const history=r.h.filter((h:any)=>Array.isArray(h)&&h.length===3&&typeof h[0]==='string'&&/^\d{4}-\d{2}-\d{2}$/.test(h[0])&&Number.isFinite(Date.parse(h[0]))&&new Date(h[0]).toISOString().slice(0,10)===h[0]&&Date.parse(h[0])>=Date.UTC(2020,0,1)&&Date.parse(h[0])<=updated&&positive(h[1])&&Number.isInteger(h[2])&&h[2]>0).map((h:any)=>({date:h[0],median:h[1],sales:h[2]})).sort((a:any,b:any)=>a.date.localeCompare(b.date));
  if(new Set(history.map((h:any)=>h.date)).size!==history.length)continue;
  items.push({name:r.n,median:r.p,sales:r.sold,updatedAt:new Date(updated).toISOString(),history,source:'MapleStory-Classic.com'});
 }
 if(!items.length)throw Error('No valid prices');return items;
}
export function matchClassicPrice(items:ClassicPrice[],name:string){const matches=items.filter(x=>marketKey(x.name)===marketKey(name));return matches.length===1?matches[0]:null;}
