using System.Collections;
using System.Reflection;
using KOS.Models;

namespace KOS.Services;

public sealed class CadAnalysisService
{
    public async Task<List<EstimateItem>> AnalyzeAsync(DrawingItem drawing, IProgress<string>? progress, CancellationToken token)
    {
        return await Task.Run(() => AnalyzeCore(drawing, progress, token), token);
    }

    private List<EstimateItem> AnalyzeCore(DrawingItem drawing, IProgress<string>? progress, CancellationToken token)
    {
        progress?.Report($"{drawing.FileName} 읽는 중");
        var result = new List<EstimateItem>();
        var readerType = ResolveReaderType(Path.GetExtension(drawing.StoredPath));
        if (readerType is null) throw new InvalidOperationException("ACadSharp DWG/DXF Reader 형식을 찾지 못했습니다.");

        object? reader = null;
        object? document = null;
        try
        {
            reader = Activator.CreateInstance(readerType, drawing.StoredPath)
                     ?? throw new InvalidOperationException("CAD Reader 생성 실패");
            var readMethod = readerType.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .FirstOrDefault(m => m.Name is "Read" or "ReadDocument" && m.GetParameters().Length == 0)
                ?? throw new InvalidOperationException("CAD 읽기 메서드를 찾지 못했습니다.");
            document = readMethod.Invoke(reader, null)
                       ?? throw new InvalidOperationException("CAD 문서가 비어 있습니다.");

            var entities = GetEntities(document).ToList();
            drawing.ObjectCount = entities.Count;
            var index = 0;
            foreach (var e in entities)
            {
                token.ThrowIfCancellationRequested();
                index++;
                if (index % 5000 == 0) progress?.Report($"{drawing.FileName}: {index:N0}/{entities.Count:N0}");
                var candidate = ConvertEntity(e, drawing);
                if (candidate is not null) result.Add(candidate);
            }
        }
        finally
        {
            (reader as IDisposable)?.Dispose();
            (document as IDisposable)?.Dispose();
        }

        drawing.Status = "완료";
        return result;
    }

