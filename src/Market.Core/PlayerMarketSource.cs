namespace Market.Core;
public static class PlayerMarketSource
{
 public const string Provider="Kerning Exchange";
 public static IEnumerable<Observation> Only(IEnumerable<Observation> rows)=>rows.Where(o=>o.Provider==Provider);
}
