using Attendance.Data;

namespace Attendance.Pages;

public partial class SplashPage : ContentPage
{
    private DatabaseHelper _dbHelper;
    public SplashPage(DatabaseHelper dbHelper)
    {
        InitializeComponent();
        _dbHelper = dbHelper;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var dots = new List<Button> { Dot1, Dot2, Dot3, Dot4 };
        var startTime = DateTime.Now;

        do
        {
            foreach (var dot in dots)
            {
                await dot.ScaleTo(1.5, 300, Easing.CubicInOut);
                await dot.ScaleTo(1.0, 300, Easing.CubicInOut);
            }

        } while ((DateTime.Now - startTime).TotalSeconds < 4);

        await Task.Delay(100);

        Application.Current.MainPage = new NavigationPage(new HomePage(_dbHelper));
    }
}