// Independent implementation of the published Classic hit-roll model.
// Reference: https://meowdb.com/msclassic/formulas (checked 2026-10-17).
export const monsters=[['Custom monster',22,13],['Snail',1,0],['Orange Mushroom',8,0],['Green Mushroom',14,8],['Horny Mushroom',22,13],['Zombie Mushroom',24,14],['Wild Boar',25,15],['Zombie Lupin',41,29]] as const;
export function hitChance(level:number,acc:number,mobLevel:number,avoid:number){
 if(![level,acc,mobLevel,avoid].every(Number.isFinite)||level<1||mobLevel<1||acc<0||avoid<0)return NaN;
 const gap=Math.max(0,mobLevel-level),effective=acc*100/(255+10*gap),lead=effective-avoid;
 if(avoid===0)return 1;if(effective===0)return 0;
 if(lead>=25+gap*(0.5*level+15))return 1;
 const spread=0.15+0.2/(1+Math.exp(lead/12));
 return Math.max(0,Math.min(1,(1+spread-avoid/effective)/(2*spread)));
}
export function neededAcc(level:number,mobLevel:number,avoid:number,target:number){
 if(!Number.isFinite(hitChance(level,0,mobLevel,avoid))||!Number.isFinite(target)||target<=0||target>1)return NaN;
 let low=0,high=1;while(hitChance(level,high,mobLevel,avoid)<target&&high<1e8)high*=2;
 if(high>=1e8)return NaN;
 while(low<high){const mid=Math.floor((low+high)/2);if(hitChance(level,mid,mobLevel,avoid)>=target)high=mid;else low=mid+1;}return low;
}
