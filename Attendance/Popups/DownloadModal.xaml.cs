using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class DownloadModal : PopupPage
{
	public DownloadModal(string header, string message)
	{
		InitializeComponent();
		HeaderLabel.Text = header;
		MessageLabel.Text = message;	
	}

    private async void OkBtn_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
    }
}