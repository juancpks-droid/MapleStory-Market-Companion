export const LITE_VERSION='0.6.0';
export function rememberItem(ids:string[],id:string,limit=12){return [id,...ids.filter(x=>typeof x==='string'&&x!==id)].slice(0,limit);}
export function toggleFavorite(ids:string[],id:string){return ids.includes(id)?ids.filter(x=>x!==id):rememberItem(ids,id,50);}
export function priceContext(data:any,now=Date.now()){
 const offers=Array.isArray(data?.offers)?data.offers:[];
 const recent=offers.filter((o:any)=>Number.isFinite(Date.parse(o.at))&&Date.parse(o.at)<=now&&now-Date.parse(o.at)<86400000).length;
 const at=Date.parse(data?.checkedAt),age=Number.isFinite(at)?Math.max(0,Math.floor((now-at)/60000)):null;
 return `${data?.marketAvailable===false?'Market source unavailable; any offers below are saved observations. ':''}${age===null?'Check time unavailable':age<1?'Checked just now':`Checked ${age} min ago`} · ${recent} listings posted in the last 24 hours · ${offers.length-recent} older/undated listings. Asking prices are not completed sales; equipment stats may differ. ${recent?'':'Not enough recent offers for a current market value. '}NPC prices are separate reference values.`;
}
export function keepSavedMarket(saved:any,fresh:any){return fresh?.marketAvailable===false&&Array.isArray(saved?.offers)&&saved.offers.length?{...fresh,offers:saved.offers,ask:null,buy:null,checkedAt:saved.checkedAt,note:'Market refresh failed. Saved offers retained; availability is unverified. '+(fresh.note||'')}:fresh;}
export function problemReport(item:{id:string;name:string},mode:string,data:any,message:string){
 // Deliberately exclude player names, offer notes, favorites and browser/device identifiers.
 return [`Maple Market Lite ${LITE_VERSION}`,`Item: ${item.name} (#${item.id})`,`Tool: ${mode==='prices'?'Price Checker':'Item Finder'}`,`Source: ${data?.source|| (mode==='prices'?'Kerning / NPC references':'Reference lookup')}`,`Checked: ${data?.checkedAt||'Not available'}`,`Offers returned: ${Array.isArray(data?.offers)?data.offers.length:0}`,`Drop references: ${Array.isArray(data?.monsters)?data.monsters.length:0}`,`Status: ${message||'No error shown'}`,'Describe what looks wrong: '].join('\n');
}
