import catalog from '../../../public/catalog.json';
import {lookupIdentity} from '../../../lib/scrolls.ts';
import {niaMarket} from '../../../lib/nia-market.ts';
const pending=new Map<string,Promise<any>>();
export async function GET(request:Request){const id=new URL(request.url).searchParams.get('id');const item=catalog.find(i=>i.id===id);if(!item)return Response.json({error:'Choose a catalog item.'},{status:400});try{const identity=lookupIdentity(item);if(pending.size>=8&&!pending.has(identity.name))return Response.json({error:'Lookups are busy; retry shortly.'},{status:429});if(!pending.has(identity.name))pending.set(identity.name,niaMarket(identity.name).finally(()=>pending.delete(identity.name)));return Response.json(await pending.get(identity.name),{headers:{'Cache-Control':'private, max-age=60'}})}catch{return Response.json({error:'NiaMeowDB could not return a verified match or is temporarily unavailable. Existing prices and NPC references remain available.'},{status:503})}}
