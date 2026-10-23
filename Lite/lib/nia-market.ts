import {load} from 'cheerio/slim';
import {page,key} from './sources.ts';
const host='https://meowdb.com';
export function resolveNia(html:string,name:string){const $=load(html);const paths=[...new Set($('a[href]').toArray().filter(a=>/^\/msclassic\/item-db\/\d+$/.test($(a).attr('href')||'')&&key($(a).text())===key(name)).map(a=>$(a).attr('href')!))];if(paths.length!==1)throw Error('No unique NiaMeowDB item match');return host+paths[0]}
const amount=(x:any)=>typeof x==='number'&&Number.isSafeInteger(x)&&x>0?x:null;
export function parseNia(summary:any,listings:any,id:number,name:string,url:string,now=Date.now()){
 if(summary.itemId!==id||summary.condition!=='clean'||!Array.isArray(listings.listings))throw Error('NiaMeowDB response did not match this item');
 const offers=listings.listings.filter((r:any)=>r.status==='active'&&['sell','buy'].includes(r.side)&&Number.isSafeInteger(r.id)&&r.id>0).slice(0,60).map((r:any)=>{
 const at=typeof r.updatedAt==='string'?r.updatedAt.replace(' ','T')+(r.updatedAt.endsWith('Z')?'':'Z'):'';
 return {id:r.id,side:r.side,price:amount(r.price),quantity:amount(r.quantity),seller:String(r.ign||r.username||'Seller not provided').slice(0,100),condition:String(r.condition||'Unspecified'),stats:Object.entries(r.stats||{}).filter(([,v])=>typeof v==='number'&&Number.isFinite(v)).map(([k,v])=>`${k}: ${v}`).join(' · '),location:[r.channel!=null?'CH '+r.channel:'',r.fmRoom!=null?'FM '+r.fmRoom:'',r.location||''].filter(Boolean).join(' · ').slice(0,300),note:String(r.note||'').slice(0,500),at:Number.isFinite(Date.parse(at))&&Date.parse(at)<=now+120000?at:null};});
 return {name,url,source:'NiaMeowDB',usual:amount(summary.usual),lowest:amount(summary.cheapestSell),bestBuy:amount(summary.bestBuy),newest:typeof summary.newestPriceAt==='string'?summary.newestPriceAt:null,offers,partial:listings.hasMoreActive===true||listings.listings.length>60,checkedAt:new Date(now).toISOString()};
}
export async function niaMarket(name:string){const url=resolveNia(await page(host+'/msclassic/item-db/all'),name),$=load(await page(url));if(key($('h1').text())!==key(name))throw Error('NiaMeowDB item identity changed');const id=Number(url.split('/').pop());const [a,b]=await Promise.all([page(host+`/msclassic/api/item-market-summary?itemId=${id}&condition=clean`),page(host+`/msclassic/api/market-listings?itemId=${id}&offers=1`)]);return parseNia(JSON.parse(a),JSON.parse(b),id,name,url)}
