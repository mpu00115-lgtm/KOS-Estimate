using System.Text.Json;
using KOS.Models;

namespace KOS.Services;

public sealed class CostService
{
    private readonly List<CostRecord> _records = new();

    public CostService()
    {
        Load(Path.Combine(AppContext.BaseDirectory, "Data", "official_cost_items_2026H1.json"), "조달청 2026");
        Load(Path.Combine(AppContext.BaseDirectory, "Data", "xcost_reference_catalog.json"), "XCost 공개 참조");
    }

    public int RecordCount => _records.Count;

    public void Match(IEnumerable<EstimateItem> items)
    {
        foreach (var item in items)
        {
            var best = _records
                .Select(r => (r, score: Score(item, r)))
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            if (best.r is null || best.score < 45)
            {
                item.PriceSource = "단가 미확정";
                item.Status = item.Status.Contains("검토") ? item.Status : "단가 검토 필요";
                continue;
            }

            item.ItemCode = best.r.Code;
            if (string.IsNullOrWhiteSpace(item.Specification)) item.Specification = best.r.Specification;
            if (string.IsNullOrWhiteSpace(item.Unit)) item.Unit = best.r.Unit;
            item.MaterialUnitPrice = best.r.Material;
            item.LaborUnitPrice = best.r.Labor;
            item.ExpenseUnitPrice = best.r.Expense;
            item.PriceSource = best.r.Source;
            item.Status = best.score >= 75 ? "자동 매칭" : "단가 후보 검토";
            item.Notify();
        }
    }

    private void Load(string path, string source)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var obj in EnumerateObjects(doc.RootElement))
            {
                var code = Pick(obj, "code", "Code", "itemCode", "ItemCode", "xcostCode");
                var name = Pick(obj, "name", "Name", "itemName", "ItemName");
                if (string.IsNullOrWhiteSpace(name)) continue;
                _records.Add(new CostRecord
                {
                    Code = code,
                    Name = name,
                    Specification = Pick(obj, "spec", "Spec", "specification", "Specification", "stad", "STAD"),
                    Unit = Pick(obj, "unit", "Unit", "UNIT"),
                    Material = PickNumber(obj, "material", "Material", "materialCost", "MaterialCost", "COST_MATERIAL_1"),
                    Labor = PickNumber(obj, "labor", "Labor", "laborCost", "LaborCost", "COST_LABOR_1"),
                    Expense = PickNumber(obj, "expense", "Expense", "expenseCost", "ExpenseCost", "COST_EXPEN_1"),
                    Source = source
                });
            }
        }
        catch { }
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object)
        {
            yield return e;
            foreach (var p in e.EnumerateObject())
                foreach (var x in EnumerateObjects(p.Value)) yield return x;
        }
        else if (e.ValueKind == JsonValueKind.Array)
            foreach (var i in e.EnumerateArray())
                foreach (var x in EnumerateObjects(i)) yield return x;
    }

    private static int Score(EstimateItem i, CostRecord r)
    {
        var a = Normalize($"{i.ItemName} {i.Specification} {i.Unit}");
        var b = Normalize($"{r.Name} {r.Specification} {r.Unit}");
        var tokens = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hits = tokens.Count(t => b.Contains(t));
        var score = tokens.Length == 0 ? 0 : hits * 100 / tokens.Length;
        if (!string.IsNullOrWhiteSpace(i.Unit) && Normalize(i.Unit) == Normalize(r.Unit)) score += 15;
        return Math.Min(100, score);
    }

    private static string Normalize(string s) => s.ToUpperInvariant()
        .Replace("㎡", "M2").Replace("㎥", "M3").Replace("개소", "EA").Replace("개", "EA")
        .Replace(" ", " ").Trim();

    private static string Pick(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                return v.ToString();
        return "";
    }

    private static double PickNumber(JsonElement e, params string[] names)
    {
        foreach (var n in names)
            if (e.TryGetProperty(n, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
                if (double.TryParse(v.ToString(), out d)) return d;
            }
        return 0;
    }
}
