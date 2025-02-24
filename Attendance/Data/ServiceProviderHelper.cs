namespace Attendance.Data
{
    public static class ServiceProviderHelper
    {
        public static T GetService<T>() where T : class => Application.Current.MainPage.Handler.MauiContext.Services.GetService<T>();
    }
}
