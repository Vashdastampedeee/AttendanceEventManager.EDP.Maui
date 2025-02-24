using Attendance.Data;
using Attendance.Models;
using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class EditEventModal : PopupPage
{
	private DatabaseHelper _dbHelper;
	private int _eventId;
	private byte[] _imageData;
	public EditEventModal(DatabaseHelper databaseHelper, int eventId)
	{
		InitializeComponent();
		_dbHelper = databaseHelper;
		_eventId = eventId;
        EventDatePicker.MinimumDate = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEventData();
    }

    private async Task LoadEventData()
    {
        var eventToEdit = await _dbHelper.GetEventByIdAsync(_eventId);
        if (eventToEdit != null)
        {
            _imageData = eventToEdit.ImageData;
            EventImagePreview.Source = ImageConverter.ConvertByteArrayToImage(_imageData);
            EventNameEntry.Text = eventToEdit.EventName;
            if (!string.IsNullOrEmpty(eventToEdit.Category) && CategoryPicker.Items.Contains(eventToEdit.Category))
            {
                CategoryPicker.SelectedItem = eventToEdit.Category;
            }
            if (DateTime.TryParse(eventToEdit.EventDate, out DateTime eventDate))
            {
                EventDatePicker.Date = eventDate;
            }
            if (DateTime.TryParseExact(eventToEdit.FromTime, "hh:mm tt", null, System.Globalization.DateTimeStyles.None, out DateTime fromDateTime))
            {
                FromTimePicker.Time = fromDateTime.TimeOfDay;
            }
            if (DateTime.TryParseExact(eventToEdit.ToTime, "hh:mm tt", null, System.Globalization.DateTimeStyles.None, out DateTime toDateTime))
            {
                ToTimePicker.Time = toDateTime.TimeOfDay;
            }
        }
        else
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Error", "Event not found!"));
            await MopupService.Instance.PopAsync();
        }
    }

    private async void UploadPictureBtn_Clicked(object sender, EventArgs e)
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

            EventImagePreview.Source = ImageConverter.ConvertByteArrayToImage(_imageData);
        }
    }
    private async void UpdateBtn_Clicked(object sender, EventArgs e)
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

        var updatedEvent = new Event
        {
            Id = _eventId,
            EventName = EventNameEntry.Text.Trim(),
            ImageData = _imageData,
            Category = selectedCategory,
            EventDate = formattedEventDate,
            FromTime = formattedFromTime,
            ToTime = formattedToTime
        };

        await _dbHelper.UpdateEventAsync(updatedEvent);
        await MopupService.Instance.PopAsync();
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