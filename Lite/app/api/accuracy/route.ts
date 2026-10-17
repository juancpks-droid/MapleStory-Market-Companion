import {monsterCatalog} from '../../../lib/accuracy-monsters.ts';
import {parseMonster} from '../../../lib/accuracy-monster-data.ts';
import {publicApi} from '../../../lib/sources.ts';
export async function GET(request:Request){
 const id=Number(new URL(request.url).searchParams.get('id'));
 if(!monsterCatalog.some(m=>m.id===id))return Response.json({error:'Choose a monster from the catalog.'},{status:400});
 try{return Response.json(parseMonster(JSON.parse(await publicApi('https://maplestory.io/api/gms/83/mob/'+id)),id),{headers:{'Cache-Control':'public, max-age=86400'}})}catch{return Response.json({error:'Monster stats unavailable. Retry or enter custom stats.'},{status:503})}
}
