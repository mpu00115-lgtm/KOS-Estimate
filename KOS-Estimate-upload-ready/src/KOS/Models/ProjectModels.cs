using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KOS.Models;

public sealed class KosProject : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Client { get; set; } = "";
    public string WorkType { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ObservableCollection<DrawingItem> Drawings { get; set; } = new();
    public ObservableCollection<EstimateItem> Estimates { get; set; } = new();
    public ObservableCollection<AnalysisIssue> Issues { get; set; } = new();

    public string Summary => $"도면 {Drawings.Count:N0}개 · 내역 {Estimates.Count:N0}개 · {UpdatedAt:yyyy-MM-dd HH:mm}";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class DrawingItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public string OriginalPath { get; set; } = "";
    public string DrawingType { get; set; } = "미분류";
    public string Level { get; set; } = "미확인";
    public string Zone { get; set; } = "";
    public string Status { get; set; } = "대기";
    public long ObjectCount { get; set; }
    public string Error { get; set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class EstimateItem : INotifyPropertyChanged
{
    public bool Selected { get; set; } = true;
    public string Trade { get; set; } = "미분류";
    public string TradeCode { get; set; } = "";
    public string ItemCode { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string Specification { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double MaterialUnitPrice { get; set; }
    public double LaborUnitPrice { get; set; }
    public double ExpenseUnitPrice { get; set; }
    public string PriceSource { get; set; } = "단가 미확정";
    public string Status { get; set; } = "검토 필요";
    public string DrawingFile { get; set; } = "";
    public string SourceHandle { get; set; } = "";
    public string SourceLayer { get; set; } = "";
    public string Formula { get; set; } = "";
    public string Assumption { get; set; } = "";
    public string Level { get; set; } = "";
    public string Zone { get; set; } = "";

    public double TotalUnitPrice => MaterialUnitPrice + LaborUnitPrice + ExpenseUnitPrice;
    public double MaterialAmount => Quantity * MaterialUnitPrice;
    public double LaborAmount => Quantity * LaborUnitPrice;
    public double ExpenseAmount => Quantity * ExpenseUnitPrice;
    public double TotalAmount => Quantity * TotalUnitPrice;

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class AnalysisIssue
{
    public string Severity { get; set; } = "정보";
    public string Drawing { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class CostRecord
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Specification { get; set; } = "";
    public string Unit { get; set; } = "";
    public double Material { get; set; }
    public double Labor { get; set; }
    public double Expense { get; set; }
    public string Source { get; set; } = "";
}
