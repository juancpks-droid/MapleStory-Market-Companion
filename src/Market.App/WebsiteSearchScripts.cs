using Market.Core;
using System.Text.Json;

namespace Market.App;
internal static class WebsiteSearchScripts
{
    public static string Input(WebsiteSearchMode mode)=>mode switch
    {
        WebsiteSearchMode.Nia=>"input[aria-label='Search priced items']",
        WebsiteSearchMode.Kerning=>"input[aria-label='Search MapleStory Classic items']",
        WebsiteSearchMode.Basil=>"input[aria-label='Search item prices']",
        WebsiteSearchMode.Mesolane=>"input[aria-label='Search market']",
        WebsiteSearchMode.MapleBuddy=>"input[type='search']",
        WebsiteSearchMode.Metaroad=>"input[aria-label='Search any item: scrolls, ores, equipment, potions…']",
        WebsiteSearchMode.GameMargins=>"input[placeholder='Search items, e.g. potion']",
        WebsiteSearchMode.MesoWatch=>"input[aria-label='Filter items']",
        WebsiteSearchMode.Sleepywood=>"input[placeholder='Search items, monsters, maps, quests, NPCs...']",
        WebsiteSearchMode.MesoScout=>"input[placeholder='Name fragments or item ID']",_=>""
    };
    const string Normalize="const norm=s=>(s||'').normalize('NFKC').replace(/[‘’]/g,\"'\").replace(/\\s+/g,' ').trim().toLowerCase();";
    public static string Fill(WebsiteSearchMode mode,string name,bool retry=false)
    {
        return "(()=>{"+Normalize+"const wanted="+JsonSerializer.Serialize(name)+";const e=document.querySelector("+JsonSerializer.Serialize(Input(mode))+");if(!e)return 'waiting';"+
            (retry?"if(norm(e.value)!==norm(wanted)&&e.value!=='')return 'user-changed';":"")+
            "const set=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;set.call(e,'');e.dispatchEvent(new Event('input',{bubbles:true}));set.call(e,wanted);e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new Event('change',{bubbles:true}));e.focus();e.scrollIntoView({block:'center'});return 'filled';})()";
    }
    // Each selector is restricted to a verified item-result control, never a trade or account action.
    public static string Match(WebsiteSearchMode mode,string name,bool activate=true)
    {
        var (selector,nameExpression)=mode switch
        {
            WebsiteSearchMode.Nia=>("a[href]","e.textContent.replace(/ open for the price$/,'')"),
            WebsiteSearchMode.Kerning=>("[role='listbox'] [role='option']","e.querySelector('.sugg-name')?.textContent"),
            WebsiteSearchMode.Basil=>("[role='listbox'] [role='option'] button","e.querySelector('span.font-semibold')?.textContent"),
            WebsiteSearchMode.Mesolane=>("a[role='option'][href^='/item/']","e.querySelector('.uiItemName')?.textContent"),
            WebsiteSearchMode.MapleBuddy=>("tr[role='button'][aria-label$=': open details']","e.querySelector('[data-col=name]')?.textContent"),
            WebsiteSearchMode.Metaroad=>("[role='listbox'] [role='option'] button","e.querySelector('.min-w-0 > span:first-child')?.textContent"),
            WebsiteSearchMode.GameMargins=>("a[href^='/maplestory-classic/item/']","e.textContent"),
            WebsiteSearchMode.MesoWatch=>("button.item-select","e.textContent"),_ =>("nosuchitem","''")
        };
        var nia=mode==WebsiteSearchMode.Nia?"&&/^\\/msclassic\\/item-db\\/\\d+$/.test(e.getAttribute('href'))":"";
        return "(()=>{"+Normalize+"const wanted=norm("+JsonSerializer.Serialize(name)+");const all=[...document.querySelectorAll("+JsonSerializer.Serialize(selector)+")].filter(e=>e.getClientRects().length&&norm("+nameExpression+")===wanted"+nia+");"+
            "const unique=[...new Map(all.map(e=>[e.getAttribute('href')||e,e])).values()];if(unique.length!==1)return unique.length?'ambiguous':'waiting';"+
            (activate?(mode==WebsiteSearchMode.Kerning?"unique[0].dispatchEvent(new MouseEvent('mousedown',{bubbles:true,button:0}));":"unique[0].click();"):"")+"return 'opened';})()";
    }
}
