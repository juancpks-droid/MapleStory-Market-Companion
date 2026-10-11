namespace Market.Core;



public sealed record Valuation(decimal Value, decimal Quick, decimal Normal, decimal High, decimal MaxBuy, int Confidence, int Count, string Basis);

public sealed record Deal(Observation Listing, Valuation Estimate, decimal Profit, decimal Discount, decimal Roi, string Rating);

public static class ValuationEngine

{

    // One listing is one observation, even if a provider is checked repeatedly.

    public static List<Observation> Deduplicate(IEnumerable<Observation> source) => source

        .GroupBy(x => string.IsNullOrWhiteSpace(x.OriginalReference) ? x.Key : "origin:" + x.OriginalReference)

        .Select(g => g.OrderByDescending(o => o.RetrievedAt).First())

        .GroupBy(x => string.IsNullOrWhiteSpace(x.Seller) ? x.Key : $"{x.MatchKey}|{TextKey.Normalize(x.Seller)}|{x.UnitPrice}|{x.Quantity}|{x.ObservedAt:yyyy-MM-dd}")

        .Select(g => g.OrderByDescending(o => o.RetrievedAt).First()).ToList();



    public static Valuation? Estimate(IEnumerable<Observation> source, Observation target, DateTimeOffset now, TimeSpan maxAge, decimal discount, string? excludeKey = null)

    {

        if (target.Equipment && !target.VariantComplete) return null;

        var rows = Deduplicate(source.Where(o => o.Key != excludeKey && o.MatchKey == target.MatchKey && o.Fresh(now, maxAge)

            && (!o.Equipment || o.VariantComplete) && !o.PriceApproximate && o.Kind is EvidenceKind.Asking or EvidenceKind.ReportedSale or EvidenceKind.ConfirmedSale));

        // Reported and confirmed sales are never described as the same evidence type.

        var confirmed = rows.Where(o => o.Kind == EvidenceKind.ConfirmedSale).ToList();

        var reported = rows.Where(o => o.Kind == EvidenceKind.ReportedSale).ToList();

        var asking = rows.Where(o => o.Kind == EvidenceKind.Asking && o.Available).ToList();

        var pool = confirmed.Count >= 3 ? confirmed : reported.Count >= 4 ? reported : asking;

        int minimum = pool == confirmed ? 3 : pool == reported ? 4 : 3;

        if (pool.Count < minimum) return null;

        var median = Median(pool.Select(o => o.UnitPrice));

        var mad = Median(pool.Select(o => Math.Abs(o.UnitPrice - median)));

        var filtered = pool.Where(o => mad == 0 ? o.UnitPrice >= median / 2 && o.UnitPrice <= median * 2 : Math.Abs(o.UnitPrice - median) <= 3 * mad).ToList();

        if (filtered.Count < minimum) return null;

        int sellers = filtered.Select(o => string.IsNullOrWhiteSpace(o.Seller) ? "unknown" : TextKey.Normalize(o.Seller)).Distinct().Count();

        if (sellers < 3) return null;

        decimal totalWeight = filtered.Sum(o => Weight(o, now));

        decimal cumulative = 0, fair = 0;

        foreach (var row in filtered.OrderBy(o => o.UnitPrice)) { cumulative += Weight(row, now); if (cumulative >= totalWeight / 2) { fair = row.UnitPrice; break; } }

        var dispersion = fair == 0 ? 1 : Median(filtered.Select(o => Math.Abs(o.UnitPrice - fair))) / fair;

        int confidence = Math.Clamp((pool == confirmed ? 65 : pool == reported ? 50 : 35) + Math.Min(15, filtered.Count) + Math.Min(10, sellers) - (int)(dispersion * 50), 0, pool == asking ? 60 : 95);

        if(pool==asking&&filtered.Count<5)confidence=Math.Min(confidence,40);
        return new(fair, Math.Floor(fair * .9m), fair, Math.Ceiling(fair * 1.1m), Math.Floor(fair * (1 - discount / 100)), confidence, filtered.Count,

            pool == confirmed ? "Provider-confirmed sales" : pool == reported ? "Reported sales (unverified)" : "Asking-price estimate (uncertain)");

    }

    static decimal Weight(Observation o, DateTimeOffset now) => 1m / (1m + (decimal)Math.Max(0, (now - o.ObservedAt!.Value).TotalHours) / 24m);

    public static decimal Median(IEnumerable<decimal> values) { var a = values.Order().ToArray(); if (a.Length == 0) throw new ArgumentException("No values"); return a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2; }

    public static List<Deal> Find(IEnumerable<Observation> observations, Settings settings, DateTimeOffset now)

    {

        var rows = Deduplicate(observations); var result = new List<Deal>(); var age = TimeSpan.FromHours(settings.FreshHours);

        foreach(var group in rows.GroupBy(o=>o.MatchKey))

        {

        // Bound CPU for busy feeds: recent 500 comparable records, 200 candidate asks per variant.

        var comparison=group.OrderByDescending(o=>o.ObservedAt).Take(500).ToList();

        foreach (var row in comparison.Where(o => o.Kind == EvidenceKind.Asking && o.Available && !o.PriceApproximate && o.Fresh(now, age) && now - o.RetrievedAt < TimeSpan.FromMinutes(30)).Take(200))

        {

            // Leave the candidate out of the baseline; it must not validate itself.

            var value = Estimate(comparison, row, now, age, settings.Discount, row.Key);

            if (value is null || (value.Basis.StartsWith("Asking")&&value.Count<5)) continue;

            var profit = value.Value - row.UnitPrice; var pct = profit / value.Value * 100;

            if (profit < settings.MinimumProfit || pct < settings.Discount || value.Confidence < settings.Confidence) continue;

            result.Add(new(row, value, profit, pct, profit / row.UnitPrice * 100,

                value.Confidence >= 80 && value.Count >= 8 ? "Strong opportunity" : value.Confidence >= 65 ? "Worth investigating" : "Marginal opportunity"));

        }

        }

        return result.OrderByDescending(d => d.Profit).ToList();

    }

}

