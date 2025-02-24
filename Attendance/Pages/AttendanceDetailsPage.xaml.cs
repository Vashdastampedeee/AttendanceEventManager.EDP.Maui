using System.Diagnostics;
using Attendance.Data;

namespace Attendance.Pages;

public partial class AttendanceDetailsPage : ContentPage
{
    private DatabaseHelper _dbHelper;
    private string _eventName;

    public AttendanceDetailsPage(DatabaseHelper dbHelper, string eventName)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
        _eventName = eventName;

        Debug.WriteLine($"[INFO] AttendanceDetailsPage initialized for event: {_eventName}");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Debug.WriteLine($"[INFO] AttendanceDetailsPage OnAppearing triggered for event: {_eventName}");

        await LoadAttendanceData();
    }

    private async Task LoadAttendanceData()
    {
        try
        {
            Debug.WriteLine($"[INFO] Loading attendance data for event: {_eventName}");

            var presentEmployees = await _dbHelper.GetPresentEmployeesAsync(_eventName);
            var absentEmployees = await _dbHelper.GetAbsentEmployeesAsync(_eventName);

            // Log each present employee
            Debug.WriteLine($"[INFO] Total Present Employees: {presentEmployees.Count}");
            foreach (var employee in presentEmployees)
            {
                Debug.WriteLine($"[PRESENT] ID: {employee.IdNumber}, Name: {employee.Name}, Business Unit: {employee.BusinessUnit}");
            }

            // Log each absent employee
            Debug.WriteLine($"[INFO] Total Absent Employees: {absentEmployees.Count}");
            foreach (var employee in absentEmployees)
            {
                Debug.WriteLine($"[ABSENT] ID: {employee.IdNumber}, Name: {employee.Name}, Business Unit: {employee.BusinessUnit}");
            }

            PresentEmployeesList.ItemsSource = presentEmployees;
            AbsentEmployeesList.ItemsSource = absentEmployees;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ERROR] Failed to load attendance data: {ex.Message}");
        }
    }
}