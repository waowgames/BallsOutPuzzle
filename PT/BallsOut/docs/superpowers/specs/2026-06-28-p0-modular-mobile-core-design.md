# P0 Modular Mobile Core Design

## Amaç

Template'i yeni bir mobil puzzle projesinde hızlı kullanılabilen, fakat yayın aşamasına giderken yeniden yazılması gerekmeyen bir çekirdeğe dönüştürmek.

P0 üç bağlı dilimden oluşur:

1. Kayıt ve soft-currency durumunu UI katmanından ayırmak.
2. Tek gameplay sahnesinde `LevelData` üzerinden level prefabı yüklemek.
3. Başlatma, retry, next-level ve ödül akışındaki yarım veya birbiriyle çakışan yolları temizlemek.

Git/GitHub işlemleri, Unity test dosyaları ve `verification-before-completion` skill'i kapsam dışıdır.

## Kapsam

### Dahil

- Tek merkezden çalışan, sürümlü ve legacy anahtarları taşıyan kayıt servisi
- UI'dan bağımsız soft-currency cüzdanı
- Event tabanlı currency metin görünümü
- `LevelData` içindeki prefabı tek gameplay sahnesinde yöneten level content loader
- UI ile level lifecycle arasındaki tek geçiş noktası
- Win, fail, main-menu play ve UI giriş animasyonu akışlarının sadeleştirilmesi
- Booster polling'inin ve çalışmayan reklam yollarının kaldırılması
- Sound ayarlarının doğrudan `PlayerPrefs` yerine kayıt servisini kullanması
- Reward state değişikliği ile reward görsel efektinin ayrılması
- Mevcut prefab serialized bağlantılarının yeni sorumluluklara göre güncellenmesi

### Hariç

- Reklam SDK'sı ve rewarded-ad uygulaması
- Analytics, remote config, cloud save ve anti-cheat
- Addressables
- Pooled coin uçuş animasyonunun tam görsel uygulaması; bu P1'de yapılacak
- Data-driven tutorial ve feedback preset sistemi; bunlar P1'de yapılacak
- Editor setup wizard ve asmdef ayrımı; bunlar P2'de yapılacak

## Mimari

### SaveService ve GameSaveData

`SaveService`, runtime kayıt verisinin tek sahibi olur. Diğer sistemler doğrudan `PlayerPrefs` çağırmaz.

`GameSaveData` şu P0 alanlarını taşır:

- Save format version
- Current absolute level index
- Soft-currency balance
- Music enabled
- SFX enabled
- Vibration enabled
- Booster id/count çiftleri

Storage backend P0 için sürümlü bir JSON string olarak `PlayerPrefs` kullanır. Bu seçim küçük hypercasual/puzzle kayıtları için yeterlidir ve ileride backend değiştirilmesini servis tüketicilerinden gizler.

Servis:

- Primary save bozuksa backup kaydını dener.
- Yeni kayıt yoksa `lm_currentLevel`, `score`, `BgMusicOn`, `SfxOn`, `VibrationOn` ve mevcut `booster_{id}_count` değerlerini ihtiyaç oldukça taşır.
- Negatif level, currency ve booster miktarlarını sıfıra clamp eder.
- Yalnızca state değiştiğinde serialize eder.
- Disk flush işlemini kritik transaction, application pause ve application quit noktalarında yapar.
- `Update` kullanmaz.

`SaveService`, diğer runtime servislerinden önce hazırlanmak için açık execution order kullanır.

### CurrencyWallet ve CurrencyDisplay

`CurrencyWallet` yalnızca soft-currency state ve transaction kurallarından sorumludur:

- `Balance`
- `BalanceChanged`
- `Add(int amount)`
- `TrySpend(int amount)`

Sıfır veya negatif ekleme reddedilir. Yetersiz bakiye harcamayı değiştirmez. Başarılı değişiklik `SaveService` üzerinden kaydedilir ve tek bir event gönderir.

`CurrencyDisplay`, `TextMeshProUGUI` referansını tutar, wallet event'ini dinler ve yalnızca metni günceller. `UIManager` içindeki score state, multiplier, save ve event sorumlulukları kaldırılır. `UIManager` sadece screen registry, popup stack ve input blocker sahibi kalır.

