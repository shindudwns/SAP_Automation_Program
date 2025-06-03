// File: Models/AutomationWizardState.cs
using SimplifyQuoter.Services.ServiceLayer;
using SimplifyQuoter.Models;
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

        /// <summary>All rows read from the uploaded Excel file.</summary>
        public ObservableCollection<RowView> AllRows { get; set; }

        /// <summary>Rows the user selected in Step 2.</summary>
        public List<RowView> SelectedRows { get; set; } = new List<RowView>();

        /// <summary>Already-logged-in ServiceLayerClient (holds cookies/session).</summary>
        public ServiceLayerClient SlClient { get; set; }

        /// <summary>Logged-in user name (for display in Step 4).</summary>
        public string UserName { get; set; }

        /// <summary>GUID for this SAP automation run (optional).</summary>
        public Guid SapFileId { get; set; }
    }
}
