// File: Models/AutomationWizardState.cs
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Services.ServiceLayer.Dtos;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SimplifyQuoter.Models
{
    /// <summary>
    /// Holds shared state for the 4-step wizard.
    /// </summary>
    public class AutomationWizardState
    {
        private static AutomationWizardState _instance;

        public static AutomationWizardState Current
        {
            get
            {
                if (_instance == null)
                    _instance = new AutomationWizardState();
                return _instance;
            }
        }

        private AutomationWizardState() { }

        /// <summary>All rows read from the uploaded Excel file (Step 1).</summary>
        public ObservableCollection<RowView> AllRows { get; set; }

        /// <summary>Rows the user selected in Step 2.</summary>
        public List<RowView> SelectedRows { get; set; } = new List<RowView>();

        /// <summary>Already-logged-in ServiceLayerClient (holds cookies/session).</summary>
        public ServiceLayerClient SlClient { get; set; }

        /// <summary>Logged-in user name (for display and job logging).</summary>
        public string UserName { get; set; }

        /// <summary>GUID for this SAP automation run (optional).</summary>
        public Guid SapFileId { get; set; }

        /// <summary>Which file path (or file name) the user uploaded in Step 1.</summary>
        public string UploadedFilePath { get; set; }

        // Store the user’s Margin % (as a plain double, e.g. 20.0 → 20%).
        public double MarginPercent { get; set; } = 20.0;

        // Store the user’s chosen UoM (e.g. “EACH” or “PK” or custom).
        public string UoM { get; set; } = "EACH";

        /// <summary>
        /// After “Replace Excel” is clicked (Step 3), ReviewConfirmPage populates this
        /// with exactly the ItemDto list built from the edited spreadsheet.
        /// ProcessPage (Step 4) will read from here when doing the final SAP insert.
        /// </summary>
        public List<ItemDto> MergedItemMasterDtos { get; set; }

        /// <summary>
        /// After “Replace Excel” is clicked (Step 3), ReviewConfirmPage populates this
        /// with exactly the QuotationDto list built from the edited spreadsheet.
        /// ProcessPage will read from here when doing the final SAP insert.
        /// </summary>
        public List<QuotationDto> MergedQuotationDtos { get; set; }

        public void Reset()
        {
            AllRows = null;
            SelectedRows?.Clear();                
            SapFileId = Guid.Empty;
            UploadedFilePath = null;
            MergedItemMasterDtos = null;
            MergedQuotationDtos = null;
            MarginPercent = 20.0;
            UoM = "EACH";
        }

    }
}