`GameEvents.OnScoreChanged` kaldırılır; currency değişimini doğrudan `CurrencyWallet.BalanceChanged` taşır. Böylece level event hub ekonomi eventleriyle büyümez.

### LevelData ve LevelContentLoader

`LevelData` generic kalır ancak tek ortak runtime içeriği olarak opsiyonel `GameObject levelPrefab` referansı sağlar. Her oyun bu sınıftan türeyip yalnızca kendi level alanlarını ekleyebilir.

`LevelContentLoader`:

- Serialized bir `levelRoot` referansı kullanır.
- `GameEvents.OnLevelLoaded` event'ini dinler.
- Önceki instance'ı hemen inactive yapıp `Destroy` kuyruğuna alır.
- Yeni prefabı `levelRoot` altında instantiate eder.
- Aktif instance ve aktif `LevelData` bilgisini read-only sunar.
- Level prefabı yoksa exception üretmez; scene-authored gameplay fallback'ine izin verir ve editor/development build'de uyarı verir.
- `Update`, scene taraması ve `Resources.Load` kullanmaz.

Bu tasarım, tercih edilen tek gameplay sahnesi + level prefab akışını sağlar; aynı zamanda boş config ile çalışan mevcut template davranışını bozmaz.

### GameFlowController

`GameFlowController`, UI katmanının level lifecycle'a açılan tek kapısı olur:

- `StartCurrentLevel()`
- `RetryCurrentLevel()`
- `ContinueToNextLevel()`

Controller state üretmez; `LevelManager` state'ini kullanır ve sadece geçiş sırasını yönetir.

Akışlar:

1. Play: current level yüklenir, ardından gameplay başlatılır.
2. Retry: failed level aynı absolute index ile tekrar yüklenir ve başlatılır.
3. Continue: `CompleteLevel` tarafından ilerletilmiş index yüklenir ve başlatılır.

`MainMenuPlayStarter`, yalnızca `GameFlowController.StartCurrentLevel` çağrısına yönlendirir. `UIAnimator` gameplay başlatmaz; sadece UI animasyonu oynatır. Win ve fail popup'ları doğrudan `LevelManager` lifecycle zinciri kurmaz.

### Win reward akışı

P0'da reward miktarı ayrı bir `LevelRewardConfig` ScriptableObject tarafından hesaplanır:

- Base reward
- Level başına artış
- Opsiyonel maksimum reward

`LevelUpPopup`, tamamlanan level index'i için gösterilecek miktarı config üzerinden alır.

Kullanıcı normal collect düğmesine bastığında:

1. Butonlar tek transaction için kilitlenir.
2. `CurrencyWallet.Add` ile reward anında ve yalnızca bir kez kaydedilir.
3. Varsa `FlyToUIEffect` yalnızca görsel callback olarak oynar.
4. Popup kapanır.
5. Görsel callback tamamlanınca `GameFlowController.ContinueToNextLevel` çağrılır.

Görsel efekt veya hedef referansı yoksa callback aynı frame çalışır. Böylece görsel efekt kayıp veya yarıda kesilmiş olsa bile reward ve next-level state doğru kalır.

Çalışmayan 2x rewarded-ad kodu `LevelUpPopup` içinden kaldırılır ve ilgili prefab butonu inactive kalır. Reklam sistemi daha sonra ayrı adapter olarak eklenebilir.

### Booster ve ses temizliği

P0 booster sistemi yalnızca currency ile satın alınan opsiyonel envanter olarak kalır:

- `InvokeRepeating` kaldırılır.
- UI yalnızca enable, currency change, purchase ve use eventlerinde yenilenir.
- Rewarded-ad fallback ve `IsRewardedAdReady` stub'ları kaldırılır.
- Watch-ad görselleri inactive yapılır.
- Booster adetleri `SaveService` içinde id ile tutulur.
- Mevcut, artık var olmayan `LevelManager.AddTime` UnityEvent bağlantısı prefabdan kaldırılır.

`SoundManager`, P0'da playback sorumluluğunu korur fakat music/SFX/vibration ayarlarını `SaveService` üzerinden okur ve yazar. Haptic playback'i ayrı servise bölmek P1 kapsamıdır.

## Veri akışı

### Uygulama açılışı

