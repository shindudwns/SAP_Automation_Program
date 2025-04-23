using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using OfficeOpenXml;               // EPPlus core namespace
using SimplifyQuoter.Models;       // for RowView

namespace SimplifyQuoter.Services
{
    /// <summary>
    /// Opens a file‐picker, sets the EPPlus license, 
    /// and reads the first worksheet into a collection of RowView.
    /// </summary>
    public class ExcelService
    {
        public ObservableCollection<RowView> LoadSheetViaDialog()
        {
            // 1) Let the user pick the Excel file
            var dlg = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "Select SMK_EXCEL file"
            };
            if (dlg.ShowDialog() != true)
                return null;

            // 2) EPPlus 8+ licensing: noncommercial personal use
            ExcelPackage.License.SetNonCommercialPersonal("Your Name");

            // 3) Read the workbook
            var rows = new ObservableCollection<RowView>();
            using (var pkg = new ExcelPackage(new FileInfo(dlg.FileName)))
            {
                var ws = pkg.Workbook.Worksheets[0];
                int cols = ws.Dimension.End.Column;
                int lastRow = ws.Dimension.End.Row;

                // 4) Skip header row (assumed row 1), start at row 2
                for (int r = 2; r <= lastRow; r++)
                {
                    var rv = new RowView
                    {
                        RowIndex = r,
                        Cells = new string[cols]
                    };

                    for (int c = 1; c <= cols; c++)
                        rv.Cells[c - 1] = ws.Cells[r, c].Text;

                    rows.Add(rv);
                }
            }

            return rows;
        }
    }
}
