using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Services
{
    public class ImportTxtService
    {
        private readonly DocumentGenerator _docGen;
        private readonly AutomationService _auto;
        private readonly string _tempDir;

        public ImportTxtService(DocumentGenerator docGen,
                                AutomationService auto)
        {
            _docGen = docGen;
            _auto = auto;
            _tempDir = Path.Combine(Path.GetTempPath(), "SimplifyQuoter_Import");
            Directory.CreateDirectory(_tempDir);
        }

        public IList<DataTable> GenerateImportSheets(
            IEnumerable<RowView> infoRows,
            IEnumerable<RowView> insideRows)
        {
            return _docGen.GenerateImportSheets(infoRows, insideRows);
        }

        public IList<string> ExportToTxt(IList<DataTable> sheets)
        {
            var paths = new List<string>();
            foreach (var dt in sheets)
            {
                var sb = new StringBuilder();
                foreach (DataRow row in dt.Rows)
                {
                    var vals = new List<string>();
                    foreach (DataColumn col in dt.Columns)
                        vals.Add(row[col]?.ToString() ?? string.Empty);

                    // use string overload and IEnumerable<string>
                    sb.AppendLine(string.Join("\t", vals));
                }

                var file = Path.Combine(_tempDir, $"{dt.TableName}.txt");
                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                paths.Add(file);
            }
            return paths;
        }

        public void ImportIntoSap(IEnumerable<string> txtFiles)
        {
            _auto.Connect();
            foreach (var f in txtFiles)
                _auto.ImportDataTransfer(f);
            _auto.Disconnect();
        }
    }
}
