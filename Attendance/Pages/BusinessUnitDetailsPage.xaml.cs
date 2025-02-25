using Attendance.Data;

namespace Attendance.Pages;

public partial class BusinessUnitDetailsPage : ContentPage
{
    private DatabaseHelper _dbHelper;
    private string _eventName;
    private string _businessUnit;
    public BusinessUnitDetailsPage(DatabaseHelper dbHelper, string eventName, string businessUnit)
    {
        InitializeComponent();

        _dbHelper = dbHelper;
        _eventName = eventName;
        _businessUnit = businessUnit;

        BusinessUnitTitle.Text = $"{_businessUnit} Attendance";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAttendanceData();
    }

    private async Task LoadAttendanceData()
    {
        var presentEmployees = await _dbHelper.GetPresentEmployeesByBUAsync(_eventName, _businessUnit);
        var absentEmployees = await _dbHelper.GetAbsentEmployeesByBUAsync(_eventName, _businessUnit);

        PresentEmployeesList.ItemsSource = presentEmployees;
        AbsentEmployeesList.ItemsSource = absentEmployees;
    }
}