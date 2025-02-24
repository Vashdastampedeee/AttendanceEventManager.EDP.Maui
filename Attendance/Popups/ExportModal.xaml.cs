using ClosedXML.Excel;
using System.Collections.ObjectModel;
using Mopups.Pages;
using Mopups.Services;
using Attendance.Data;
using Attendance.Models;

namespace Attendance.Popups;

public partial class ExportModal : PopupPage
{
    private readonly DatabaseHelper _dbHelper;
    private ObservableCollection<AttendanceLog> _logs;
    private string selectedEventName;
    private string selectedEventCategory;
    private string selectedEventDate;
    private string selectedFromTime;
    private string selectedToTime;

    public ExportModal(DatabaseHelper dbHelper)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
        LoadEvents();
    }

    private async void LoadEvents()
    {
        var logs = await _dbHelper.GetLogsAsync();
        _logs = new ObservableCollection<AttendanceLog>(logs);

        EventPicker.ItemsSource = _logs.Select(l => l.EventName).Distinct().ToList();
    }

    private void EventPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (EventPicker.SelectedIndex >= 0)
        {
            selectedEventName = EventPicker.SelectedItem.ToString();
            CategoryPicker.ItemsSource = _logs.Where(l => l.EventName == selectedEventName).Select(l => l.EventCategory).Distinct().ToList();
            CategoryPicker.IsEnabled = true;

            CategoryPicker.SelectedIndex = -1;
            DatePicker.SelectedIndex = -1;
            TimePicker.SelectedIndex = -1;
        }
    }

    private void CategoryPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (CategoryPicker.SelectedIndex >= 0)
        {
            selectedEventCategory = CategoryPicker.SelectedItem.ToString();
            DatePicker.ItemsSource = _logs.Where(l => l.EventName == selectedEventName && l.EventCategory == selectedEventCategory).Select(l => l.EventDate).Distinct().ToList();
            DatePicker.IsEnabled = true;

            DatePicker.SelectedIndex = -1;
            TimePicker.SelectedIndex = -1;
        }
    }

    private void DatePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (DatePicker.SelectedIndex >= 0)
        {
            selectedEventDate = DatePicker.SelectedItem.ToString();
            TimePicker.ItemsSource = _logs.Where(l => l.EventName == selectedEventName && l.EventCategory == selectedEventCategory && l.EventDate == selectedEventDate).Select(l => $"{l.FromTime} - {l.ToTime}").Distinct().ToList();
            TimePicker.IsEnabled = true;

            TimePicker.SelectedIndex = -1;
        }
    }

    private void TimePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (TimePicker.SelectedIndex >= 0)
        {
            var timeRange = TimePicker.SelectedItem.ToString().Split(" - ");
            selectedFromTime = timeRange[0].Trim();
            selectedToTime = timeRange[1].Trim();
        }
    }

    private async void ExportToExcel_Clicked(object sender, EventArgs e)
    {
        List<AttendanceLog> filteredLogs;

        if (string.IsNullOrEmpty(selectedEventName) || string.IsNullOrEmpty(selectedEventCategory))
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Export Failed", "Please select an event and category."));
            return;
        }

        if (string.IsNullOrEmpty(selectedEventDate) && string.IsNullOrEmpty(selectedFromTime))
        {
            filteredLogs = await _dbHelper.GetLogsByEventAndCategoryAsync(selectedEventName, selectedEventCategory);
        }
        else if (!string.IsNullOrEmpty(selectedEventDate) && string.IsNullOrEmpty(selectedFromTime))
        {
            filteredLogs = await _dbHelper.GetLogsByEventCategoryAndDateAsync(selectedEventName, selectedEventCategory, selectedEventDate);
        }
        else
        {
            filteredLogs = await _dbHelper.GetFilteredLogsAsync(selectedEventName, selectedEventCategory, selectedEventDate, selectedFromTime, selectedToTime);
        }

        if (filteredLogs == null || filteredLogs.Count == 0)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Export Failed", "No records found for the selected filters."));
            return;
        }

        filteredLogs = filteredLogs.OrderByDescending(log => log.Timestamp).ToList();

        string fileName = $"{selectedEventName.Replace(" ", "_")}_Logs.xlsx";
        string filePath = Path.Combine("/storage/emulated/0/Download/", fileName);

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Attendance Logs");
                worksheet.Cell(1, 1).Value = "ID Number";
                worksheet.Cell(1, 2).Value = "Event Name";
                worksheet.Cell(1, 3).Value = "Event Category";
                worksheet.Cell(1, 4).Value = "Event Date";
                worksheet.Cell(1, 5).Value = "Event Time";
                worksheet.Cell(1, 6).Value = "Business Unit";
                worksheet.Cell(1, 7).Value = "Name";
                worksheet.Cell(1, 8).Value = "Data Scanned";
                worksheet.Cell(1, 9).Value = "Status";

                int row = 2;
                foreach (var log in filteredLogs)
                {
                    worksheet.Cell(row, 1).Value = log.IdNumber;
                    worksheet.Cell(row, 2).Value = log.EventName;
                    worksheet.Cell(row, 3).Value = log.EventCategory;
                    worksheet.Cell(row, 4).Value = log.EventDate;
                    worksheet.Cell(row, 5).Value = $"{log.FromTime} - {log.ToTime}";
                    worksheet.Cell(row, 6).Value = log.BusinessUnit;
                    worksheet.Cell(row, 7).Value = log.Name;
                    worksheet.Cell(row, 8).Value = log.Timestamp;
                    worksheet.Cell(row, 9).Value = log.Status;
                    row++;
                }

                worksheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }

            await MopupService.Instance.PushAsync(new DownloadModal("Export Successful", $"File saved: {filePath}"));
        }
        catch (Exception ex)
        {
            await MopupService.Instance.PushAsync(new DownloadModal("Export Error", $"Failed to export: {ex.Message}"));
        }
    }
}