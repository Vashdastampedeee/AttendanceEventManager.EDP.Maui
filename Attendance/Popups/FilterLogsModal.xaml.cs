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
        var selectedEvent = await _dbHelper.GetSelectedEventAsync();
        var logs = await _dbHelper.GetLogsAsync(); // Fetch logs
        _logs = new ObservableCollection<AttendanceLog>(logs.ToList());

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

        if (selectedEvent != null)
        {
            EventPicker.SelectedItem = selectedEvent.EventName;
            selectedEventName = selectedEvent.EventName;

            CategoryPicker.SelectedItem = selectedEvent.Category;
            selectedEventCategory = selectedEvent.Category;

            UpdateCategoryPicker();
            UpdateDatePicker();
        }

        if (!string.IsNullOrEmpty(savedEventName))
        {
            EventPicker.SelectedItem = savedEventName;
            selectedEventName = savedEventName;
        }
        else if (selectedEvent != null)
        {
            EventPicker.SelectedItem = selectedEvent.EventName;
            selectedEventName = selectedEvent.EventName;
        }

        UpdateCategoryPicker(); // Ensure category updates properly

        if (!string.IsNullOrEmpty(savedEventCategory))
        {
            CategoryPicker.SelectedItem = savedEventCategory;
            selectedEventCategory = savedEventCategory;
        }
        else if (selectedEvent != null)
        {
            CategoryPicker.SelectedItem = selectedEvent.Category;
            selectedEventCategory = selectedEvent.Category;
        }

        UpdateDatePicker(); // Ensure date updates properly

        if (!string.IsNullOrEmpty(savedEventDate))
        {
            DatePicker.SelectedItem = savedEventDate;
            selectedEventDate = savedEventDate;
        }
        else if (selectedEvent != null)
        {
            DatePicker.SelectedItem = selectedEvent.EventDate;
            selectedEventDate = selectedEvent.EventDate;
        }

        if (!string.IsNullOrEmpty(savedFromTime) && !string.IsNullOrEmpty(savedToTime))
        {
            TimePicker.SelectedItem = $"{savedFromTime} - {savedToTime}";
            selectedFromTime = savedFromTime;
            selectedToTime = savedToTime;
        }
        else if (selectedEvent != null)
        {
            TimePicker.SelectedItem = $"{selectedEvent.FromTime} - {selectedEvent.ToTime}";
            selectedFromTime = selectedEvent.FromTime;
            selectedToTime = selectedEvent.ToTime;
        }
    }

    private void UpdateDatePicker()
    {
        if (string.IsNullOrEmpty(selectedEventName) || string.IsNullOrEmpty(selectedEventCategory))
            return; // ✅ Do nothing if either filter is not selected

        var eventDates = _logs
            .Where(l => l.EventName == selectedEventName &&
                        l.EventCategory == selectedEventCategory)
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
            .Where(l => l.EventName == selectedEventName)
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

            CategoryPicker.SelectedIndex = -1;
            DatePicker.SelectedIndex = -1;
            TimePicker.SelectedIndex = -1;
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

            DatePicker.SelectedIndex = -1;
            TimePicker.SelectedIndex = -1;
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

            TimePicker.SelectedIndex = -1;
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
        // ✅ Validate each picker before accessing its value
        if (EventPicker.SelectedItem == null)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Filter Error", "Please select an event."));
            return;
        }

        if (CategoryPicker.SelectedItem == null)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Filter Error", "Please select an event category."));
            return;
        }

        // ✅ Assign values only after ensuring they are not null
        selectedEventName = EventPicker.SelectedItem?.ToString();
        selectedEventCategory = CategoryPicker.SelectedItem?.ToString();
        selectedEventDate = DatePicker.SelectedItem?.ToString();

        // ✅ Fix potential null issue with time picker
        string timeRange = TimePicker.SelectedItem?.ToString();
        if (!string.IsNullOrEmpty(timeRange) && timeRange.Contains(" - "))
        {
            var times = timeRange.Split(" - ");
            selectedFromTime = times[0].Trim();
            selectedToTime = times[1].Trim();
        }
        else
        {
            selectedFromTime = null;
            selectedToTime = null;
        }

        // ✅ Now safely apply the filter
        _onFilterApplied?.Invoke(selectedEventName, selectedEventDate, selectedEventCategory, selectedFromTime, selectedToTime);
        await MopupService.Instance.PopAsync();
    }
}