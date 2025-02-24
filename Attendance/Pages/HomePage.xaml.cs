using Attendance.Data;
using Attendance.Models;
using Attendance.Popups;
using Mopups.Services;
using Plugin.Maui.Audio;
namespace Attendance.Pages;

public partial class HomePage : ContentPage
{

    private DatabaseHelper _dbHelper;
    public HomePage(DatabaseHelper dbHelper)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
    }
    protected async override void OnAppearing()
    {
        base.OnAppearing();
        NavigationPage.SetHasNavigationBar(this, false);
        await Task.Delay(200);
        BarcodeTextBox.Focus();
        await LoadSelectedEvent();
    }
    private async Task PlayBeepSound()
    {
        try
        {
            var player = AudioManager.Current.CreatePlayer(await FileSystem.OpenAppPackageFileAsync("barcode.mp3"));
            player.Play();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error playing beep sound: {ex.Message}");
        }
    }
    public async Task LoadSelectedEvent()
    {
        var selectedEvent = await _dbHelper.GetSelectedEventAsync();

        if (selectedEvent == null || !selectedEvent.IsSelected) 
        {
            EventName.Text = "No Event Name";
            EventImage.Source = "event_image_placeholder.jpg";
            CurrentDate.Text = "No Date Set";
        }
        else
        {
            EventName.Text = selectedEvent.EventName;
            EventImage.Source = ImageConverter.ConvertByteArrayToImage(selectedEvent.ImageData);
            CurrentDate.Text = $"{selectedEvent.EventDate}\n{selectedEvent.FromTime} - {selectedEvent.ToTime}";
        }
    }
    private void BarcodeTextBox_Completed(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(BarcodeTextBox.Text) && !string.IsNullOrEmpty(BarcodeTextBox.Text))
        {
            ScanBtn_Clicked(this, EventArgs.Empty);
        }
    }
    private async void ScanBtn_Clicked(object sender, EventArgs e)
    {
        var selectedEvent = await _dbHelper.GetSelectedEventAsync();

        if(selectedEvent == null || !selectedEvent.IsSelected)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Scan Error!", "Please set an event before scanning!"));
        }
        else
        {
            string barcode = BarcodeTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(barcode))
            {
                var employee = await _dbHelper.GetEmployeeAsync(barcode);
                await PlayBeepSound();


                if (employee != null)
                {
                    string todayDate = DateTime.Now.ToString("MM/dd/yyyy");

                    var existingLog = await _dbHelper.GetAttendanceLogByDateAsync(barcode, todayDate);

                    if (existingLog != null)
                    {
                        Id_Number.Text = "ID Number: ALREADY SCANNED";
                        Full_Name.Text = $"Name: {employee.Name}";
                        Business_Unit.Text = $"Business Unit: {employee.BusinessUnit}";
                        Id_Frame.BackgroundColor = Colors.Red;

                        if (employee.IdPhoto != null && employee.IdPhoto.Length > 0)
                        {
                            Id_Photo.Source = ImageConverter.ConvertByteArrayToImage(employee.IdPhoto) ?? "blank_id.png";
                        }
                        else
                        {
                            Id_Photo.Source = "blank_id.png";
                        }


                        await Task.Delay(200);
                        BarcodeTextBox.Focus();
                        BarcodeTextBox.Text = string.Empty;
                    }
                    else
                    {
                        Id_Number.Text = $"ID Number: {employee.IdNumber}";
                        Full_Name.Text = $"Name: {employee.Name}";
                        Business_Unit.Text = $"Business Unit: {employee.BusinessUnit}";
                        Id_Frame.BackgroundColor = Colors.Green;

                        if (employee.IdPhoto != null && employee.IdPhoto.Length > 0)
                        {
                            Id_Photo.Source = ImageConverter.ConvertByteArrayToImage(employee.IdPhoto) ?? "blank_id.png";
                        }
                        else
                        {
                            Id_Photo.Source = "blank_id.png";
                        }

                        await _dbHelper.SaveLogAsync(new AttendanceLog
                        {
                            IdNumber = barcode,
                            BusinessUnit = employee.BusinessUnit,
                            Name = employee.Name,
                            Status = "SUCCESS",
                            Timestamp = DateTime.Now.ToString("MM/dd/yyyy h:mm:ss tt"),
                            EventName = selectedEvent.EventName,
                            EventCategory = selectedEvent.Category,
                            EventDate = selectedEvent.EventDate,
                            FromTime = selectedEvent.FromTime,
                            ToTime = selectedEvent.ToTime
                        });

                        await Task.Delay(200);
                        BarcodeTextBox.Focus();
                        BarcodeTextBox.Text = string.Empty;
                    }
                }
                else
                {
                    Id_Number.Text = $"ID Number: {barcode} Not Found";
                    Full_Name.Text = "Name: Not Found";
                    Business_Unit.Text = "Business Unit: Not Found";
                    Id_Photo.Source = "invalid.png";
                    Id_Frame.BackgroundColor = Colors.Red;

                    await _dbHelper.SaveLogAsync(new AttendanceLog
                    {
                        IdNumber = barcode,
                        BusinessUnit = "",
                        Name = "",
                        Status = "NOT FOUND",
                        Timestamp = DateTime.Now.ToString("MM/dd/yyyy h:mm:ss tt"),
                        EventName = selectedEvent.EventName,
                        EventCategory = selectedEvent.Category,
                        EventDate = selectedEvent.EventDate,
                        FromTime = selectedEvent.FromTime,
                        ToTime = selectedEvent.ToTime
                    });

                    await Task.Delay(200);
                    BarcodeTextBox.Focus();
                    BarcodeTextBox.Text = string.Empty;
                }
            }
        }


    }
    private async void OptionBtn_Clicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SettingPage(_dbHelper));
    }
}