    private static Type? ResolveReaderType(string extension)
    {
        var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "ACadSharp")
                  ?? Assembly.Load("ACadSharp");
        var preferred = extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase)
            ? new[] { "ACadSharp.IO.DXF.DxfReader", "ACadSharp.IO.DxfReader" }
            : new[] { "ACadSharp.IO.DWG.DwgReader", "ACadSharp.IO.DwgReader" };
        return preferred.Select(asm.GetType).FirstOrDefault(x => x is not null)
               ?? asm.GetTypes().FirstOrDefault(t => t.Name.EndsWith(extension.Equals(".dxf", StringComparison.OrdinalIgnoreCase) ? "DxfReader" : "DwgReader"));
    }

    private static IEnumerable<object> GetEntities(object document)
    {
        var props = new[] { "Entities", "ModelSpace", "EntityCollection" };
        foreach (var name in props)
        {
            var value = document.GetType().GetProperty(name)?.GetValue(document);
            if (value is IEnumerable direct)
                foreach (var x in direct) if (x is not null) yield return x;
        }

        var blockRecords = document.GetType().GetProperty("BlockRecords")?.GetValue(document) as IEnumerable;
        if (blockRecords is null) yield break;
        foreach (var block in blockRecords)
        {
            if (block is null) continue;
            var entities = block.GetType().GetProperty("Entities")?.GetValue(block) as IEnumerable;
            if (entities is null) continue;
            foreach (var x in entities) if (x is not null) yield return x;
        }
    }

    private static EstimateItem? ConvertEntity(object entity, DrawingItem drawing)
    {
        var type = entity.GetType().Name.ToUpperInvariant();
        var layer = ReadString(entity, "Layer") ?? ReadNestedString(entity, "Layer", "Name") ?? "";
        var handle = ReadString(entity, "Handle") ?? "";
        var block = ReadString(entity, "Name") ?? ReadNestedString(entity, "Block", "Name") ?? "";
        var combined = $"{type} {layer} {block}".ToUpperInvariant();

        if (ContainsAny(combined, "DIMENSION", "TEXT", "MTEXT", "HATCH", "LEADER", "VIEWPORT", "TITLE", "BORDER", "도곽", "치수"))
            return null;

        if (type.Contains("INSERT") || type.Contains("BLOCKREFERENCE"))
        {
            var family = InferFamily(combined);
            if (family is null) return null;
            return Create(drawing, layer, handle, family.Value.trade, family.Value.code, family.Value.name, block, "개소", 1, "유효 블록 개수");
        }

        var area = ReadDouble(entity, "Area");
        var length = ReadDouble(entity, "Length") ?? ReadDouble(entity, "Perimeter");
        var closed = ReadBool(entity, "IsClosed") ?? ReadBool(entity, "Closed") ?? false;

        if (area is > 0 && closed)
        {
            var f = InferAreaFamily(combined);
            return Create(drawing, layer, handle, f.trade, f.code, f.name, f.spec, "㎡", NormalizeArea(area.Value), "폐합 형상 면적");
        }

        if (length is > 0)
        {
            var f = InferLengthFamily(combined);
            return Create(drawing, layer, handle, f.trade, f.code, f.name, f.spec, "m", NormalizeLength(length.Value), "선형 객체 길이");
        }

        return null;
    }

    private static EstimateItem Create(DrawingItem d, string layer, string handle, string trade, string tradeCode, string name, string spec, string unit, double qty, string formula) =>
        new()
        {
            Trade = trade, TradeCode = tradeCode, ItemName = name, Specification = spec,
            Unit = unit, Quantity = Math.Round(qty, 4), DrawingFile = d.FileName,
            SourceHandle = handle, SourceLayer = layer, Formula = formula,
            Level = d.Level, Zone = d.Zone, Status = "품목·규격 검토 필요"
        };

    private static (string trade, string code, string name)? InferFamily(string s)
    {
        if (ContainsAny(s, "DOOR", "문")) return ("창호공사", "08", "문 설치");
        if (ContainsAny(s, "WINDOW", "WIN", "창호", "창")) return ("창호공사", "08", "창호 설치");
        if (ContainsAny(s, "SANITARY", "TOILET", "위생")) return ("기계설비공사", "16", "위생기구 설치");
        if (ContainsAny(s, "LIGHT", "LAMP", "조명")) return ("전기공사", "17", "조명기구 설치");
        return null;
    }

    private static (string trade, string code, string name, string spec) InferAreaFamily(string s)
    {
        if (ContainsAny(s, "CEIL", "천장")) return ("수장공사", "10", "천장 마감", "");
        if (ContainsAny(s, "FLOOR", "바닥", "SLAB")) return ("수장공사", "10", "바닥 마감", "");
        if (ContainsAny(s, "WALL", "벽", "PARTITION")) return ("수장공사", "10", "벽체 마감", "");
        if (ContainsAny(s, "WATERPROOF", "방수")) return ("방수공사", "06", "방수", "");
        if (ContainsAny(s, "CONC", "콘크리트")) return ("철근콘크리트공사", "05", "콘크리트 면적 후보", "");
        return ("미분류", "99", "면적 산출 후보", "");
    }

    private static (string trade, string code, string name, string spec) InferLengthFamily(string s)
    {
        if (ContainsAny(s, "PIPE", "배관")) return ("기계설비공사", "16", "배관", "");
        if (ContainsAny(s, "DUCT", "덕트")) return ("기계설비공사", "16", "덕트", "");
        if (ContainsAny(s, "WALL", "벽", "PARTITION")) return ("수장공사", "10", "벽체 기준선", "");
        if (ContainsAny(s, "SKIRT", "걸레받이")) return ("수장공사", "10", "걸레받이", "");
        return ("미분류", "99", "길이 산출 후보", "");
    }

    private static double NormalizeLength(double v) => v > 1000 ? v / 1000d : v;
    private static double NormalizeArea(double v) => v > 1_000_000 ? v / 1_000_000d : v;
    private static bool ContainsAny(string s, params string[] values) => values.Any(s.Contains);
    private static string? ReadString(object o, string p) => o.GetType().GetProperty(p)?.GetValue(o)?.ToString();
    private static string? ReadNestedString(object o, string p, string nested) => o.GetType().GetProperty(p)?.GetValue(o)?.GetType().GetProperty(nested)?.GetValue(o.GetType().GetProperty(p)?.GetValue(o)!)?.ToString();
    private static double? ReadDouble(object o, string p)
    {
        var v = o.GetType().GetProperty(p)?.GetValue(o);
        if (v is null) return null;
        try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture); } catch { return null; }
    }
    private static bool? ReadBool(object o, string p)
    {
        var v = o.GetType().GetProperty(p)?.GetValue(o);
        if (v is bool b) return b;
        try { return Convert.ToBoolean(v); } catch { return null; }
    }
}
