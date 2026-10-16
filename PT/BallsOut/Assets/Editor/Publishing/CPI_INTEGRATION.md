# Flick CPI SDK entegrasyonu

16 Ekim 2026. Son kapsam: AppsFlyer + mevcut Meta SDK + Android anahtarları ayarlanmış GameAnalytics. Video kaydı kapsam dışında.

## Kurulum

| Bileşen | Durum |
| --- | --- |
| AppsFlyer | 7.0.3; resmî UPM deposundaki commit sabitlendi |
| Meta | Projede mevcut SDK, App ID ve Client Token kullanılıyor |
| GameAnalytics | 8.3.0; resmî UPM deposundaki commit sabitlendi |
| GameAnalytics anahtarları | Kullanıcının verdiği yeni Android anahtarları kaydedildi; iOS boş. Eski TinySauce anahtarları okunmuyor |
| Android kimliği | `com.WaowGames.BallBlocksDropPuzzle` |
| Mevcut uygulama sürümü | `0.1.0` / Android versionCode `1` |

`CpiAnalyticsBootstrap` başlangıç sahnesinden bağımsız olarak bir kez oluşur ve sahne geçişlerinde korunur. Sahnelere bileşen eklemek gerekmez. SDK servisleri oyun akışına müdahale etmeden çalışır; Meta/GA hataları AppsFlyer başlangıcını engellemez.

Yayıncı ayarları: `Assets/Resources/Publishing/CpiAnalyticsSettings.asset`.

Unity menüsü: **Tools > Flick CPI > Select Publisher Settings**.

Her SDK ayrı kapatılabilir. Editor'da analitik başlangıcı varsayılan olarak kapalıdır. Gerçek veri teslimi cihaz ve yayıncı panelleri üzerinden kontrol edilmelidir.

## AppsFlyer PlayTime

Rehberdeki Dev Key ayarlandı. SDK 7'nin `init` / `registerSessionReadyListener` / `start` / `logEvent` API'leri kullanılıyor. `OnSessionReady` dinleyicisi init öncesinde bağlanır; native dinleyici init sonrasında kaydedilir. Her hazır olma callback'inde o oturum için `start` çağrılır. PlayTime ölçümü ilk oturum başlangıcından sonra açılır. Dinleyici uygulama ömrü boyunca korunur ve bootstrap yok edilince abonelik kaldırılır.

`f_custom_playtime`, pause/focus kaybı/quit sırasında yalnızca son ön plan parçasını bildirir. Arka planda geçen süre sayılmaz. Aynı geçişte Pause + Focus + Quit bildirimleri çakışırsa parça yalnızca bir kez gönderilir. Bir saniyeden kısa parçalar gönderilmez.

| Alan | Gönderilen değer |
| --- | --- |
| `playtime` | Son parçanın tam saniye süresi; birikimli oturum toplamı değil |
| `total_play_time` | PlayerPrefs'te cihazda tutulan birikimli saniye |
| `session_id` | Her uygulama açılışında oluşturulan GUID |
| `reason` | `pause`, `background` veya `quit` |
| `no_ads` | Varsayılan `"0"`; doğrulanmış reklam kaldırma hakkı varsa `"1"` |
| `current_stage` | LevelManager'ın bir tabanlı bölüm numarası |

Tüm alanlar `Dictionary<string, string>` içinde gönderilir. Normal çalışma sırasında `Update()` veya periyodik ağ çağrısı kullanılmaz. Çevrimdışı event kuyruğunu AppsFlyer'ın native SDK'sı yönetir.

Projeye ileride tam ekran reklam eklenirse, reklam gösterilmeden **önce** `CpiAnalyticsBootstrap.BeginFullscreenAd()`, kapandığında veya gösterim başarısız olduğunda `EndFullscreenAd()` çağrılmalı. Bu akış reklamın oluşturduğu pause/focus değişimlerinde PlayTime göndermeyi engeller ve reklam süresini saymaz.

Doğrulanmış reklam kaldırma satın alımı veya geri yükleme sonrasında `CpiAnalyticsBootstrap.SetNoAds(true)` kullanılabilir. Şu an reklam kaldırma ürünü olmadığı için değer `0` kalır.

## Meta

Mevcut FacebookSettings ve Android manifest kimlikleri kullanılıyor. `FB.Init` ardından `FB.ActivateApp` çalışır; uygulama arka plandan döndüğünde aktivasyon yenilenir. Oyun zaman ölçeği ve UI görünürlüğü değiştirilmez.

