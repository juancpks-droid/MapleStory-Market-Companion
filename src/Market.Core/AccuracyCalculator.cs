namespace Market.Core;
// Independent implementation of the hit-roll model published at
// https://meowdb.com/msclassic/formulas, checked 2026-10-17.
public static class AccuracyCalculator {
 public static double HitChance(double level,double acc,double mobLevel,double avoid){
  if(!double.IsFinite(level)||!double.IsFinite(acc)||!double.IsFinite(mobLevel)||!double.IsFinite(avoid)||level<1||mobLevel<1||acc<0||avoid<0)return double.NaN;
  var gap=Math.Max(0,mobLevel-level);var effective=acc*100/(255+10*gap);var lead=effective-avoid;
  if(avoid==0)return 1;if(effective==0)return 0;if(lead>=25+gap*(0.5*level+15))return 1;
  var spread=0.15+0.2/(1+Math.Exp(lead/12));return Math.Clamp((1+spread-avoid/effective)/(2*spread),0,1);
 }
 public static int NeededAcc(double level,double mobLevel,double avoid,double target){
  if(!double.IsFinite(HitChance(level,0,mobLevel,avoid))||!double.IsFinite(target)||target<=0||target>1)throw new ArgumentOutOfRangeException(nameof(target));
  int low=0,high=1;while(HitChance(level,high,mobLevel,avoid)<target&&high<100000000)high*=2;
  if(high>=100000000)throw new ArgumentOutOfRangeException(nameof(avoid));
  while(low<high){int mid=low+(high-low)/2;if(HitChance(level,mid,mobLevel,avoid)>=target)high=mid;else low=mid+1;}return low;
 }
}
