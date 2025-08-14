using System;
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

        public WizardWindow(int startStep = 1)
        {
            InitializeComponent();

            this.Closed += (s, e) => AutomationWizardState.Current.Reset();

            _uploadPage = new UploadPage();
            _uploadPage.FileLoaded += (s, e) => ShowStep(2);

            _selectItemsPage = new SelectItemsPage();
            _selectItemsPage.ProceedToReview += (s, e) => ShowStep(3);

            _reviewPage = new ReviewConfirmPage();
            _reviewPage.ProceedToProcess += (s, e) => ShowStep(4);

            _processPage = new ProcessPage();

            // 시작 스텝 보정 및 적용
            var step = Math.Max(1, Math.Min(4, startStep));
            ShowStep(step);
        }

        /// <summary>
        /// Switches to the specified step (1..4) and updates the stepper visuals.
        /// </summary>
        public void ShowStep(int stepNumber)
        {
            // 1) Load the correct content page
            switch (stepNumber)
            {
                case 1: PageHost.Content = _uploadPage; break;
                case 2: PageHost.Content = _selectItemsPage; break;
                case 3: PageHost.Content = _reviewPage; break;
                case 4: PageHost.Content = _processPage; break;
                default: PageHost.Content = null; break;
            }

            // 2) Define brushes for selected vs. unselected
            var lightFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DDD"));
            var darkFill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555"));
            var lightFore = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888"));
            var darkFore = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555"));

            // Step 1
            StepCircle1.Fill = (stepNumber == 1) ? darkFill : lightFill;
            StepLabel1.Foreground = (stepNumber == 1) ? darkFore : lightFore;

            // Step 2
            StepCircle2.Fill = (stepNumber == 2) ? darkFill : lightFill;
            StepLabel2.Foreground = (stepNumber == 2) ? darkFore : lightFore;

            // Step 3 (always enabled)
            StepCircle3.Fill = (stepNumber == 3) ? darkFill : lightFill;
            StepLabel3.Foreground = (stepNumber == 3) ? darkFore : lightFore;

            // Step 4
            StepCircle4.Fill = (stepNumber == 4) ? darkFill : lightFill;
            StepLabel4.Foreground = (stepNumber == 4) ? darkFore : lightFore;

            // 3) Enable/disable circles based on prerequisites
            bool canGoToStep2 = (AutomationWizardState.Current.AllRows != null);
            StepCircle2.IsEnabled = canGoToStep2;
            StepLabel2.Opacity = (canGoToStep2 ? 1.0 : 0.5);

            // Step 3 is always enabled now
            StepCircle3.IsEnabled = true;
            StepLabel3.Opacity = 1.0;

            bool canGoToStep4 = (AutomationWizardState.Current.SelectedRows.Count > 0);
            StepCircle4.IsEnabled = canGoToStep4;
            StepLabel4.Opacity = (canGoToStep4 ? 1.0 : 0.5);
        }

        // =========================
        // Click Handlers for Circles
        // =========================

        private void StepCircle1_Click(object sender, MouseButtonEventArgs e)
        {
            ShowStep(1);
        }

        private void StepCircle2_Click(object sender, MouseButtonEventArgs e)
        {
            if (StepCircle2.IsEnabled)
                ShowStep(2);
        }

        private void StepCircle3_Click(object sender, MouseButtonEventArgs e)
        {
            ShowStep(3);
        }

        private void StepCircle4_Click(object sender, MouseButtonEventArgs e)
        {
            if (StepCircle4.IsEnabled)
                ShowStep(4);
        }

        public void ClearProcessPage()
        {
            _processPage.ResetUi();
        }

    }
}
