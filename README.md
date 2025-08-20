# SimplifyQuoter

> A WPF desktop application to automate SAP Business One workflows and generate import sheets from Excel—enriched with AI-driven part descriptions and grouping.

[![.NET Framework](https://img.shields.io/badge/.NET-4.7.2+-blue.svg)](https://dotnet.microsoft.com/)  
[![EPPlus](https://img.shields.io/badge/EPPlus-8.x-green.svg)](https://github.com/EPPlusSoftware/EPPlus)  
[![OpenAI](https://img.shields.io/badge/OpenAI-ChatGPT-red.svg)](https://platform.openai.com/)  
[![Npgsql](https://img.shields.io/badge/Npgsql-6.x-lightgrey.svg)](https://www.npgsql.org/)

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

- **Excel Upload & Persistence**  
  - Load one or more `.xlsx` files (`INFO_EXCEL`, `INSIDE_EXCEL`) via EPPlus  
  - Persist rows to PostgreSQL (`import_file` / `import_row` tables)
- **AI Enrichment**  
  - On-demand OpenAI calls (GPT-3.5 / GPT-4) to classify parts and generate ≤10-word descriptions  
  - Local caching in `part` table to avoid repeat API hits  
  - Rate-limit throttling and exponential back-off
- **Import TXT Generation**  
  - `DocumentGenerator` builds three sheets (A, B, C) as DataTables:  
    1. **Sheet A**: Item master: code, brand, AI-enriched group & description  
    2. **Sheet B**: Purchasing price list (PL = 11)  
    3. **Sheet C**: Sales price list (PL = 12, price ÷ 0.8)  
  - `ImportTxtService` dumps each sheet to UTF-8 `.txt` and updates processing status
- **Progress & Logging UI**  
  - Live per-row log entries in a `ListBox`  
  - Custom segmented progress bar driven by `ValueToWidthConverter`
- **Modular, Testable Services**  
  - `ExcelService`, `Transformer`, `AiEnrichmentService`, `DocumentGenerator`, `ImportTxtService`

---

## Prerequisites

- Windows 10/11 or Windows Server  
- [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472)  
- Visual Studio 2019+ with **WPF Desktop** workload  
- **SAP Business One DI-API SDK** (for future SAP integration)  
- **PostgreSQL** (for part-cache & import tracking)  
- **EPPlus** & **Npgsql** via NuGet  
- **OpenAI API key** with Chat Completions access

---

## Installation & Setup

```bash
git clone https://github.com/your-org/SimplifyQuoter.git
cd SimplifyQuoter
# Open in Visual Studio and restore NuGet packages
```  

---

## Configuration

Edit `App.config`:

```xml
<configuration>
  <appSettings>
    <!-- PostgreSQL -->
    <add key="ConnectionStrings:DefaultConnection" value="Host=…;Port=…;Username=…;Password=…;Database=…;" />
    <!-- OpenAI -->
    <add key="OpenAI:ApiKey"   value="sk-…" />
    <add key="OpenAI:Model"    value="gpt-5.0" />
  </appSettings>
</configuration>
```

---

## Usage

1. Start the application.  
2. Upload files:  
   - **Inside**: click &ldquo;Inside&rdquo; and select `INSIDE_EXCEL.xlsx`  
   - **Info**: click &ldquo;Info&rdquo; and select `INFO_EXCEL.xlsx`  
3. Click **Process** in the top-right tile.  
4. Live log appears; progress bar advances per row.  
5. When complete, Download links (Item, Purchasing Price, Sales Price) activate.

---

## Architecture

### Views
```text
Views/
├─ MainWindow.xaml        # initial Excel upload & row selection
├─ ProcessWindow.xaml     # (future) SAP DI-API automation
└─ ImportWindow.xaml      # import-sheet generation UI with logs & progress
```

### Services
```text
Services/
├─ ExcelService.cs        # reads .xlsx → RowView[]
├─ Transformer.cs         # value conversions & AI enrichment orchestration
├─ AiEnrichmentService.cs # wraps OpenAI calls + PostgreSQL caching
├─ DocumentGenerator.cs   # maps RowView → DataTable sheets A/B/C
└─ ImportTxtService.cs    # writes .txt exports + updates DB + reports progress
```

### Models
```text
Models/
└─ RowView.cs             # carries row‐index, string[] Cells, selection
```

### Converters
```text
Converters/
└─ ValueToWidthConverter.cs  # maps [0–100] → pixel width for progress fill
```

---

## Data Flow

```mermaid
flowchart LR
    A[User clicks Inside/Info] --> B[ExcelService to import_row]
    B --> C[Rows displayed in ImportWindow]
    C --> D[User clicks Process]
    D --> E[Process import]
    E --> F[Generate import sheets]
    F --> G[Write sheets A–C to .txt]
    E --> H[Update DB & UI progress]
    G --> I[Enable download links]

```

---

## Next Steps

- **SAP B1 DI-API**: implement `AutomationService` routines for IMD & Sales Quote  
- **Unit Tests**: cover `Transformer`, `DocumentGenerator`, `ImportTxtService`  
- **Error Handling**: surface Excel-parsing & AI exceptions in UI  
- **MVVM Refactor**: decouple Views via ViewModels & `ICommand`

