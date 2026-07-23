using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using KOS.Models;
using KOS.Services;

namespace KOS;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ProjectService _projects = new();
    private readonly CadAnalysisService _cad = new();
    private readonly CostService _costs = new();
    private readonly ExcelExportService _excel = new();
    private CancellationTokenSource? _analysisCts;
    private KosProject? _activeProject;

    public ObservableCollection<KosProject> Projects { get; } = new();
    public KosProject? ActiveProject
    {
        get => _activeProject;
        set { _activeProject = value; PropertyChanged?.Invoke(this, new(nameof(ActiveProject))); RefreshHeader(); RefreshKpis(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadProjects();
        StatusText.Text = $"단가 참조 {_costs.RecordCount:N0}건 로드";
    }

    private void LoadProjects()
    {
        Projects.Clear();
        foreach (var p in _projects.LoadAll()) Projects.Add(p);
    }

    private void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectWindow { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var p = _projects.Create(dialog.ProjectName, dialog.Address, dialog.Client, dialog.WorkType, dialog.Note);
        Projects.Insert(0, p);
        ActiveProject = p;
        ShowPage("Overview");
    }

    private void ProjectList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProjectList.SelectedItem is not KosProject p) return;
        ActiveProject = p;
        ShowPage("Overview");
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string page) ShowPage(page);
    }

    private void ShowPage(string page)
    {
        if (page != "Projects" && ActiveProject is null) { MessageBox.Show("먼저 프로젝트를 선택하세요."); return; }
        ProjectsPage.Visibility = page == "Projects" ? Visibility.Visible : Visibility.Collapsed;
        OverviewPage.Visibility = page == "Overview" ? Visibility.Visible : Visibility.Collapsed;
        DrawingsPage.Visibility = page == "Drawings" ? Visibility.Visible : Visibility.Collapsed;
        TakeoffPage.Visibility = page == "Takeoff" ? Visibility.Visible : Visibility.Collapsed;
        EstimatePage.Visibility = page == "Estimate" ? Visibility.Visible : Visibility.Collapsed;
        OutputPage.Visibility = page == "Output" ? Visibility.Visible : Visibility.Collapsed;
        HeaderTitle.Text = page == "Projects" ? "현장 프로젝트" : ActiveProject?.Name ?? "KOS";
        HeaderSub.Text = page switch
        {
            "Overview" => "프로젝트 현황과 다음 작업",
            "Drawings" => "CAD 파일 등록과 분석",
            "Takeoff" => "측정 그룹과 산출 결과",
            "Estimate" => "Excel형 공내역서와 자동 단가",
            "Output" => "공내역서 및 프로젝트 출력",
            _ => "프로젝트를 선택하거나 새로 만드세요."
        };
        RefreshKpis();
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        var d = new OpenFileDialog { Filter = "CAD 파일 (*.dwg;*.dxf)|*.dwg;*.dxf", Multiselect = true };
        if (d.ShowDialog() != true) return;
        var added = _projects.AddFiles(ActiveProject, d.FileNames);
        ActiveProject.Notify(nameof(ActiveProject.Drawings));
        DrawingGrid.Items.Refresh();
        StatusText.Text = $"CAD 파일 {added.Count:N0}개 추가";
        RefreshKpis();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "CAD 폴더 선택", ShowNewFolderButton = false };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
            .Where(x => Path.GetExtension(x).Equals(".dwg", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(x).Equals(".dxf", StringComparison.OrdinalIgnoreCase));
        var added = _projects.AddFiles(ActiveProject, files);
        DrawingGrid.Items.Refresh();
        StatusText.Text = $"폴더에서 CAD 파일 {added.Count:N0}개 추가";
        RefreshKpis();
    }

    private void RemoveDrawing_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null || DrawingGrid.SelectedItems.Count == 0) return;
        var selected = DrawingGrid.SelectedItems.Cast<DrawingItem>().ToList();
        if (MessageBox.Show($"{selected.Count}개 도면을 프로젝트에서 삭제할까요?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        foreach (var x in selected) _projects.RemoveDrawing(ActiveProject, x);
        DrawingGrid.Items.Refresh();
        RefreshKpis();
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null || ActiveProject.Drawings.Count == 0) { MessageBox.Show("CAD 파일을 먼저 추가하세요."); return; }
        _analysisCts = new();
        AnalysisProgress.Minimum = 0; AnalysisProgress.Maximum = ActiveProject.Drawings.Count; AnalysisProgress.Value = 0;
        ActiveProject.Estimates.Clear();
        ActiveProject.Issues.Clear();

        for (var i = 0; i < ActiveProject.Drawings.Count; i++)
        {
            var drawing = ActiveProject.Drawings[i];
            drawing.Status = "분석 중"; drawing.Error = ""; drawing.Notify();
            var progress = new Progress<string>(s => StatusText.Text = s);
            try
            {
                var rows = await _cad.AnalyzeAsync(drawing, progress, _analysisCts.Token);
                foreach (var row in rows) ActiveProject.Estimates.Add(row);
            }
            catch (OperationCanceledException) { drawing.Status = "취소"; break; }
            catch (Exception ex)
            {
                drawing.Status = "실패"; drawing.Error = ex.Message;
                ActiveProject.Issues.Add(new AnalysisIssue { Severity = "오류", Drawing = drawing.FileName, Message = ex.Message });
            }
            drawing.Notify(); AnalysisProgress.Value = i + 1; DrawingGrid.Items.Refresh();
        }

        GroupEstimateRows();
        _costs.Match(ActiveProject.Estimates);
        _projects.Save(ActiveProject);
        EstimateGrid.Items.Refresh(); TakeoffGrid.Items.Refresh();
        StatusText.Text = $"분석 완료 · 표시 내역 {ActiveProject.Estimates.Count:N0}행";
        RefreshKpis();
        ShowPage("Takeoff");
    }

    private void GroupEstimateRows()
    {
        if (ActiveProject is null) return;
        var grouped = ActiveProject.Estimates
            .GroupBy(x => new { x.Trade, x.TradeCode, x.ItemName, x.Specification, x.Unit, x.Level, x.Zone, x.DrawingFile })
            .Select(g =>
            {
                var first = g.First();
                return new EstimateItem
                {
                    Trade = first.Trade, TradeCode = first.TradeCode, ItemName = first.ItemName,
                    Specification = first.Specification, Unit = first.Unit, Quantity = g.Sum(x => x.Quantity),
                    Level = first.Level, Zone = first.Zone, DrawingFile = first.DrawingFile,
                    SourceLayer = string.Join(", ", g.Select(x => x.SourceLayer).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(8)),
                    SourceHandle = string.Join(", ", g.Select(x => x.SourceHandle).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(20)),
                    Formula = $"{g.Count():N0}개 측정값 합계", Status = first.Status
                };
            })
            .OrderBy(x => x.TradeCode).ThenBy(x => x.ItemName).ThenBy(x => x.Level).ToList();

        ActiveProject.Estimates.Clear();
        foreach (var x in grouped) ActiveProject.Estimates.Add(x);
    }

    private void CancelAnalysis_Click(object sender, RoutedEventArgs e) => _analysisCts?.Cancel();

    private void AutoPrice_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        _costs.Match(ActiveProject.Estimates);
        EstimateGrid.Items.Refresh();
        _projects.Save(ActiveProject);
        RefreshKpis();
        StatusText.Text = "단가 자동 매칭 완료";
    }

    private void RemoveEstimate_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        foreach (var x in EstimateGrid.SelectedItems.Cast<EstimateItem>().ToList()) ActiveProject.Estimates.Remove(x);
        _projects.Save(ActiveProject); RefreshKpis();
    }

    private void EstimateGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EstimateGrid.SelectedItem is not EstimateItem i) return;
        DetailName.Text = $"{i.ItemName} {i.Specification} / {i.Quantity:N4}{i.Unit}";
        DetailFormula.Text = i.Formula;
        DetailDrawing.Text = $"{i.DrawingFile} / {i.Level} / {i.Zone}";
        DetailSource.Text = $"{i.SourceLayer}\nHandle: {i.SourceHandle}";
        DetailAssumption.Text = string.IsNullOrWhiteSpace(i.Assumption) ? i.Status : i.Assumption;
        DetailPrice.Text = i.PriceSource;
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        var dialog = new SaveFileDialog { Filter = "Excel 통합문서 (*.xlsx)|*.xlsx", FileName = $"{ActiveProject.Name}_공내역서.xlsx", InitialDirectory = _projects.OutputFolder(ActiveProject) };
        if (dialog.ShowDialog() != true) return;
        _excel.Export(ActiveProject, dialog.FileName);
        StatusText.Text = $"Excel 저장 완료: {dialog.FileName}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ActiveProject is null) return;
        _projects.Save(ActiveProject); StatusText.Text = "프로젝트 저장 완료";
    }

    private void OpenProjectFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = ActiveProject is null ? _projects.Root : _projects.ProjectFolder(ActiveProject);
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void FilterReview_Click(object sender, RoutedEventArgs e)
    {
        var source = CollectionViewSource.GetDefaultView(EstimateGrid.ItemsSource ?? TakeoffGrid.ItemsSource);
        if (source is null) return;
        var active = source.Filter is not null;
        source.Filter = active ? null : o => o is EstimateItem x && (x.Status.Contains("검토") || x.PriceSource.Contains("미확정"));
        StatusText.Text = active ? "전체 표시" : "검토 필요 항목만 표시";
    }

    private void EstimateSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ActiveProject is null) return;
        var q = EstimateSearch.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(ActiveProject.Estimates);
        view.Filter = string.IsNullOrWhiteSpace(q) ? null : o =>
        {
            var x = (EstimateItem)o;
            return $"{x.Trade} {x.ItemCode} {x.ItemName} {x.Specification}".Contains(q, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void ProjectSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        var q = ProjectSearch.Text.Trim();
        var view = CollectionViewSource.GetDefaultView(Projects);
        view.Filter = string.IsNullOrWhiteSpace(q) ? null : o =>
        {
            var x = (KosProject)o;
            return $"{x.Name} {x.Address} {x.Client}".Contains(q, StringComparison.OrdinalIgnoreCase);
        };
    }

    private void RefreshHeader()
    {
        if (ActiveProject is null) return;
        HeaderTitle.Text = ActiveProject.Name;
        HeaderSub.Text = $"{ActiveProject.Address} · {ActiveProject.Client}";
    }

    private void RefreshKpis()
    {
        if (ActiveProject is null) return;
        KpiDrawings.Text = ActiveProject.Drawings.Count.ToString("N0");
        KpiAnalyzed.Text = ActiveProject.Drawings.Count(x => x.Status == "완료").ToString("N0");
        KpiEstimates.Text = ActiveProject.Estimates.Count.ToString("N0");
        var priced = ActiveProject.Estimates.Count(x => !x.PriceSource.Contains("미확정"));
        KpiPriceRate.Text = ActiveProject.Estimates.Count == 0 ? "0%" : $"{priced * 100d / ActiveProject.Estimates.Count:0}%";
        KpiAmount.Text = $"{ActiveProject.Estimates.Sum(x => x.TotalAmount):N0}원";
        NextActionText.Text = ActiveProject.Drawings.Count == 0 ? "CAD 파일을 추가하세요."
            : ActiveProject.Drawings.Any(x => x.Status is "대기" or "실패") ? "도면 분석을 실행하거나 실패 도면을 확인하세요."
            : ActiveProject.Estimates.Any(x => x.PriceSource.Contains("미확정")) ? "단가 미확정 항목을 검토하세요."
            : "공내역서를 확인하고 Excel로 출력하세요.";
        TakeoffSummary.Text = $"측정 그룹 {ActiveProject.Estimates.Count:N0}행 · 원시 객체 {ActiveProject.Drawings.Sum(x => x.ObjectCount):N0}개 · 검토 필요 {ActiveProject.Estimates.Count(x => x.Status.Contains("검토")):N0}행";
    }
}
