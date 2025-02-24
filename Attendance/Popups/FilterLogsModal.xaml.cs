using System.Collections.ObjectModel;
using Attendance.Data;
using Attendance.Models;
using Mopups.Pages;
using Mopups.Services;

namespace Attendance.Popups;

public partial class FilterLogsModal : PopupPage
{
    private readonly Action<string, string, string, string, string> _onFilterApplied;
    private readonly DatabaseHelper _dbHelper;
    private ObservableCollection<AttendanceLog> _logs;
    private string selectedEventName;
    private string selectedEventCategory;
    private string selectedEventDate;
    private string selectedFromTime;
    private string selectedToTime;

    private string savedEventName;
    private string savedEventCategory;
    private string savedEventDate;
    private string savedFromTime;
    private string savedToTime;

    public FilterLogsModal(DatabaseHelper dbHelper, Action<string, string, string, string, string> onFilterApplied,
        string prevEventName, string prevEventCategory, string prevEventDate, string prevFromTime, string prevToTime)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
        _onFilterApplied = onFilterApplied;

        savedEventName = prevEventName;
        savedEventCategory = prevEventCategory;
        savedEventDate = prevEventDate;
        savedFromTime = prevFromTime;
        savedToTime = prevToTime;

        LoadEvents();
    }

    private async void LoadEvents()
    {
        var logs = await _dbHelper.GetLogsAsync(); // Fetch logs
        _logs = new ObservableCollection<AttendanceLog>(logs
            .Where(l => l.Status == "SUCCESS") // ✅ Exclude NOT FOUND logs
            .ToList());

        var distinctEvents = _logs
            .Select(l => l.EventName)
            .Distinct()
            .ToList();

        var distinctCategories = _logs
            .Select(l => l.EventCategory)
            .Distinct()
            .ToList();

        var distinctDates = _logs
            .Select(l => l.EventDate)
            .Distinct()
            .ToList(); // ✅ Added this to populate DatePicker

        EventPicker.ItemsSource = distinctEvents;
        CategoryPicker.ItemsSource = distinctCategories;
        DatePicker.ItemsSource = distinctDates; // ✅ Now DatePicker will have values!

        if (!string.IsNullOrEmpty(savedEventName))
        {
            EventPicker.SelectedItem = savedEventName;
            UpdateCategoryPicker();
        }

        if (!string.IsNullOrEmpty(savedEventCategory))
        {
            CategoryPicker.SelectedItem = savedEventCategory;
            UpdateDatePicker();
        }

        if (!string.IsNullOrEmpty(savedEventDate))
        {
            DatePicker.SelectedItem = savedEventDate;
        }

        if (!string.IsNullOrEmpty(savedFromTime) && !string.IsNullOrEmpty(savedToTime))
        {
            TimePicker.SelectedItem = $"{savedFromTime} - {savedToTime}";
        }
    }

    private void UpdateDatePicker()
    {
        if (string.IsNullOrEmpty(selectedEventName) || string.IsNullOrEmpty(selectedEventCategory))
            return; // ✅ Do nothing if either filter is not selected

        var eventDates = _logs
            .Where(l => l.EventName == selectedEventName && 
                        l.EventCategory == selectedEventCategory && 
                        l.Status == "SUCCESS")
            .Select(l => l.EventDate)
            .Distinct()
            .ToList();

        DatePicker.ItemsSource = eventDates;
        DatePicker.IsEnabled = eventDates.Count > 0;
    }

    private void UpdateCategoryPicker()
    {
        if (string.IsNullOrEmpty(selectedEventName))
            return; // ✅ Do nothing if no event is selected

        var availableCategories = _logs
            .Where(l => l.EventName == selectedEventName && l.Status == "SUCCESS")
            .Select(l => l.EventCategory)
            .Distinct()
            .ToList();

        CategoryPicker.ItemsSource = availableCategories;
        CategoryPicker.IsEnabled = availableCategories.Count > 0;
    }

    private void EventPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (EventPicker.SelectedIndex >= 0)
        {
            selectedEventName = EventPicker.SelectedItem.ToString();
            UpdateCategoryPicker();
            CategoryPicker.IsEnabled = true; // ✅ Enable CategoryPicker when event is selected
        }
        else
        {
            selectedEventName = "";
            CategoryPicker.ItemsSource = null;
            CategoryPicker.IsEnabled = false; // ✅ Disable CategoryPicker when no event is selected
        }
    }

    private void CategoryPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (CategoryPicker.SelectedIndex >= 0)
        {
            selectedEventCategory = CategoryPicker.SelectedItem.ToString();
            UpdateDatePicker();
        }
    }

    private void DatePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (DatePicker.SelectedIndex >= 0)
        {
            selectedEventDate = DatePicker.SelectedItem.ToString();

            var eventTimes = _logs
                .Where(l => l.EventName == selectedEventName &&
                            l.EventCategory == selectedEventCategory &&
                            l.EventDate == selectedEventDate)
                .Select(l => $"{l.FromTime} - {l.ToTime}")
                .Distinct()
                .ToList();

            TimePicker.ItemsSource = eventTimes;
            TimePicker.IsEnabled = eventTimes.Count > 0;
        }
    }

    private void TimePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (TimePicker.SelectedIndex >= 0)
        {
            var timeRange = TimePicker.SelectedItem.ToString();
            var times = timeRange.Split(" - ");
            selectedFromTime = times[0].Trim();
            selectedToTime = times[1].Trim();
        }
    }


    private async void ApplyFilter_Clicked(object sender, EventArgs e)
    {
        _onFilterApplied?.Invoke(selectedEventName, selectedEventDate, selectedEventCategory, selectedFromTime, selectedToTime);
        await MopupService.Instance.PopAsync();
    }
}