Meta uygulamasının Live durumu, Android paket/key-hash kaydı, Events Manager veri alımı ve yayıncı partner erişimi hesap üzerinden ayrıca kontrol edilmelidir.

Windows'ta Meta'nın Editor doğrulaması OpenSSL ister. Bu bilgisayardaki mevcut Git/OpenSSL kurulumu kullanılarak `FacebookAndroidUtil.SetupProperly = true` sonucu alındı. Editor hazırlığı, standart Git kurulumundaki OpenSSL'i gerekirse yalnızca Unity sürecinin PATH'ine ekler; sistem/kullanıcı PATH'i değiştirilmez.

## GameAnalytics

Kullanıcının verdiği yeni Game Key ve Secret Key, SDK'nın ayar API'siyle Android platformuna kaydedildi. GameAnalytics etkinleştirme ayarı açık. Ayarlar **Window > GameAnalytics > Select Settings** üzerinden veya `Assets/Resources/GameAnalytics/Settings.asset` dosyasında görülebilir. iOS anahtarları henüz girilmedi.

Yeni Android build'lerinde GameAnalytics otomatik başlatılır. İlgili platformun anahtarları boşken SDK başlatılmaz, GameAnalytics nesnesi oluşturulmaz ve event gönderilmez. Eski TinySauce ayarlarından anahtar kopyalanmaz.

Mevcut `GameEvents` üzerinden `Start`, `Complete`, `Fail` progression eventleri `level:<bölüm>` olarak gönderilir. Oynanırken retry yapılırsa önceki deneme Fail ile kapanır. Zaten başarısız olmuş bölümün retry'ı ikinci bir Fail üretmez. Sürüm bilgisi Player Settings'ten alınır.

## Android

AppsFlyer native SDK/RPC köprüsü, Install Referrer 2.1 ve Google Advertising ID bağımlılıkları mevcut External Dependency Manager üzerinden çözüldü.

Eski Gradle şablonları Unity 6000.0.68f1'in kendi şablonlarına uyarlandı: namespace, Java 17 ve güncel Unity yer tutucuları korundu. Önceki şablonlar `Assets/Editor/Publishing/Legacy/Gradle` altında saklandı. Android 8+ yerleşik multidex desteği nedeniyle eski özel MultiDexApplication sınıfı manifestten çıkarıldı. İnternet, ağ durumu ve AD_ID izinleri mevcut.

Manifest birleştirmesindeki `tools:replace` alanları açık `android:` namespace'i kullanacak şekilde düzeltildi. Launcher label/icon ve library backup alanları aynı attribute'u iki farklı anahtar olarak birleştirmiyor.

Paket kimliği, sürüm, imzalama kimliği ve oyun sahneleri değiştirilmedi. Mevcut Meta bağımlılıkları korunur.

## iOS

ATT native köprüsü yalnızca iOS'a dahildir. İlk açılışta ATT kararı beklenir; bekleme 30 saniye ile sınırlıdır. Ardından SDK'lar başlatılır. Meta reklam takibi ATT sonucuna göre ayarlanır. İzin reddedilse de SDK'lar izin verilen verilerle başlatılabilir.

iOS build sonrasında `NSUserTrackingUsageDescription`, AppsFlyer SKAN endpoint'i ve AppTrackingTransparency framework'ü Xcode projesine eklenir. SDK 7'de kaldırılmış olan `waitForATTUserAuthorization` çağrısı kullanılmaz; bekleme uygulama tarafındadır.

**iOS yayını için eksik:** Publisher Settings içindeki sayısal Apple App Store ID. AppsFlyer etkin olup bu alan boşsa iOS build anlaşılır bir hata ile durdurulur. iOS derlemesi macOS/Xcode üzerinde ayrıca yapılmalıdır.

## Teslim öncesi kalan hesap/cihaz adımları

- Yayıncıya Android paket adını ve yayınlanacak sürümü ilet.
- Android cihazda ilk açılış ve arka plana dönüş sonrası AppsFlyer verisini panelden kontrol et.
- PlayTime alanlarının parça süreleri olarak geldiğini ve arka plan süresinin eklenmediğini panelden kontrol et.
- Meta Events Manager'da uygulama aktivasyonunu kontrol et.
- Yeni anahtarları içeren Android build'i cihazda açıp GameAnalytics Realtime > Live events ekranını kontrol et.
- Google Play yayını için gerçek upload keystore/App Signing kullan. Yerel debug imzalı APK mağaza teslimi değildir.

