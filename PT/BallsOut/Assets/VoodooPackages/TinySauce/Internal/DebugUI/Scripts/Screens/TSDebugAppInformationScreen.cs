using UnityEngine;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Screens
{
    public class TSDebugAppInformationScreen : TSDebugScreen
    {
        protected override string ScreenTitle => "App Information";

        protected override void OnScreenShow()
        {
            TinySauceSettings settings = TinySauceSettings.Load();
            ClearWidgets();

            var appSection = AddFoldableSection("App Information");
            appSection.AddTextPair("Product Name", Application.productName);
            appSection.AddTextPair("Bundle ID", Application.identifier);
            appSection.AddTextPair("App Version", Application.version);

            var tinySauceSection = AddFoldableSection("TinySauce");

            var gameAnalyticsSection = tinySauceSection.AddFoldableSection("GameAnalytics");
            gameAnalyticsSection.AddTextPair("iOS Game Key", settings.gameAnalyticsIosGameKey);
            gameAnalyticsSection.AddTextPair("iOS Secret Key", settings.gameAnalyticsIosSecretKey);
            gameAnalyticsSection.AddTextPair("Android Game Key", settings.gameAnalyticsAndroidGameKey);
            gameAnalyticsSection.AddTextPair("Android Secret Key", settings.gameAnalyticsAndroidSecretKey);
        
            var facebookSection = tinySauceSection.AddFoldableSection("Facebook");
            facebookSection.AddTextPair("App ID", settings.facebookAppId);
            facebookSection.AddTextPair("Client Token", settings.facebookClientToken);
        
            var adjustSection = tinySauceSection.AddFoldableSection("Adjust");
            adjustSection.AddTextPair("iOS App Token", settings.adjustIOSToken);
            adjustSection.AddTextPair("Android App Token", settings.adjustAndroidToken);
        
        
        
        }
    }
}
