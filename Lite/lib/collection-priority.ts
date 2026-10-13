import {lookupIdentity} from './scrolls.ts';
import {itemPath} from './guide.ts';
// Curated common consumables/materials and distinct scroll tiers; no player-search telemetry.
export const priorityNames=['Red Potion','Orange Potion','White Potion','Blue Potion','Mana Elixir','Elixir','Power Elixir','Snail Shell','Blue Snail Shell','Orange Mushroom Cap','Slime Bubble','Mushroom Spore','Gloves Attack Scroll: Lesser','Gloves Attack Scroll: Intermediate','Gloves Attack Scroll: Greater','Gloves Attack Scroll: Chaos'];
export function priorityPaths(catalog:any[],cursor=0){const names=priorityNames.slice(cursor,cursor+4).concat(priorityNames.slice(0,Math.max(0,cursor+4-priorityNames.length)));const paths:string[]=[];for(const name of names){const entry=catalog.find(i=>!i.aliasOf&&(i.sourceName||i.name)===name);if(entry)try{paths.push(itemPath(lookupIdentity(entry).name))}catch{}}return {paths:[...new Set(paths)],cursor:(cursor+4)%priorityNames.length};}
