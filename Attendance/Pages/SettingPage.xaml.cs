using System.Collections.ObjectModel;
using Attendance.Data;
using Attendance.Models;
using Attendance.Popups;
using Mopups.Services;

namespace Attendance.Pages;

public partial class SettingPage : TabbedPage
{
    private DatabaseHelper _dbHelper;
    private CancellationTokenSource _cts;

    private string selectedCategory = "";

    private string lastEventName;
    private string lastEventCategory;
    private string lastEventDate;
    private string lastFromTime;
    private string lastToTime;

    private string lastSelectedCategory;
    public SettingPage(DatabaseHelper dbHelper)
    {
        InitializeComponent();
        var navigationPage = (NavigationPage)Application.Current.MainPage;
        navigationPage.BarBackgroundColor = Color.FromArgb("#003366");
        navigationPage.BarTextColor = Colors.White;
        _dbHelper = dbHelper;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        LoadAttendanceLogs();
        LoadEvents();
        LoadDashboardData();
    }
    //DATABASE TABBED
    private async void SyncBtn_Clicked(object sender, EventArgs e)
    {
        await _dbHelper.SyncEmployeesFromSQLServer();
    }

    private async void DownloadBtn_Clicked(object sender, EventArgs e)
    {
        await DownloadDatabaseFile();
    }
    private async Task DownloadDatabaseFile()
    {
        string sourcePath = Path.Combine(FileSystem.AppDataDirectory, "dbtest.db");
        string destinationPath = Path.Combine("/storage/emulated/0/Download/", "dbtest.db");

        if (File.Exists(sourcePath))
        {
            File.Copy(sourcePath, destinationPath, true);
            await MopupService.Instance.PushAsync(new DownloadModal("Download Complete", $"Database saved at:\n{destinationPath}"));
        }
        else
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Download Error!", "Database file not found!"));
        }
    }
    private async Task LoadAttendanceLogs()
    {
        var logs = await _dbHelper.GetLogsAsync();
        if (logs == null || logs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Logs", "No attendance records found."));
            return;
        }
        AttendanceLogData.ItemsSource = new ObservableCollection<AttendanceLog>(logs);
    }
    //ATTENDANCE LOGS TABBED
    private async void SearchLogsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string searchText = e.NewTextValue.Trim();

        if (_cts != null)
        {
            _cts.Cancel(); // Cancel the previous search task
        }

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        await Task.Delay(500); // Wait for 300ms before executing search

        if (token.IsCancellationRequested) return; // Stop if a new search started

        if (string.IsNullOrWhiteSpace(searchText))
        {
            LoadAttendanceLogs();
        }
        else
        {
            var filteredLogs = await _dbHelper.SearchLogsAsync(searchText);
            if (!token.IsCancellationRequested)
            {
                AttendanceLogData.ItemsSource = new ObservableCollection<AttendanceLog>(filteredLogs);
            }
        }
    }
    private void RefreshLogBtn_Clicked(object sender, EventArgs e)
    {
        LoadAttendanceLogs();
    }
    private async void ExportBtn_Clicked(object sender, EventArgs e)
    {

        var logs = await _dbHelper.GetLogsAsync();

        if (logs == null || logs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Export Error!", "Scan Employee First!"));
            return;
        }

        //await _dbHelper.ExportAttendanceLogsToExcel();
        await MopupService.Instance.PushAsync(new ExportModal(_dbHelper));
    }
    //EVENT TABBED
    public async Task LoadEvents()
    {
        var events = await _dbHelper.GetEventsAsync();

        foreach (var ev in events)
        {
            ev.ImageSource = ImageConverter.ConvertByteArrayToImage(ev.ImageData);
        }

        EventData.ItemsSource = events;
    }
    private void AddBtn_Clicked(object sender, EventArgs e)
    {
        MopupService.Instance.PushAsync(new AddEventModal(_dbHelper));
    }
    private async void RefreshEventBtn_Clicked(object sender, EventArgs e)
    {
        await LoadEvents();
    }
    private async void SearchEventTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string searchText = e.NewTextValue.Trim();

        if (_cts != null)
        {
            _cts.Cancel(); // Cancel the previous search task
        }

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;

        await Task.Delay(500); // Wait before executing search

        if (token.IsCancellationRequested) return; // Stop if a new search started

        if (string.IsNullOrWhiteSpace(searchText))
        {
            await LoadEvents(); // Reload all events if search is empty
        }
        else
        {
            var filteredEvents = await _dbHelper.SearchEventsAsync(searchText);

            foreach (var ev in filteredEvents)
            {
                ev.ImageSource = ImageConverter.ConvertByteArrayToImage(ev.ImageData);
            }

            if (!token.IsCancellationRequested)
            {
                EventData.ItemsSource = new ObservableCollection<Event>(filteredEvents);
            }
        }
    }
    private async void DeleteBtn_Clicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var selectedEvent = button?.BindingContext as Event;

        if (selectedEvent != null)
        {
            await _dbHelper.DeleteEventAsync(selectedEvent);
            await LoadEvents();
        }
    }
    private async void EditBtn_Clicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var selectedEvent = button?.BindingContext as Event;

        if (selectedEvent != null)
        {
            await MopupService.Instance.PushAsync(new EditEventModal(_dbHelper, selectedEvent.Id));
        }
    }
    private async void UseEventBtn_Clicked(object sender, EventArgs e)
    {
        var button = sender as Button;
        var selectedEvent = button?.BindingContext as Event;

        if (selectedEvent != null)
        {
            await _dbHelper.SetSelectedEventAsync(selectedEvent.Id);
            await LoadEvents();
            LoadDashboardData();
        }
    }

    private async void CategoryFilterPicker_Clicked(object sender, EventArgs e)
    {
        var filteredLogs = await _dbHelper.GetLogsAsync();

        if (filteredLogs == null || filteredLogs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("No Events Found!", "Add Events First"));
            return;
        }
        await MopupService.Instance.PushAsync(new FilterCategoryModal(
            _dbHelper,
            ApplyCategoryFilter,
            lastSelectedCategory 
        ));
    }

    private async void ApplyCategoryFilter(string category)
    {
        lastSelectedCategory = category; // ✅ Save applied category

        var filteredLogs = await _dbHelper.GetFilteredCategoryAsync(category);

        if (filteredLogs == null || filteredLogs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Filter Error!", "Select Category First!"));
            return;
        }

        EventData.ItemsSource = new ObservableCollection<Event>(filteredLogs);
    }

    private async void LogsFilterPicker_Clicked(object sender, EventArgs e)
    {
        var logs = await _dbHelper.GetLogsAsync();

        if(logs == null || logs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Filter Error!", "Scan Employee First!"));
            return;
        }

        await MopupService.Instance.PushAsync(new FilterLogsModal(
        _dbHelper, 
        ApplyLogsFilter, 
        lastEventName,
        lastEventCategory,
        lastEventDate,
        lastFromTime,
        lastToTime));
    }
    private async void ApplyLogsFilter(string eventName, string eventDate, string eventCategory, string fromTime, string toTime)
    {
        lastEventName = eventName;
        lastEventCategory = eventCategory;
        lastEventDate = eventDate;
        lastFromTime = fromTime;
        lastToTime = toTime;

        var filteredLogs = await _dbHelper.GetFilteredLogsAsync(eventName, eventDate, eventCategory, fromTime, toTime);

        if (filteredLogs == null || filteredLogs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("No Logs Found!", "No logs match the selected filters."));
            return;
        }
        AttendanceLogData.ItemsSource = new ObservableCollection<AttendanceLog>(filteredLogs);
    }

    private async void LoadDashboardData()
    {
        int scannedCount = await _dbHelper.GetScannedEmployeeCountForActiveEventAsync();
        int totalEmployees = await _dbHelper.GetTotalEmployeeCountAsync();
        var buCounts = await _dbHelper.GetTotalEmployeeCountForAllBUAsync();
        var scannedBUCounts = await _dbHelper.GetScannedEmployeeCountForAllBUAsync();

        double allPercentage = totalEmployees > 0 ? (scannedCount / (double)totalEmployees) * 100 : 0;
        double rawlingsPercentage = buCounts["RAWLINGS"] > 0 ? (scannedBUCounts["RAWLINGS"] / (double)buCounts["RAWLINGS"]) * 100 : 0;
        double jlinePercentage = buCounts["JLINE"] > 0 ? (scannedBUCounts["JLINE"] / (double)buCounts["JLINE"]) * 100 : 0;
        double hlbPercentage = buCounts["HLB"] > 0 ? (scannedBUCounts["HLB"] / (double)buCounts["HLB"]) * 100 : 0;
        double bagPercentage = buCounts["BAG"] > 0 ? (scannedBUCounts["BAG"] / (double)buCounts["BAG"]) * 100 : 0;
        double supportPercentage = buCounts["SUPPORT GROUP"] > 0 ? (scannedBUCounts["SUPPORT GROUP"] / (double)buCounts["SUPPORT GROUP"]) * 100 : 0;

        AllLabel.Text = $"ALL: {scannedCount}/{totalEmployees} ({allPercentage:F2}%)";
        RawlingsLabel.Text = $"Rawlings: {scannedBUCounts["RAWLINGS"]}/{buCounts["RAWLINGS"]} ({rawlingsPercentage:F2}%)";
        JlineLabel.Text = $"JLINE: {scannedBUCounts["JLINE"]}/{buCounts["JLINE"]} ({jlinePercentage:F2}%)";
        HlbLabel.Text = $"HLB: {scannedBUCounts["HLB"]}/{buCounts["HLB"]} ({hlbPercentage:F2}%)";
        BagLabel.Text = $"BAG: {scannedBUCounts["BAG"]}/{buCounts["BAG"]} ({bagPercentage:F2}%)";
        SupportLabel.Text = $"SUPPORT: {scannedBUCounts["SUPPORT GROUP"]}/{buCounts["SUPPORT GROUP"]} ({supportPercentage:F2}%)";
    }

    private async void View_Clicked(object sender, EventArgs e)
    {
        var selectedEvent = await _dbHelper.GetSelectedEventAsync();
        if (selectedEvent == null)
        {
            await DisplayAlert("Error", "No active event selected!", "OK");
            return;
        }

        await Navigation.PushAsync(new AttendanceDetailsPage(_dbHelper, selectedEvent.EventName));
    }
}