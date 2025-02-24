using Attendance.Data;
using Attendance.Models;
using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class AddEventModal : PopupPage
{
    private DatabaseHelper _dbHelper;
    private byte[] _imageData;
    public AddEventModal(DatabaseHelper databaseHelper)
	{
		InitializeComponent();
        _dbHelper = databaseHelper;
        EventDatePicker.MinimumDate = DateTime.Today;
    }

    private async void OkBtn_Clicked(object sender, EventArgs e)
    {
        await MopupService.Instance.PopAsync();
    }

    private async void SubmitBtn_Clicked(object sender, EventArgs e)
    {
    
        if (string.IsNullOrWhiteSpace(EventNameEntry.Text))
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Error", "Event Name is required."));
            return;
        }
        else if (CategoryPicker.SelectedItem == null)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Error", "Event Category is required."));
            return;
        }
        else if (FromTimePicker.Time == TimeSpan.Zero || ToTimePicker.Time == TimeSpan.Zero)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Error", "Time is required."));
            return;
        }

        string selectedCategory = CategoryPicker.SelectedItem?.ToString() ?? string.Empty;
        string formattedEventDate = EventDatePicker.Date.ToString("MM/dd/yyyy");
        string formattedFromTime = DateTime.Today.Add(FromTimePicker.Time).ToString("hh:mm tt");
        string formattedToTime = DateTime.Today.Add(ToTimePicker.Time).ToString("hh:mm tt");

        var newEvent = new Event
        {
            EventName = EventNameEntry.Text.Trim(),
            ImageData = _imageData,
            IsSelected = false,
            Category = selectedCategory,
            EventDate = formattedEventDate, 
            FromTime = formattedFromTime,   
            ToTime = formattedToTime        
        };

        await _dbHelper.AddEventAsync(newEvent);
        await MopupService.Instance.PopAsync();
    }

    private async void UploadPictureBtn_Clicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select an image",
                FileTypes = FilePickerFileType.Images
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                _imageData = memoryStream.ToArray();

                EventImagePreview.Source = ImageSource.FromStream(() => new MemoryStream(_imageData));
                EventImagePreview.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to pick an image: {ex.Message}", "OK");
        }
    }
    private void FromTimePicker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Time")
        {
            if (FromTimePicker.Time == TimeSpan.Zero)
            {
                ToTimePicker.IsEnabled = false;
            }
            else
            {
                ToTimePicker.IsEnabled = true;

                if (FromTimePicker.Time >= ToTimePicker.Time)
                {
                    ToTimePicker.Time = FromTimePicker.Time.Add(TimeSpan.FromHours(1)); 
                }
            }
        }
    }

    private void ToTimePicker_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Time")
        {
            if (ToTimePicker.Time <= FromTimePicker.Time)
            {
               
                ToTimePicker.Time = FromTimePicker.Time.Add(TimeSpan.FromHours(1));
            }
        }
    }
}