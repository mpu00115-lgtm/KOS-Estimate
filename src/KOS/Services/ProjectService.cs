using System.Text.Json;
using KOS.Models;

namespace KOS.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Root { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KOS", "Projects");

    public ProjectService() => Directory.CreateDirectory(Root);

    public List<KosProject> LoadAll() =>
        Directory.EnumerateFiles(Root, "project.json", SearchOption.AllDirectories)
            .Select(path => { try { return JsonSerializer.Deserialize<KosProject>(File.ReadAllText(path), Options); } catch { return null; } })
            .Where(x => x is not null)
            .Cast<KosProject>()
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();

    public KosProject Create(string name, string address, string client, string workType, string note)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("프로젝트명을 입력하세요.");
        var p = new KosProject { Name = name.Trim(), Address = address.Trim(), Client = client.Trim(), WorkType = workType.Trim(), Note = note.Trim() };
        EnsureFolders(p);
        Save(p);
        return p;
    }

    public void Save(KosProject project)
    {
        project.UpdatedAt = DateTime.Now;
        EnsureFolders(project);
        File.WriteAllText(Path.Combine(ProjectFolder(project), "project.json"), JsonSerializer.Serialize(project, Options));
    }

    public string ProjectFolder(KosProject p) => Path.Combine(Root, p.Id);
    public string DrawingsFolder(KosProject p) => Path.Combine(ProjectFolder(p), "Drawings");
    public string AnalysisFolder(KosProject p) => Path.Combine(ProjectFolder(p), "Analysis");
    public string OutputFolder(KosProject p) => Path.Combine(ProjectFolder(p), "Output");
    public string LogsFolder(KosProject p) => Path.Combine(ProjectFolder(p), "Logs");

    public List<DrawingItem> AddFiles(KosProject project, IEnumerable<string> files)
    {
        var result = new List<DrawingItem>();
        foreach (var source in files.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var ext = Path.GetExtension(source);
            if (!ext.Equals(".dwg", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".dxf", StringComparison.OrdinalIgnoreCase)) continue;
            var target = UniquePath(DrawingsFolder(project), Path.GetFileName(source));
            File.Copy(source, target);
            var d = new DrawingItem { FileName = Path.GetFileName(target), StoredPath = target, OriginalPath = source };
            project.Drawings.Add(d);
            result.Add(d);
        }
        Save(project);
        return result;
    }

    public void RemoveDrawing(KosProject project, DrawingItem item)
    {
        project.Drawings.Remove(item);
        try { if (File.Exists(item.StoredPath)) File.Delete(item.StoredPath); } catch { }
        Save(project);
    }

    private void EnsureFolders(KosProject p)
    {
        Directory.CreateDirectory(ProjectFolder(p));
        Directory.CreateDirectory(DrawingsFolder(p));
        Directory.CreateDirectory(AnalysisFolder(p));
        Directory.CreateDirectory(OutputFolder(p));
        Directory.CreateDirectory(LogsFolder(p));
    }

    private static string UniquePath(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var i = 2;
        while (File.Exists(path)) path = Path.Combine(folder, $"{stem}_{i++}{ext}");
        return path;
    }
}
