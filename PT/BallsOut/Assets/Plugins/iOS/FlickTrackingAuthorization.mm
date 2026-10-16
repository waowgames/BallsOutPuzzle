#import <AppTrackingTransparency/AppTrackingTransparency.h>
#import <UIKit/UIKit.h>

extern "C" int FlickGetTrackingAuthorizationStatus()
{
    if (@available(iOS 14.0, *))
        return (int)[ATTrackingManager trackingAuthorizationStatus];
    return 3;
}

extern "C" void FlickRequestTrackingAuthorization()
{
    if (@available(iOS 14.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (UIApplication.sharedApplication.applicationState == UIApplicationStateActive)
                [ATTrackingManager requestTrackingAuthorizationWithCompletionHandler:^(ATTrackingManagerAuthorizationStatus status) {}];
        });
    }
}
