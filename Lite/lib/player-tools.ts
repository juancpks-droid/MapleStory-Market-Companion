export const LITE_VERSION='0.7.0';
export function rememberItem(ids:string[],id:string,limit=12){return [id,...ids.filter(x=>typeof x==='string'&&x!==id)].slice(0,limit);}
export function toggleFavorite(ids:string[],id:string){return ids.includes(id)?ids.filter(x=>x!==id):rememberItem(ids,id,50);}
export function priceContext(data:any,now=Date.now()){
 const offers=Array.isArray(data?.offers)?data.offers:[];
 const recent=offers.filter((o:any)=>Number.isFinite(Date.parse(o.at))&&Date.parse(o.at)<=now&&now-Date.parse(o.at)<86400000).length;
 const at=Date.parse(data?.checkedAt),age=Number.isFinite(at)?Math.max(0,Math.floor((now-at)/60000)):null;
 return `Player market: Kerning Exchange only. ${data?.marketAvailable===false?'Market source unavailable; any offers below are saved observations. ':''}${age===null?'Check time unavailable':age<1?'Checked just now':`Checked ${age} min ago`} · ${recent} listings posted in the last 24 hours · ${offers.length-recent} older/undated listings. Asking prices are not completed sales; equipment stats may differ. ${recent?'':'Not enough recent offers for a current market value. '}NPC prices are separate reference values.`;
}
export function kerningMarket(data:any,now=Date.now()){
 if(!data)return data;
 const offers=(Array.isArray(data.offers)?data.offers:[]).filter((o:any)=>o?.source==='Kerning Exchange'&&(o.kind==='sell'||o.kind==='buy')&&Number.isFinite(o.price)&&o.price>0);
 const recent=data.marketAvailable===false?[]:offers.filter((o:any)=>Number.isFinite(Date.parse(o.at))&&Date.parse(o.at)<=now&&now-Date.parse(o.at)<86400000);
 const asks=recent.filter((o:any)=>o.kind==='sell').map((o:any)=>o.price),buys=recent.filter((o:any)=>o.kind==='buy').map((o:any)=>o.price);
 let sourceUrl='https://kerning.gg';try{const url=new URL(data.sourceUrl);if(url.protocol==='https:'&&url.hostname==='kerning.gg'&&!url.username&&!url.password)sourceUrl=url.href;}catch{}
 return {...data,offers,sourceUrl,ask:asks.length?Math.min(...asks):null,buy:buys.length?Math.max(...buys):null};
}
export function keepSavedMarket(saved:any,fresh:any){saved=kerningMarket(saved);fresh=kerningMarket(fresh);return fresh?.marketAvailable===false&&saved?.offers?.length?{...fresh,offers:saved.offers,ask:null,buy:null,checkedAt:saved.checkedAt,note:'Kerning refresh failed. Saved Kerning offers retained; availability is unverified. '+(fresh.note||'')}:fresh;}
export function problemReport(item:{id:string;name:string},mode:string,data:any,message:string){
 // Deliberately exclude player names, offer notes, favorites and browser/device identifiers.
 return [`Maple Market Lite ${LITE_VERSION}`,`Item: ${item.name} (#${item.id})`,`Tool: ${mode==='prices'?'Price Checker':'Item Finder'}`,`Source: ${data?.source|| (mode==='prices'?'Kerning / NPC references':'Reference lookup')}`,`Checked: ${data?.checkedAt||'Not available'}`,`Offers returned: ${Array.isArray(data?.offers)?data.offers.length:0}`,`Drop references: ${Array.isArray(data?.monsters)?data.monsters.length:0}`,`Status: ${message||'No error shown'}`,'Describe what looks wrong: '].join('\n');
}