1. `SaveService` primary veya backup save'i yükler; gerekirse legacy migration yapar.
2. `CurrencyWallet` kayıtlı bakiyeyi alır.
3. `LevelManager` kayıtlı absolute level index'ini alır.
4. `CurrencyDisplay` mevcut bakiyeyi gösterir ve değişiklik event'ine abone olur.
5. `LevelManager.EnsureCurrentLevelLoaded` level event'ini gönderir.
6. `LevelContentLoader`, level prefabı varsa `levelRoot` altında oluşturur.

### Level tamamlama

1. Gameplay sistemi `LevelManager.CompleteLevel` çağırır.
2. `LevelManager` state'i `Completed` yapar, absolute index'i ilerletir ve kaydeder.
3. Completion event'i bitirilen index ile gönderilir.
4. `LevelUpPopup` reward miktarını gösterir.
5. Collect ile wallet transaction tamamlanır.
6. Görsel callback sonrasında controller yeni current index'i yükler ve başlatır.

### Retry

1. Gameplay sistemi `LevelManager.FailLevel` çağırır.
2. Fail popup açılır.
3. Retry düğmesi `GameFlowController.RetryCurrentLevel` çağırır.
4. Aynı absolute index tekrar yüklenir; content loader eski instance'ı kapatıp yenisini oluşturur.

## Prefab ve serialization düzeni

- Runtime servisleri mevcut sahne/prefab düzenini en az riskle koruyacak şekilde ayrı component'ler olarak tutulur.
- `UI Manager.prefab` üzerindeki `UIManager.scoreTxt` bağlantısı yeni `CurrencyDisplay` component'ine taşınır.
- `LevelManager`, `SaveService`, `CurrencyWallet` ve `GameFlowController` aynı root üzerinde bulunabilir; sınıf sorumlulukları birbirinden bağımsız kalır.
- Yeni scriptlerin `.meta` GUID'leri oluşturulur ve prefab YAML bağlantılarında bu GUID'ler kullanılır.
- Silinen serialized field ve UnityEvent blokları prefab YAML'dan temizlenir.
- Inactive booster/reward UI nesneleri raycast ve overdraw üretmeyecek şekilde `SetActive(false)` durumda kalır.

## Hata davranışı

- Save JSON okunamazsa backup denenir; o da okunamazsa güvenli default oluşturulur.
- Eksik `SaveService` veya `CurrencyWallet` referansı null exception yerine development uyarısı üretir ve state değiştirmez.
- Eksik level config veya prefab, scene-authored content fallback'i nedeniyle lifecycle'ı kilitlemez.
- Aynı win reward butonuna tekrar basılması ikinci currency transaction oluşturmaz.
- Aynı level için duplicate complete/fail çağrıları mevcut `LevelState` guard'larıyla reddedilir.
- Currency ve booster miktarları hiçbir public API üzerinden negatif olamaz.

## Performans ve kod kalitesi

- Yeni sistemlerin hiçbirinde `Update`, `FindObjectOfType`, LINQ veya frame başına allocation bulunmaz.
- Booster periyodik refresh yapmaz.
- UI metni yalnızca balance değiştiğinde yenilenir.
- Save yalnızca state mutation ve application lifecycle noktalarında serialize edilir.
- Level prefabları shared material ve GPU instancing kullanımına izin veren asset düzenini korur; loader material instance üretmez.
- Gizli popup ve opsiyonel UI nesneleri yalnız alpha ile saklanmaz; canvas veya GameObject devre dışı bırakılır.
- Her sınıf tek bir ana sorumluluk taşır.

## Kontrol yaklaşımı

Kullanıcı talebi gereği Unity test dosyası oluşturulmaz ve `verification-before-completion` skill'i kullanılmaz.

Uygulama sonunda:

1. Runtime scriptlerde doğrudan `PlayerPrefs` kullanımı yalnızca `SaveService` ile sınırlı olmalıdır.
2. `UIManager.Score`, `ScoreAdd`, `ScoreChanged`, ad stub'ları ve `LevelManager.AddTime` serialized çağrısı için statik referans taraması yapılmalıdır.
3. Yeni ve silinen script GUID'leri prefab/scene YAML içinde kontrol edilmelidir.
4. Unity batchmode compilation çalıştırılmalı; compiler error ve missing-script kayıtları incelenmelidir.

GitHub, branch, staging, commit, push ve pull request işlemleri yapılmaz.
