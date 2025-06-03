using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SimplifyQuoter.Models;

namespace SimplifyQuoter.Views
{
    public partial class WizardWindow : Window
    {
        private readonly UploadPage _uploadPage;
        private readonly SelectItemsPage _selectItemsPage;
        private readonly ReviewConfirmPage _reviewPage;
        private readonly ProcessPage _processPage;

        public WizardWindow()
        {
            InitializeComponent();

            // Instantiate each page and wire up navigation events
            _uploadPage = new UploadPage();
            _uploadPage.FileLoaded += (s, e) => ShowStep(2);

            _selectItemsPage = new SelectItemsPage();
            _selectItemsPage.ProceedToReview += (s, e) => ShowStep(3);

            _reviewPage = new ReviewConfirmPage();
            _reviewPage.ProceedToProcess += (s, e) => ShowStep(4);

            _processPage = new ProcessPage();

            // Start at step #1
            ShowStep(1);
        }

        /// <summary>
        /// Switches to the specified step (1..4). 
        /// Updates PageHost content and visual state of circles & labels.
        /// </summary>
        public void ShowStep(int stepNumber)
        {
            // 1) Load the correct page
            switch (stepNumber)
            {
                case 1:
                    PageHost.Content = _uploadPage;
                    break;
                case 2:
                    PageHost.Content = _selectItemsPage;
                    break;
                case 3:
                    PageHost.Content = _reviewPage;
                    break;
                case 4:
                    PageHost.Content = _processPage;
                    break;
                default:
                    PageHost.Content = null;
                    break;
            }

            // 2) Update the stepper visuals:
            Brush lightFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD"));
            Brush darkFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555"));
            Brush lightFore = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888"));
            Brush darkFore = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555"));

            // Step 1
            StepCircle1.Fill = (stepNumber == 1) ? darkFill : lightFill;
            StepLabel1.Foreground = (stepNumber == 1) ? darkFore : lightFore;

            // Step 2
            StepCircle2.Fill = (stepNumber == 2) ? darkFill : lightFill;
            StepLabel2.Foreground = (stepNumber == 2) ? darkFore : lightFore;

            // Step 3
            StepCircle3.Fill = (stepNumber == 3) ? darkFill : lightFill;
            StepLabel3.Foreground = (stepNumber == 3) ? darkFore : lightFore;

            // Step 4
            StepCircle4.Fill = (stepNumber == 4) ? darkFill : lightFill;
            StepLabel4.Foreground = (stepNumber == 4) ? darkFore : lightFore;

            // 3) Enable/disable circles based on prerequisites:
            bool canGoToStep2 = (AutomationWizardState.Current.AllRows != null);
            StepCircle2.IsEnabled = canGoToStep2;
            StepLabel2.Opacity = (canGoToStep2 ? 1.0 : 0.5);

            bool canGoToStep3 = (AutomationWizardState.Current.SelectedRows.Count > 0);
            StepCircle3.IsEnabled = canGoToStep3;
            StepLabel3.Opacity = (canGoToStep3 ? 1.0 : 0.5);

            bool canGoToStep4 = canGoToStep3;
            StepCircle4.IsEnabled = canGoToStep4;
            StepLabel4.Opacity = (canGoToStep4 ? 1.0 : 0.5);
        }

        // ==========================
        // Click Handlers for Circles
        // ==========================
        private void StepCircle1_Click(object sender, MouseButtonEventArgs e)
        {
            ShowStep(1);
        }

        private void StepCircle2_Click(object sender, MouseButtonEventArgs e)
        {
            // Only navigate if allowed (IsEnabled is true)
            if (StepCircle2.IsEnabled)
                ShowStep(2);
        }

        private void StepCircle3_Click(object sender, MouseButtonEventArgs e)
        {
            // Only attempt to proceed if “Selected Items” has at least one row.
            if (!StepCircle3.IsEnabled)
                return;

            // Ask SelectItemsPage to validate & transfer its inputs into the shared state.
            // If TryProceedToReview() returns true, we know it was valid → move to step 3.
            bool ok = _selectItemsPage.TryProceedToReview();
            if (ok)
            {
                ShowStep(3);
            }
            // If it wasn’t ok, TryProceedToReview already showed a MessageBox, so do nothing more.
        }

        private void StepCircle4_Click(object sender, MouseButtonEventArgs e)
        {
            if (StepCircle4.IsEnabled)
                ShowStep(4);
        }
    }
}
