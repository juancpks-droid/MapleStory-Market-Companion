"use client";
import {useEffect,useState,useRef,useMemo} from 'react';
import {Item,matchItems,exactItem,cacheKey} from '../lib/search.ts';
type View={item:Item;tab:string;data:any};
const categories=['All','Equipment','Use','Etc','Set-up','Cash'];
const money=(n:any)=>Number(n).toLocaleString();
export default function Home(){
 const [tab,setTab]=useState('finder'),[query,setQuery]=useState(''),[catalog,setCatalog]=useState<Item[]>([]),[selected,setSelected]=useState<Item|null>(null),[category,setCategory]=useState('All'),[busy,setBusy]=useState(false),[message,setMessage]=useState(''),[help,setHelp]=useState(false),[offline,setOffline]=useState(false),[sessionReady,setSessionReady]=useState(false),[installPrompt,setInstallPrompt]=useState(false);
 const [view,setView]=useState<View|null>(null),[open,setOpen]=useState(false),[settledQuery,setSettledQuery]=useState(''),[active,setActive]=useState(-1);
 const generation=useRef(0),forceLookup=useRef(false),request=useRef<AbortController|null>(null),restoreView=useRef<any>(null);
 const data=view?.data||{},shownItem=view?.item||selected,shownTab=view?.tab||tab;
 const dismissInstall=()=>{setInstallPrompt(false);try{localStorage.setItem('maple-lite-install-dismissed',String(Date.now()))}catch{}};
 useEffect(()=>{const standalone=window.matchMedia('(display-mode: standalone)'),phone=window.matchMedia('(max-width: 600px)');const check=()=>{let dismissed=0;try{dismissed=Number(localStorage.getItem('maple-lite-install-dismissed'))||0}catch{};const installed=standalone.matches||(navigator as Navigator&{standalone?:boolean}).standalone;setInstallPrompt(!installed&&(phone.matches||/iPhone|iPad|iPod/.test(navigator.userAgent))&&Date.now()-dismissed>7*86400000)};check();standalone.addEventListener('change',check);phone.addEventListener('change',check);return()=>{standalone.removeEventListener('change',check);phone.removeEventListener('change',check)}},[]);

 useEffect(()=>{
  let alive=true;
  try{const saved=JSON.parse(localStorage.getItem('maple-lite-session')||'{}');restoreView.current=saved;if(saved.tab==='finder'||saved.tab==='prices')setTab(saved.tab);if(typeof saved.query==='string')setQuery(saved.query);if(categories.includes(saved.category))setCategory(saved.category);}catch{}
  fetch('/catalog.json').then(r=>{if(!r.ok)throw Error();return r.json() as Promise<Item[]>}).then((items:Item[])=>{
   if(!alive)return;setCatalog(items);const saved=restoreView.current;
   if(saved){const item=items.find(i=>i.id===saved.item?.id);if(item)setSelected(item);
    const shown=items.find(i=>i.id===saved.displayed?.id),mode=saved.displayedTab;
    if(shown&&(mode==='finder'||mode==='prices'))try{const data=JSON.parse(localStorage.getItem(cacheKey(mode,shown.id))||'null');if(data)setView({item:shown,tab:mode,data})}catch{}
   }
   setSessionReady(true);
  }).catch(()=>{if(alive)setMessage('Catalog could not load. Reconnect and reload. Your saved search has been kept.');});
  const change=()=>setOffline(!navigator.onLine);change();window.addEventListener('online',change);window.addEventListener('offline',change);
  if('serviceWorker'in navigator)navigator.serviceWorker.register('/sw.js').catch(()=>{});
  return()=>{alive=false;window.removeEventListener('online',change);window.removeEventListener('offline',change)};
 },[]);
 useEffect(()=>{if(!sessionReady)return;const save=()=>{try{localStorage.setItem('maple-lite-session',JSON.stringify({item:selected,tab,query,category,displayed:view?.item,displayedTab:view?.tab}))}catch{}};save();window.addEventListener('pagehide',save);return()=>window.removeEventListener('pagehide',save)},[sessionReady,selected,tab,query,category,view]);
 const matches=useMemo(()=>query===settledQuery?matchItems(catalog,settledQuery,category):[],[catalog,settledQuery,query,category]);
 const choose=(item:Item)=>{request.current?.abort();generation.current++;setOpen(false);setActive(-1);setQuery(item.name);setSelected({...item});};
 useEffect(()=>{const timer=setTimeout(()=>setSettledQuery(query),180);return()=>clearTimeout(timer)},[query]);
 useEffect(()=>{if(!open||!query.trim()||!sessionReady)return;const timer=setTimeout(()=>{const item=exactItem(catalog,query,category);if(item)choose(item);},700);return()=>clearTimeout(timer)},[query,category,open,catalog,sessionReady]);
 const editQuery=(value:string)=>{generation.current++;request.current?.abort();setBusy(false);setMessage('');setQuery(value);setOpen(!!value.trim());setActive(-1);};
 useEffect(()=>{
  if(!selected||!sessionReady)return;
  const item=selected,mode=tab,g=++generation.current,controller=new AbortController(),key=cacheKey(mode,item.id),forced=forceLookup.current;request.current=controller;forceLookup.current=false;
  const current=()=>g===generation.current&&!controller.signal.aborted;
  const publish=(value:any)=>{if(current())setView({item,tab:mode,data:value});};
  let saved:any=null;try{saved=JSON.parse(localStorage.getItem(key)||'null')}catch{}
  setMessage('');setBusy(true);if(saved)publish(saved);
  const fresh=saved&&Date.now()-Date.parse(saved.checkedAt)<(mode==='finder'?86400000:120000)&&!(mode==='finder'&&(!saved.monsters?.length||saved.monsters.some((m:any)=>m.mapsPending||m.mapsUnavailable)));
  if(!navigator.onLine||(!forced&&fresh)){setBusy(false);if(!navigator.onLine)setMessage(saved?'Offline — saved results.':'Offline — previous results kept. Reconnect and tap Retry.');return()=>controller.abort()}
  const get=async(stage='')=>{const r=await fetch(`/api/lookup?id=${encodeURIComponent(item.id)}&mode=${mode}${stage}`,{signal:controller.signal});const d:any=await r.json();if(!r.ok)throw Error(d.error||'Source unavailable. Try again shortly.');return d};
  (async()=>{try{
   if(mode==='finder'&&!saved?.monsters?.length&&!view){const quick=await get('&stage=monsters');if(!current())return;if(quick.monsters?.length){publish(quick);setMessage('Monsters found — loading map references…');}}
   let d=await get();if((d.stale||d.npc?.stale)&&current()){publish(d);setMessage("Saved reference shown — refreshing in the background…");try{d=await get("&refresh=1")}catch{}}if(!current())return;
   if(mode==='finder'&&!d.monsters?.length&&saved?.monsters?.length){setMessage('Source unavailable. Keeping your saved drop references.');return;}
   publish(d);setMessage('');try{localStorage.setItem(key,JSON.stringify(d));const keys=Object.keys(localStorage).filter(k=>k.startsWith('maple-lite-v3-'));while(keys.length>30){const remove=keys.shift();if(remove&&remove!==key)localStorage.removeItem(remove)}}catch{}
  }catch(e:any){if(current())setMessage((navigator.onLine?e.message:'Offline')+' Previous results are kept; tap Retry when ready.');}
  finally{if(current())setBusy(false)}})();
  return()=>controller.abort();
 },[selected,tab,sessionReady]);
 const searchKeyDown=(e:React.KeyboardEvent<HTMLInputElement>)=>{
  if(e.key==='Escape'){setOpen(false);return;}
  if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();setOpen(true);setActive(i=>Math.max(0,Math.min(matches.length-1,i+(e.key==='ArrowDown'?1:-1))));}
  if(e.key==='Enter'){e.preventDefault();const item=active>=0?matches[active]:exactItem(catalog,query,category)||(matches.length===1?matches[0]:null);if(item)choose(item);else{setOpen(true);setMessage('Choose a matching item and percentage below.');}}
 };
 const icon=(item:Item,size=32)=><img width={size} height={size} loading="lazy" src={item.classic?(item.iconId?`https://maplestory.io/api/GMS/83/item/${item.iconId}/icon`:'/icon.png'):`https://maplestory.io/api/GMS/83/item/${item.id}/icon`} alt="" onError={e=>{e.currentTarget.style.visibility='hidden'}}/>;
 return <div className="shell"><header><img src="/icon.png" width="44" height="44" alt="Maple crest"/><div><strong>MAPLE MARKET <span>LITE</span></strong><small>CLASSIC WORLD COMPANION</small></div><button className="help" onClick={()=>setHelp(!help)} aria-label="Install and about">?</button></header>