16 Ekim 2026'da bağlı Samsung SM-A145F cihazında AppsFlyer tanı logları okundu. Önceki başlangıç kodunda eksik olan SDK 7 session-ready dinleyicisi düzeltildi. Düzeltilmiş APK'da soğuk başlangıç ve arka plandan dönüşte dinleyici → callback → start sırası cihaz logunda doğrulandı. Arka plana geçişte `f_custom_playtime` eventi 22 saniye ve rehberdeki zorunlu alanlarla üretildi.

**AppsFlyer 404 sorunu çözüldü:** İlk kontrolde takip uçları `HTTP 404 Not Found` / `application doesn't exist` döndürüyordu. Yayıncı `com.WaowGames.BallBlocksDropPuzzle` paketini AppsFlyer hesabına kaydettiğini bildirdikten sonra aynı paket ve Dev Key ile cihazdan tekrar denendi. Oturum isteği `HTTP 200 OK` / `ok`, 32 saniyelik `f_custom_playtime` isteği de `HTTP 200 OK` / `ok` aldı; SDK görev sonuçları sırasıyla `LAUNCH: SUCCESS` ve `INAPP: SUCCESS`. Eventte zorunlu alanlar mevcut. Başarılı AppsFlyer oturum ve event teslimi cihaz logunda doğrulandı. Yayıncının BigQuery SDK kontrolü ayrıca yeniden çalıştırılmalı; bu panelin başarılı sonucu henüz gözlenmedi.

GameAnalytics paneline verinin ulaştığı kullanıcı tarafından bildirildi. Gelir entegrasyonu rehberde Biz PM görüşmesine bağlı isteğe bağlı bölümdür; otomatik satın alma/reklam geliri raporlaması eklenmedi.

## Yapılan derleme kontrolü

- Unity C# derlemesi başarılı.
- ARMv7 ve ARM64 IL2CPP derlemesi başarılı.
- Son tam Unity Android build sonucu: `Succeeded`.
- SDK 7 başlangıç düzeltmesini ve yeni GameAnalytics anahtarlarını içeren normal APK da üretildi: `BallsOut-CPI-fixed.apk`. Tanı kontrolünden sonra mevcut oyun verileri korunarak telefona bu normal sürüm geri kuruldu.
- Yerel APK üretildi: `BallsOut-CPI-local.apk` (yaklaşık 65 MiB). Bu, cihaz kontrolü için varsayılan debug anahtarıyla imzalı yerel APK'dır. Bu APK yeni GameAnalytics anahtarları girilmeden önce üretildi; yeni anahtarları içermesi için yeniden build alınmalı.
- APK içinde AppsFlyer RPC bridge, Meta FacebookSdk ve GameAnalytics Java sınıfları ile ARMv7/ARM64 native kütüphaneleri görüldü.
- Paylaşılan Gradle önbelleğinde dosya taşıma hatası görüldüğü için son build ayrı bir Gradle önbelleğiyle alındı. Unity'nin `GRADLE_USER_HOME` ortam değeri işlem sonunda önceki haline döndürüldü.
- Build raporundaki Meta önkoşul uyarısının kaynağı OpenSSL'in bulunamamasıydı; yukarıdaki Editor hazırlığıyla kurulum kontrolü düzeltildi. Diğer hata kaydı build'i çağıran aracın 5 saniyelik bekleme sınırıydı; native build arka planda devam ederek başarıyla tamamlandı.
- Mevcut oyun sahnesi build sonunda geri yüklendi. Unity test dosyası oluşturulmadı ve test paketi çalıştırılmadı.

## Kaynaklar

- [Yayıncı CPI rehberi ve alt dokümanları](https://flickdifferent.notion.site/cpi-test-guide-for-partners?p=0c7a45eaf6d38240b0fd81e25595db1a&pm=c)
- [AppsFlyer resmî Unity deposu](https://github.com/AppsFlyerSDK/appsflyer-unity-plugin)
- [AppsFlyer SDK 7 başlangıç akışı](https://github.com/AppsFlyerSDK/appsflyer-unity-plugin/blob/master/docs/BasicIntegration.md)
- [AppsFlyer Android SDK 7 oturum başlangıcı](https://dev.appsflyer.com/hc/docs/integrate-android-sdk-7)
- [AppsFlyer Android 404 hata açıklaması](https://dev.appsflyer.com/hc/docs/troubleshooting-android-7)
- [GameAnalytics resmî Unity kurulum rehberi](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/)
