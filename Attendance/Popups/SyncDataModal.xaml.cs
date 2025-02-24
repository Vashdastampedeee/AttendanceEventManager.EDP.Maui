using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class SyncDataModal : PopupPage
{
    private bool _isCanceled = false;
    public SyncDataModal()
	{
		InitializeComponent();
	}

    public async Task UpdateProgress(string message)
    {
        if (!_isCanceled) // Only update if not canceled
        {
            ProgressLabel.Text = message;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        _isCanceled = true;
        await MopupService.Instance.PopAsync(); // Close popup on cancel
    }

}