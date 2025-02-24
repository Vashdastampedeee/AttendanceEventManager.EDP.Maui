using Attendance.Data;
using Attendance.Pages;

namespace Attendance
{
    public partial class App : Application
    {
        private DatabaseHelper _dbHelper;
        public App(DatabaseHelper dbHelper)
        {
            InitializeComponent();
            _dbHelper = dbHelper;
            MainPage = new SplashPage(_dbHelper);
        }
    }
}
