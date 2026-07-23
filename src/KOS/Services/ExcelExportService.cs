using ClosedXML.Excel;
using KOS.Models;

namespace KOS.Services;

public sealed class ExcelExportService
{
    public void Export(KosProject project, string targetPath)
    {
        var template = Path.Combine(AppContext.BaseDirectory, "Templates", "standard_boq_template.xlsx");
        using var wb = File.Exists(template) ? new XLWorkbook(template) : new XLWorkbook();

        var ws = wb.Worksheets.FirstOrDefault(x => x.Name == "공종별내역서") ?? wb.AddWorksheet("공종별내역서");
        ws.Cell(1, 1).Value = project.Name;
        ws.Cell(2, 1).Value = $"현장: {project.Address} / 발주처: {project.Client}";

        var headers = new[] { "공종", "공종코드", "품목코드", "품명", "규격", "단위", "수량", "재료비단가", "노무비단가", "경비단가", "합계단가", "재료비금액", "노무비금액", "경비금액", "합계금액", "단가출처", "상태" };
        var start = 5;
        for (var c = 0; c < headers.Length; c++) ws.Cell(start, c + 1).Value = headers[c];
        ws.Range(start, 1, start, headers.Length).Style.Font.Bold = true;
        ws.Range(start, 1, start, headers.Length).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;

        var row = start + 1;
        foreach (var i in project.Estimates.Where(x => x.Selected))
        {
            var values = new object[] { i.Trade, i.TradeCode, i.ItemCode, i.ItemName, i.Specification, i.Unit, i.Quantity, i.MaterialUnitPrice, i.LaborUnitPrice, i.ExpenseUnitPrice, i.TotalUnitPrice, i.MaterialAmount, i.LaborAmount, i.ExpenseAmount, i.TotalAmount, i.PriceSource, i.Status };
            for (var c = 0; c < values.Length; c++) ws.Cell(row, c + 1).Value = XLCellValue.FromObject(values[c]);
            row++;
        }

        ws.Columns().AdjustToContents(8, 42);
        ws.SheetView.FreezeRows(start);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        wb.SaveAs(targetPath);
    }
}
