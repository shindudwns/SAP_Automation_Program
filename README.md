# SAP_Automation

> A WPF‐based desktop application to automate SAP Business One workflows and generate import sheets from Excel.

[![.NET Framework](https://img.shields.io/badge/.NET-4.7.2+-blue.svg)](https://dotnet.microsoft.com/)  
[![EPPlus](https://img.shields.io/badge/EPPlus-6.x-green.svg)](https://github.com/EPPlusSoftware/EPPlus)  
[![SAP B1 DI-API](https://img.shields.io/badge/SAP%20B1-DI--API-orange.svg)](https://help.sap.com/viewer/p/SAP_BUSINESS_ONE_DI_API)

---

## Table of Contents

1. [Features](#features)  
2. [Prerequisites](#prerequisites)  
3. [Installation & Setup](#installation--setup)  
4. [Configuration](#configuration)  
5. [Usage](#usage)  
6. [Architecture](#architecture)  
7. [Data Flow](#data-flow)  
8. [Folder Structure](#folder-structure)  
9. [Next Steps](#next-steps)  

---

## Features

- **Excel Upload & Preview**  
  – Load one or more `.xlsx` files into a grid  
  – Highlight and select rows/cells  
- **Process Window**  
  – Create Item Master Data (IMD) entries in SAP B1  
  – Create Sales Quotations in SAP B1  
- **Import TXT Generator**  
  – Upload INFO_EXCEL and/or INSIDE_EXCEL  
  – Filter rows by `READY` identifier  
  – Generate three import sheets and export as `.xlsx`  
- **Status Tracking**  
  – Visual row‐highlighting and status columns (Pending, Working, Done, Error)  
- **Modular Services**  
  – `ExcelService`, `Transformer`, `AutomationService`, `ImportProcessorService`, `DocumentGenerator`, `ExportService`

---

## Prerequisites

- **Windows 10/11** or Server  
- **.NET Framework 4.7.2** (WPF Desktop)  
- **Visual Studio 2019+** (with WPF workload)  
- **SAP Business One DI-API SDK** installed  
- **EPPlus** (via NuGet) for Excel I/O  

---

## Installation & Setup

1. **Clone the repo**  
   ```bash
   git clone https://github.com/your-org/SimplifyQuoter.git
   cd SimplifyQuoter
   ```
2. **Restore NuGet packages**  
   - In Visual Studio: **Tools → NuGet Package Manager → Restore**  
3. **Ensure SAP B1 DI-API SDK** is installed and registered on your dev machine.  
4. **Build & run** via Visual Studio (`F5`) or `dotnet build` / `dotnet run`.

---

## Configuration

Edit **App.config** to supply your SAP B1 connection details:

```xml
<appSettings>
  <add key="SapServer"       value="YOUR_SQL_SERVER\INSTANCE" />
  <add key="SapCompanyDb"    value="SBODEMOAL" />
  <add key="SapUser"         value="manager" />
  <add key="SapPassword"     value="Password1" />
  <add key="SapDbServerType" value="dst_MSSQL2019" />
</appSettings>
```

---

## Usage

1. **Launch** the application.  
2. **Upload Excel**  
   - Click **Upload Excel**, select your SMK_EXCEL.  
   - Rows appear in the grid.  
3. **Select Rows**  
   - Click any cell to highlight its row.  
   - Multi-select via Ctrl/Shift.  
4. **Process IMD/SQ**  
   - Click **Process**, then in the **ProcessWindow** click **Process Item Master Data** or **Process Sales Quotation**.  
5. **Generate Import Sheets**  
   - Click **Create Import TXT**  
   - In the **ImportWindow**, upload INFO_EXCEL / INSIDE_EXCEL  
   - Click **Process**, then **Download Sheet A/B/C**  

---

## Architecture


- **UI Layer** (`Views/`)  
  – `MainWindow` for Excel & selection  
  – `ProcessWindow` for SAP operations  
  – `ImportWindow` for import-sheet generation  
- **Services Layer** (`Services/`)  
  – `ExcelService`: file I/O  
  – `Transformer`: field conversions  
  – `AutomationService`: SAP DI-API calls (Connect, CreateItemMasterData, CreateSalesQuotation)  
  – `ImportProcessorService`: filter “READY” rows  
  – `DocumentGenerator`: map rows → DataTables  
  – `ExportService`: write .xlsx exports  
- **Models** (`Models/RowView.cs`)  
  – Holds raw cell data, row index, selection flag  

---

## Data Flow

```text
[User clicks Upload Excel]
    ↓
ExcelService.LoadSheetViaDialog()
    ↓
MainWindow displays rows in DataGrid
    ↓
User selects rows → RowView.IsSelected = true
    ↓
[User clicks Process]
    ↓
ProcessWindow opens with selected rows
    ↓
[Process Item Master Data]
    └─ Transformer.TransformIMD()
    └─ AutomationService.CreateItemMasterData(dto)
    └─ Update status column
[Process Sales Quotation]
    └─ Transformer.TransformSQ()
    └─ AutomationService.CreateSalesQuotation(dto)
    └─ Update status column

[User clicks Create Import TXT]
    ↓
ImportWindow opens
    ↓
User uploads INFO_EXCEL / INSIDE_EXCEL
    ↓
ImportProcessorService.FilterReady(rows, idCol)
    ↓
DocumentGenerator.BuildImportSheets(filteredRows)
    ↓
ImportWindow shows Download A/B/C buttons
    ↓
User clicks Download → ExportService.Export(DataTable, defaultName)
```

---

## Folder Structure

```
SimplifyQuoter/
├─ App.config
├─ App.xaml & App.xaml.cs
│
├─ Models/
│   └─ RowView.cs
│
├─ Services/
│   ├─ ExcelService.cs
│   ├─ Transformer.cs
│   ├─ ImportProcessorService.cs
│   ├─ DocumentGenerator.cs
│   ├─ ExportService.cs
│   └─ AutomationService.cs
│
├─ Views/
│   ├─ MainWindow.xaml & .cs
│   ├─ ProcessWindow.xaml & .cs
│   └─ ImportWindow.xaml & .cs
│
├─ ViewModels/    (optional)
│   └─ ImportWindowViewModel.cs
│
└─ Utilities/
    ├─ ViewModelBase.cs
    └─ RelayCommand.cs
```

---

## Next Steps

- Implement **AutomationService** methods using SAPbobsCOM DI-API.  
- Extend **RowView** with `IMDStatus` & `SQStatus` enums + color-coded columns.  
- Hook up **ImportProcessorService** & **DocumentGenerator** to real Excel templates.  
- Add **unit tests** for `Transformer` and `ImportProcessorService`.  

---

> **License**: MIT © SimplifyQuoter Team  
> **Support**: [Issue Tracker](https://github.com/your-o
