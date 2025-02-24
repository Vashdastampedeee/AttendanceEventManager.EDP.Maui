using Attendance.Data;
using Microsoft.Extensions.Logging;
using Mopups.Hosting;
using Plugin.Maui.Audio;

namespace Attendance
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureMopups()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG   
    		builder.Logging.AddDebug();
#endif
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "dbtest.db");
            builder.Services.AddSingleton<DatabaseHelper>(sp => new DatabaseHelper(dbPath));
            builder.Services.AddSingleton(AudioManager.Current);

            return builder.Build();
        }
    }
}