<nav aria-label="Main tools"><button className={tab==='finder'?'chosen':''} onClick={()=>setTab('finder')} aria-pressed={tab==='finder'}><span>⌕</span>Item Finder</button><button className={tab==='prices'?'chosen':''} onClick={()=>setTab('prices')} aria-pressed={tab==='prices'}><span>◈</span>Price Checker</button></nav>
 {installPrompt&&!help&&<aside className="install install-invite" aria-label="Add to Home Screen"><b>Add Maple Market to your Home Screen?</b><p>Open your item finder and prices with one tap, like an app.</p><div className="install-actions"><button onClick={()=>{dismissInstall();setHelp(true)}}>Show me how</button><button className="quiet" onClick={dismissInstall}>Not now</button></div></aside>}
 {help&&<aside className="install"><b>Keep it on your Home Screen</b><p>On iPhone, open this page in Safari. Tap Share (the square with an up arrow), then Add to Home Screen. Leave Open as Web App on if shown, then tap Add. If the option is missing, look under Edit Actions in the Share menu.</p><p>Your search text, category filter, selected item, tab and recent results stay on this device. Internet is needed for fresh data. Reference drops may differ from Classic World.</p><button onClick={()=>setHelp(false)}>Got it</button></aside>}
 <main><div className="eyebrow">YOUR POCKET FIELD GUIDE</div><h1>{tab==='finder'?'Find your next drop.':'Know the asking price.'}</h1><p className="intro">{tab==='finder'?'Items, monsters and places to explore.':'Player offers and NPC references, in one place.'}</p>
 <div className="search-region"><div className="search"><span aria-hidden="true">⌕</span><input aria-label="Search items" placeholder="Search an item or item ID…" value={query} onChange={e=>editQuery(e.target.value)} onFocus={()=>setOpen(!!query.trim())} onKeyDown={searchKeyDown} role="combobox" aria-autocomplete="list" aria-expanded={open} aria-controls="item-options" aria-activedescendant={open&&active>=0?`item-option-${active}`:undefined} autoComplete="off"/><button aria-label="Clear search" onClick={()=>editQuery('')}>×</button></div>
 <div className="categories">{categories.map(c=><button key={c} className={category===c?'active':''} aria-pressed={category===c} onClick={()=>{setCategory(c);setActive(-1);if(query.trim())setOpen(true);}}>{c}</button>)}</div>
 {offline&&<p className="notice">Offline · saved results only</p>}
 {(open&&query.trim())&&<section className="results search-popup" id="item-options" role="listbox" aria-label="Matching items"><div className="section-label">MATCHING ITEMS<button className="close-results" onClick={()=>setOpen(false)}>Close</button></div>{matches.map((i,index)=><button className={'item-row'+(index===active?' highlighted':'')} id={`item-option-${index}`} role="option" aria-selected={index===active} key={i.id} onClick={()=>choose(i)}><div className="item-art">{icon(i)}</div><div><strong>{i.name}</strong><small>{i.category} · {i.classic?'Classic reference':'GMS 83 reference'}</small></div><span className="arrow">›</span></button>)}{!matches.length&&<p className="empty">No matching items. Try a shorter name or another category.</p>}</section>}</div>
 {!selected&&!open&&<section className="results"><div className="section-label">QUICK PICKS<span>Tap an item to begin</span></div>{catalog.filter(i=>['4000001','4000000','4000016','2000000','1051025','4000009'].includes(i.id)).map(i=><button className="item-row" key={i.id} onClick={()=>choose(i)}><div className="item-art">{icon(i)}</div><div><strong>{i.name}</strong><small>{i.category}</small></div></button>)}</section>}
 {shownItem&&<section id="details"><div className="selected"><div className="item-art large">{icon(shownItem,48)}</div><div><small>{shownItem.category} · {shownItem.classic?'Classic reference':'GMS 83 reference'}</small><h2>{shownItem.name}</h2><span>#{shownItem.id.replace("classic:","")}</span></div></div>
 <div className="loadline" role="status">{busy?<><i/> Checking {selected?.name}… {view&&'Previous results stay visible.'}</>:data.checkedAt?`Checked ${new Date(data.checkedAt).toLocaleString()}`:'Ready to look up'}<button disabled={busy||offline} onClick={()=>{forceLookup.current=true;setSelected(selected?{...selected}:null)}}>{busy?'Loading':'Retry'}</button></div>{message&&<p className="notice">{message}</p>}
 {shownTab==='finder'?<><div className="section-label">MONSTER DROPS & MAPS<span>{data.source||'Reference lookup'}</span></div><p className="muted">{(data.sharedDatabase?'Shared game database · ':'')+(data.stale?'Saved reference may be out of date. ':'')}{data.note||'Reported drops and historical references are labelled separately.'}</p>{data.monsters?.map((m:any)=><article className="monster" key={m.url}><div className="monster-top"><span className="monster-mark">✦</span><h3>{m.name}</h3></div><p className="evidence">{m.evidence}</p><div className="map-list">{m.maps?.length?m.maps.map((x:any)=><div key={x.url}>↳ {x.name}</div>):<span>{m.mapsPending?(busy?'Loading maps…':'Maps could not load. Tap Retry.'):'No map reference available.'}</span>}</div></article>)}{!busy&&!data.monsters?.length&&<div className="empty"><strong>No recorded drops available</strong><p>This does not mean the item cannot drop. Try again later or use the source fallback.</p></div>}</>:<><div className="price-cards">{data.ask!=null&&<article><small>LOWEST PLAYER ASK</small><strong>{money(data.ask)}</strong><span>mesos / unit</span></article>}{data.buy!=null&&<article><small>BEST BUY OFFER</small><strong>{money(data.buy)}</strong><span>mesos / unit</span></article>}{data.npc?.sell!=null&&<article className="npc"><small>SELL TO NPC</small><strong>{money(data.npc.sell)}</strong><span>mesos · reference</span></article>}{data.npc?.buy!=null&&<article className="npc"><small>BUY FROM NPC</small><strong>{money(data.npc.buy)}</strong><span>mesos · lowest recorded</span></article>}</div><p className="muted">{data.note}</p>{data.npc&&<p className="evidence">NPC reference: {data.npc.source}{data.npc.stale?" · older saved reference":""}. May differ by server/version. Not player market value.</p>}{['sell','buy'].map(kind=><section key={kind}><div className="section-label">{kind==='sell'?'FOR SALE':'LOOKING TO BUY'}<span>{data.offers?.filter((x:any)=>x.kind===kind).length||0} offers</span></div>{data.offers?.filter((x:any)=>x.kind===kind).map((o:any)=><article className="offer" key={o.id}><div><span className="tag">{kind==='sell'?'SELL':'BUY'}</span><small>{o.source}</small></div><h3>{money(o.price)} <span>mesos / unit</span></h3><p>{o.quantity?`Quantity ${money(o.quantity)} · `:''}{o.seller||'Seller not provided'}</p>{o.variant&&<p className="evidence">{o.variant}</p>}{o.note&&<p>{o.note}</p>}<small>Posted {new Date(o.at).toLocaleString()}</small>{Date.now()-Date.parse(o.at)>86400000&&<p className="evidence">Older listing · availability may have changed</p>}</article>)}{!busy&&!data.offers?.some((x:any)=>x.kind===kind)&&<p className="empty">No matching public offers returned.</p>}</section>)}</>}
 {!busy&&data.sourceUrl&&<details className="fallback"><summary>Website fallback</summary><p>Use only if the information above is missing.</p><a href={data.sourceUrl} target="_blank" rel="noreferrer">Open original source ↗</a></details>}</section>}
 <footer>Maple Market Lite · 0.3.0<br/>Independent companion · Item artwork © Nexon</footer></main>
 </div>
}

