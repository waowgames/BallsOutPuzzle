# Generic Level Template Design

## Amaç

Projeyi yeni Unity oyunları için güvenli bir başlangıç şablonuna dönüştürmek. Level sistemi yalnızca ortak yaşam döngüsünü yönetecek; Cannon Rings, voxel, hamle, timer, lane/bowl ve reklam-revive gibi oyun türüne bağlı davranışlar çekirdeğin dışında kalacak.

## Kapsam

### Silinecek oyun özelinde kalıntılar

- `Assets/Scripts/CannonRings` klasörü ve tüm içeriği
- Cannon Rings’e ait klasör meta dosyası
- Kullanılmayan `LevelCountdownUI`
- Kullanılmayan `LevelMissionManager`
- Bu iki component’in prefab üzerindeki serialized referansları
- `GameEvents` içindeki timer, mission, move, lane ve bowl eventleri
- `LevelManager` içindeki voxel, move budget, timer, klavye kısayolu ve revive davranışları
- `LevelFailPopup` içindeki add-time, bonus-move ve çalışmayan rewarded-ad revive kodu

### Korunacak davranışlar

- Level yükleme
- Oyunu başlatma
- Level tamamlama
- Level başarısızlığı
- Aynı level’ı tekrar başlatma
- Sonraki level’a ilerleme ve PlayerPrefs ile ilerleme kaydı
- Win popup açılması ve sonraki level’a devam edilmesi
- Fail popup açılması ve retry düğmesi
- Level numarasının event tabanlı güncellenmesi
- Mevcut popup stack ve UI input blocking düzeni

## Mimari

### LevelManager

`LevelManager`, level yaşam döngüsünün tek sahibi olacak:

1. Kayıtlı level index’ini yükler.
2. `LoadLevel` ile level verisini ve runtime durumunu hazırlar.
3. `StartLevel` ile gameplay durumuna geçer.
4. Gameplay sistemi gerektiğinde `CompleteLevel` veya `FailLevel` çağırır.
5. `RetryLevel` aynı index’i yeniden yükleyip başlatır.
6. Win popup sonrası mevcut index, bir sonraki level olarak yüklenir.

Manager içinde `Update` bulunmayacak. Oyun türüne bağlı ilerleme koşulları manager tarafından izlenmeyecek. Her yeni oyun kendi gameplay component’inden yalnızca ortak public API’yi çağıracak.

Tekrarlanan complete/fail çağrılarını engellemek için açık bir `LevelState` kullanılacak:

- `Uninitialized`
- `Loaded`
- `Playing`
- `Completed`
- `Failed`

`LevelConfig` atanmamış olsa bile lifecycle çalışabilecek. Böylece sahne tabanlı veya kendi veri sağlayıcısını kullanan yeni projeler template’i önce özel bir ScriptableObject üretmeden kullanabilecek. Config atanmışsa `CurrentLevelData`, listedeki generic `LevelData` örneğini sağlayacak.

### LevelData ve LevelConfig

`LevelData`, oyunların türeteceği boş ve generic ScriptableObject tabanı olarak kalacak.

`LevelConfig` yalnızca:

- Sıralı level listesi
- Listenin sonunda döngüye girme seçeneği
- Absolute index’ten level verisi çözümleme
- Kaç tam döngü tamamlandığını hesaplama

sorumluluklarını taşıyacak. Kullanılmayan difficulty multiplier ve time reduction alanları kaldırılacak.

### GameEvents

Event hub içinde yalnızca halen kullanılan genel event grupları kalacak:

- Level: loaded, started, completed, failed, retried
- Score
- UI screen/popup

Gameplay’e özel eventler yeni projelerde ilgili feature klasöründe tanımlanacak. Böylece template event hub zamanla oyun özelinde bir “God Class” hâline gelmeyecek.

### Win ve fail akışı

`LevelUpPopup`, `OnLevelCompleted` event’ini dinlemeye devam edecek. Gösterilen level numarası manager’ın ilerletilmiş index’inden değil, event ile gelen tamamlanan index’ten hesaplanacak. Böylece popup bitirilen level’ı gösterecek.

`LevelFailPopup`, `OnLevelFailed` ile açılacak ve retry düğmesi `RetryLevel` çağıracak. Timer ve rewarded-ad kodu kaldırılacak. Prefab üzerindeki çalışmayan ad/revive kontrolü görünmez hâle getirilecek; retry akışı korunacak.

`LevelDisplayUI`, sadece level numarasını gösterecek. Runtime’da kendiliğinden move-count text üretmeyecek.

## Dosya organizasyonu

Çalışan ve generic olan scriptler `Assets/Scripts/Old` altında bırakılmayacak. Unity GUID bağlantılarını korumak için scriptler `.meta` dosyalarıyla birlikte taşınacak:

- Core altyapısı → `Assets/Scripts/Core`
- Level lifecycle ve win/fail bileşenleri → `Assets/Scripts/Level`
- UI altyapısı ve UI efektleri → `Assets/Scripts/UI`
- Ses sistemi → `Assets/Scripts/Audio`
- Genel görsel efektler → `Assets/Scripts/Effects`

Taşıma sırasında class adları ve mevcut serialized GUID’ler korunacak. Böylece prefab bağlantıları kopmayacak.

## Performans ve kod kalitesi

- Level çekirdeğinde frame başına çalışan `Update` olmayacak.
- Scene taraması normal akışta kullanılmayacak; fail popup fallback araması yalnızca cached instance bulunamazsa gerçekleşecek.
- UI gizlenirken raycast kapatılacak ve popup canvas devre dışı bırakılacak.
- Gameplay özelindeki state ve eventler level çekirdeğine eklenmeyecek.
- Level manager yalnızca lifecycle, progression ve aktif level context sorumluluklarını taşıyacak.
- Runtime UI nesnesi üretme ve gereksiz allocation kaldırılacak.

## Prefab ve serialization güvenliği

- Silinen MonoBehaviour component blokları ilgili prefab YAML dosyalarından kaldırılacak.
- Taşınan scriptlerin `.meta` GUID değerleri korunacak.
- Cannon Rings scriptlerinin hiçbir scene, prefab veya ScriptableObject serialized referansı olmadığı doğrulandı.
- Değişiklik sonunda sahne/prefab dosyalarında silinen script GUID’leri aranacak.

## Hata davranışı

- Negatif level index’i sıfıra clamp edilecek.
- Boş veya atanmamış config lifecycle’ı durdurmayacak.
- `CompleteLevel` ve `FailLevel`, yalnızca `Playing` durumunda sonuç üretecek.
- Aynı level için complete/fail event’i birden fazla kez gönderilmeyecek.
- Retry, başarısız level index’ini değiştirmeden yeniden yükleyip başlatacak.

## Kontrol yaklaşımı

Kullanıcı talebi gereği Unity test dosyası oluşturulmayacak ve `verification-before-completion` skill’i kullanılmayacak.

Uygulama sonrası:

1. Silinen class ve event adları için statik referans taraması yapılacak.
2. Prefab ve scene dosyalarında silinen script GUID’leri aranacak.
3. Unity batchmode ile script compilation çalıştırılacak.
4. Compile log içindeki error ve missing-script mesajları incelenecek.

GitHub, branch, staging ve commit işlemleri yapılmayacak.
