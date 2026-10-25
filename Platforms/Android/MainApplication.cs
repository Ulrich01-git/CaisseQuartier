using Android.App;
using Android.Runtime;
using Microsoft.Maui;

namespace CaisseQuartier;

[Application]
public class MainApplication(IntPtr handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
