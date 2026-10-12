using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Market.Core;
public sealed partial class Store : IDisposable
{
    readonly SqliteConnection db;
    List<Observation>? historyCache;
    public Store(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()); db.Open();
        Run("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; CREATE TABLE IF NOT EXISTS observations (key TEXT PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS history (id TEXT PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS items (name TEXT PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS settings (id INTEGER PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS fm_summaries (key TEXT PRIMARY KEY, payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS alerts (id TEXT PRIMARY KEY, stamp TEXT NOT NULL);");InitializeProfiles();MigrateVariantKeys();
    }
    void Run(string sql, params (string, object?)[] p) { using var c = db.CreateCommand(); c.CommandText = sql; foreach (var (k,v) in p) c.Parameters.AddWithValue(k, v ?? DBNull.Value); c.ExecuteNonQuery(); }
    public void Save(IEnumerable<Observation> rows)
    {
        using var tx = db.BeginTransaction();
        foreach (var o in rows)
        {
            o.Validate(); var payload = JsonSerializer.Serialize(o, Json.Options);
            using var c = db.CreateCommand(); c.Transaction = tx;
            c.CommandText = "INSERT INTO observations VALUES ($k,$p) ON CONFLICT(key) DO UPDATE SET payload=excluded.payload; INSERT OR IGNORE INTO history VALUES ($h,$p);";
            c.Parameters.AddWithValue("$k", o.Key); c.Parameters.AddWithValue("$p", payload);
            // No timestamp means a single undated sighting, not a fresh historical event per poll.
            c.Parameters.AddWithValue("$h", o.Key + "|" + o.ObservedAt?.ToString("O") + "|" + o.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)); c.ExecuteNonQuery();
        }
        tx.Commit();historyCache=null;
    }
    List<T> Read<T>(string sql, params (string,object)[] parameters) { using var c = db.CreateCommand(); c.CommandText = sql; foreach(var (key,value) in parameters)c.Parameters.AddWithValue(key,value); using var r = c.ExecuteReader(); var list = new List<T>(); while(r.Read()) { var x = JsonSerializer.Deserialize<T>(r.GetString(0), Json.Options); if (x is not null) list.Add(x); } return list; }
    public List<Observation> Observations() => Read<Observation>("SELECT payload FROM observations");
    public List<Observation> History() => (historyCache??=Read<Observation>("SELECT payload FROM history")).ToList();
    public void ExpireMissingFromSnapshot(string provider, IEnumerable<Observation> current, IEnumerable<string>? itemIds=null)
    {
        var keys=current.Select(o=>o.Key).ToHashSet();
        var ids=itemIds?.ToHashSet();
        foreach(var old in Observations().Where(o=>o.Provider==provider && (ids is null||ids.Contains(o.ItemId)) && (o.Kind==EvidenceKind.Asking||o.Kind==EvidenceKind.Buying) && o.Available && !keys.Contains(o.Key)))
            Run("UPDATE observations SET payload=$p WHERE key=$k",("$p",JsonSerializer.Serialize(old with{Available=false},Json.Options)),("$k",old.Key));
    }
    public List<FmSummary> Summaries()=>Read<FmSummary>("SELECT payload FROM fm_summaries");
    public void ReplaceSummaries(IEnumerable<FmSummary> summaries)
    {
        using var tx=db.BeginTransaction();using var command=db.CreateCommand();command.Transaction=tx;command.CommandText="DELETE FROM fm_summaries";command.ExecuteNonQuery();
        foreach(var row in summaries){command.CommandText="INSERT INTO fm_summaries VALUES($k,$p)";command.Parameters.Clear();command.Parameters.AddWithValue("$k",row.ItemId);command.Parameters.AddWithValue("$p",JsonSerializer.Serialize(row,Json.Options));command.ExecuteNonQuery();}tx.Commit();
    }
    public List<SavedItem> Items() => ItemsFor(Profile);
    public Settings LoadSettings() { var s = Read<Settings>("SELECT payload FROM settings WHERE id=1").FirstOrDefault() ?? new(); s.Sanitize(); return s; }
    public void SaveSettings(Settings s) { s.Sanitize(); Run("INSERT INTO settings VALUES(1,$p) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", ("$p", JsonSerializer.Serialize(s, Json.Options))); }
    public void SaveItem(SavedItem i)=>ChangeInventory("Save "+i.Name,()=>Run("INSERT INTO profile_items VALUES($profile,$n,$p) ON CONFLICT(profile,name) DO UPDATE SET payload=excluded.payload",("$profile",Profile),("$n",ItemIdentity.Key(i)),("$p",JsonSerializer.Serialize(i,Json.Options))));
    public void RemoveItem(string name,string variant="")=>ChangeInventory("Remove "+name,()=>Run("DELETE FROM profile_items WHERE profile=$profile AND name=$n",("$profile",Profile),("$n",ItemIdentity.Key(name,variant))));
    public void ReplaceItems(List<SavedItem> items)=>ChangeInventory("Import / reconcile inventory",()=>ApplyBag(items));
    public bool WasAlerted(string key) { using var c=db.CreateCommand(); c.CommandText="SELECT COUNT(*) FROM alerts WHERE id=$k"; c.Parameters.AddWithValue("$k",key); return (long)c.ExecuteScalar()! > 0; }
    public void MarkAlert(string key) => Run("INSERT OR IGNORE INTO alerts VALUES($k,$t)", ("$k",key),("$t",DateTimeOffset.UtcNow.ToString("O")));
    public void ClearMarket(){Run("DELETE FROM observations; DELETE FROM history; DELETE FROM fm_summaries;");historyCache=null;}
    public void Dispose() => db.Dispose();
}
