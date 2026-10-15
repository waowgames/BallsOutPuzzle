# Yarn oyunu — hazırlık dokümanı

Kaynak proje: **Balls Out** (`Ball Blocks Drop Puzzle`, Unity 6000.0.68f1, URP 17, `com.WaowGames.BallBlocksDropPuzzle`).
Hedef: aynı oyunun **iplik (yarn) temalı** kopyası. Bu doküman, proje export edildikten sonra yeni projede işi yapacak oturum için yazıldı; kod henüz değişmedi.

## 1. Konsept (benim anladığım)

| Balls Out | Yarn oyunu |
|---|---|
| Üstte renkli top yığını (rezervuar) | Üstte **iple örülmüş bir desen** (her top = bir ilmek) |
| Altta sürüklenen, içine top dolan kutular | Altta **inceltilmiş kutular** (yassı örgü tablası / kasnak) |
| Top kutuya zıplayıp içine yığılır | İlmek sökülür, ip kutuya gider ve **kutunun üstüne örülür** |
| Kutu dolunca kalkıp kaybolur | Örgü parçası tamamlanınca kalkıp kaybolur |

Oyun kuralları aynı kalıyor: kutuyu alt ızgarada sürükle, aynı renkteki ilmekler kutuya aksın, bütün desen sökülüp bütün kutular dolunca level biter.

> Bu tablo iki cümlelik tariften çıkarıldı. Yanlış anladığım yer varsa önce Bölüm 8'deki soruları yanıtlamak gerekir.

## 2. Temel karar: simülasyona dokunma, görsel katmanı değiştir

Oyunun mantığı ile görseli zaten ayrı. Top bir `MonoBehaviour` değil, düz bir durum nesnesi (`BallState`); görseli havuzdan gelen bir `Transform`. Bu yüzden yeni oyun büyük ölçüde **reskin**:

- **Aynen kalır:** ızgara, top simülasyonu, toplama kuralları, kutu hareketi, tamamlanma, buz / kilit-anahtar / zincir / engel / iç katman / feeder / conveyor mekanikleri, level formatı, 66 referans level, level üretici ve doğrulayıcı Python araçları, UI, ekonomi, kayıt, ses altyapısı, tutorial ve zorluk sunumu.
- **Değişir:** topun görseli, kutunun görseli ve dolum görünümü, toplama animasyonu (ip), zemin/rezervuar sanatı, renk paleti, ses ve efektler, mağaza kimliği.

Sınıf ve namespace adları (`BallsOut`, `Ball*`) ilk aşamada **yeniden adlandırılmasın**. Oyuncu görmüyor, yeniden adlandırma ise prefab/asset referanslarını ve Python doğrulayıcının port ettiği sınıf adlarını riske atıyor. İstenirse en sonda tek seferde yapılır.

## 3. Mevcut mimari (yeni oturum için harita)

Oyun kodu `Assets/_Game/Scripts` altında, `BallsOut` namespace'inde. Genel şablon kodu (`Assets/Scripts`) namespace'siz.

**Giriş noktası:** `Core/BallBoxLevelRuntime.cs` — `LoadLevel()` her şeyi kurar ve bütün olayları dışarı açar (`OnBallCollected`, `OnBoxFillChanged`, `OnBoxCompleted`, `OnLevelWon` …). `Active` statik referansı ve `OnRuntimeLoaded` olayı ile ses, haptik, tutorial, booster UI buraya bağlanır.

| Alan | Dosya | Görev |
|---|---|---|
| Level verisi | `Level/LevelDefinition.cs`, `Level/CellMask.cs` | Izgara ölçüleri, `boxes`, `balls`, `feeders`, `conveyor`, `obstacles`, `links`. Top konumu formülleri (`BallRowZ`, `BallColumnX`) burada |
| Renk | `Level/BallColorDefinition.cs` | `ballMaterial`, `boxMaterial`, `ballPrefabOverride`. Renk kimliği asset'in kendisi |
| Şekil | `Level/BoxShapeDefinition.cs` | Hücre listesi ve kapasite hesabı |
| Görsel kayıt | `Core/PrefabRegistry.cs` | `ballPrefab`, `ballScale`, `ballHeight`, `fillOffset`, zemin prefabları, şekle özel kutu prefabları, gölge |
| Top ızgarası | `Grid/BallMicroGrid.cs`, `Grid/BoardGrid.cs` | Makro hücre başına 4×4 mikro site, satırlar yarım kaydırmalı (odd-r) |
| Simülasyon | `Simulation/BallSimulationSystem.cs` | Sabit tick'li (0.08 sn) deterministik düşme; kare hızından bağımsız |
| Toplama | `Balls/BallCollectionSystem.cs` | Hangi top hangi kutuya girer, "straggler magnet" |
| Dolum | `Boxes/BoxFillSystem.cs` | Topun kutudaki slotuna uçuş animasyonu (`Render`) ve `OnBallContact` |
| Kutu | `Boxes/BoxController.cs` | Slot yerleşimi (`FillSlots`), kapasite, görsel kurulum, buz/kilit/anahtar |
| Kutu görseli | `Boxes/BoxShapeVisual.cs` | Prefab yoksa prosedürel kutu mesh'i, iç tepsi, dış çizgi |
| Tamamlanma | `Boxes/BoxCompletionSystem.cs`, `BoxCompletionEffect.cs` | Dolu kutunun kalkıp kaybolması |
| Havuz | `Pooling/BallPool.cs` | Renk başına top görseli havuzu |
| Rezervuar sanatı | `Core/BallHopperVisual.cs`, `Core/BoardRimMesh.cs`, `Core/BoardPresentation.cs` | Hazne, çerçeve, zemin karoları, kamera kadrajı |
| Beslemeler | `Balls/BallFeederSystem.cs`, `BallConveyorSystem.cs`, `ConveyorTrack.cs` | Tüp ve bant kuyrukları |
| Engeller | `Grid/BoardObstacleSystem.cs`, `ObstacleVisual.cs`, `Boxes/IceBoxVisual.cs`, `BoxChain.cs`, `BoxLockVisual.cs`, `BoxKeyVisual.cs` | Taş, buz, zincir, kilit, anahtar (hepsi prosedürel mesh) |
| Boosterlar | `Boosters/*` | Fill, Magnet, Hammer |
| Ses / haptik | `Core/BallBoxAudio.cs`, `Core/BallBoxHaptics.cs` | Runtime olaylarını `SoundManager` ve haptiğe bağlar |
| Editor | `Editor/LevelDefinitionEditor.cs`, `GridAndBoxGameBuilder.cs`, `LevelPreviewCapture.cs` | Level inspector, sahne kurucu, önizleme |

**İçerik:** `Assets/_Game/Content/GridAndBox/` — `PrefabRegistry.asset`, 5 renk (`Color_*.asset`), 6 şekil (`Shape_*.asset`), `ReferenceLevels/` (66 level + `build_levels.py`, `level_generator.py`, `verify_levels.py`, `README.md`).
**Sahneler:** `Scenes/Loading Screen.unity`, `Scenes/Game Scene.unity`.
**Shader'lar:** `Assets/_Game/Resources/*.shader` (`Balls Out/...` adlarıyla, kodda `Shader.Find` ile bulunuyor) ve `Assets/Shaders` (`Money Design/Soft Plastic`, `SoftShadow`, `PolishedMoney`).

## 4. Değişecek yerler

### 4.1 Top → ilmek
- `PrefabRegistry.ballPrefab` yerine ilmek prefabı; her `Color_*.asset` için yeni `ballMaterial` (iplik dokulu). Kod değişmeden çalışır — `BallPool.Rent` prefabı ve materyali buradan alıyor.
- Yığın yarım kaydırmalı bir kafeste duruyor (`LevelDefinition.BallColumnX`, satır aralığı `BallRowSpacing`). **Kafes korunmalı**: değişirse simülasyon, 66 level ve `verify_levels.py` birlikte bozulur. İlmek mesh'i bu kafeste boşluksuz döşenecek şekilde tasarlanmalı (örgüdeki "V" ilmekler zaten satır satır yarım kayık oturur; `ballScale` ile bindirme ayarlanır).
- Yığın bir **desen** gibi okunmalı: level'lardaki renk dizilimi (`mix=` "stripes / checker / diagonal / zigzag") zaten desen üretiyor. Gerçek piksel-art resim isteniyorsa `build_levels.py`'ye resimden renk haritası okuyan bir mod eklenir (Bölüm 8, soru 2).

### 4.2 Kutu → ince örgü tablası
- `BoxShapeVisual.Create` (prosedürel) ve/veya `PrefabRegistry.boxes` (şekle özel prefab): duvarlar alçaltılır, kutu yassı bir tabla/kasnak olur. `CreateInner` (iç katman tepsisi) ve `CreateOutline` aynı ölçülere uymalı.
- `BoxController.Initialize` içinde yüksekliğe bağlı her şey gözden geçirilir: `FillRoot` ofseti (`registry.fillOffset`), `lidTop` (kilit, anahtar, buz, zincir direği bu yüksekliğe oturuyor), tıklama `BoxCollider` yüksekliği, `BoxFillLabel` ve `BoxAxisArrow` konumu.
- `PrefabRegistry.boxShadowHeight` / `boxShadowOffset` ince kutuya göre yeniden ayarlanır.

### 4.3 Dolum → "üstüne örmek"
- Bugün: kapasite = `slot/katman × fillLayers` (varsayılan 3 katman), toplar üst üste yığılıyor (`BoxFillSystem.GetSlotPosition`, `FillSpacing.y`).
- Önerilen: **kapasite ve top sayıları aynı kalsın**, sadece görünüm değişsin — katmanlar arası yükseklik çok küçültülür, her katman örgünün bir "kat"ı gibi görünür. Böylece level'lar yeniden üretilmeden çalışır. (Tek katmana inmek top sayılarını üçte bire düşürür ve bütün level'ların yeniden üretilmesini gerektirir.)
- `FillSlots` zaten satır satır, soldan sağa sıralı (`BoxController.Initialize` sonundaki `Array.Sort`) — örgünün sıra sıra ilerlemesi için hazır.
- `FillBallDiameter` (slot aralığı × 1.08) ilmeklerin boşluksuz birleşmesi için ayarlanır.

### 4.4 Toplama animasyonu → ip
- `BoxFillSystem.Render`: şu anki zıplama eğrisi yerine ilmeğin sökülüp kutuya süzülmesi.
- Yeni görsel: yığından kutuya uzanan **ip hattı**. Kancalar hazır: `Collection.OnBallCollected` (ip başlar), `Fill.OnBallContact` (ilmek tablaya değer). Tüp mesh üreten yardımcı zaten var: `LockKeyModels.Builder.Tube`; `BoxChain.cs` iki nokta arasında sarkan hat çizmenin örneği.
- Simülasyon saatine dokunulmaz; ip yalnızca görseldir ve toplama sırasını etkilemez.

### 4.5 Tema dönüşümü gereken diğer parçalar
| Mevcut | Dosya | Yarn karşılığı (öneri) |
|---|---|---|
| Hazne / huni / çerçeve | `BallHopperVisual`, `BoardRimMesh` | Örgü kasnağı / ahşap çerçeve |
| Zemin karoları | `PrefabRegistry.floorPrefab`, `alternateTileMaterial` | Keçe / kumaş zemin |
| Feeder tüpü (cam) | `BallFeederSystem`, `FeederGlass.shader` | İplik makarası |
| Conveyor bandı | `BallConveyorSystem`, `ConveyorTrack` | İp yolu / mekik |
| Taş engel | `ObstacleVisual` | Düğme / ahşap blok |
| Buz | `IceBoxVisual`, `IceBox.shader` | Aynı kalabilir |
| Zincir | `BoxChain`, `ChainModels` | Kalın ip / kurdele |
| Kilit ve anahtar | `BoxLockVisual`, `BoxKeyVisual`, `LockKeyModels` | Aynı kalabilir |
| Hammer / Magnet / Fill booster | `Boosters/*`, `BoosterUIManager` | Makas / tığ / otomatik örme (ikon + efekt) |
| Tamamlanma efekti | `BoxCompletionEffect`, `CelebrationSpark.shader` | Aynı mantık, iplik renkli parçacık |

### 4.6 Ses, UI, metin
- `Resources/Audio/SoundLibrary.asset` — top/plastik sesleri yerine yumuşak iplik sesleri. `BallBoxAudio.cs` içindeki `SoundId` eşlemeleri aynı kalır.
- UI sprite'ları (`ART/UI`, `Prefabs/UI prefabs`), booster ikonları, loading ekranı, oyun adı.
- `Resources/TutorialConfig.asset` ve `Scripts/Tutorial/TutorialDirector.cs` içindeki metinlerde "ball / box" geçen yerler.

## 5. Export sonrası ilk iş listesi (kimlik ayrımı)

Yeni proje eski oyunun kimliğini taşımamalı:

1. `ProjectSettings`: `productName`, Android `applicationIdentifier`, `bundleVersion` / `AndroidBundleVersionCode` sıfırlanır.
2. **Yeni keystore** — `balloutkey.keystore` yeni oyunda kullanılmaz.
3. SDK ayarları oyun başına ayrıdır: `Resources/TinySauce/Settings.asset`, `Resources/GameAnalytics/Settings.asset`, `VoodooPackages/.../FacebookSettings.asset`, `Plugins/Android/mainTemplate.gradle` ve manifest içindeki uygulama kimlikleri.
4. `Scripts/Save/SaveService.cs` — kayıt anahtarı/dosya adı kontrol edilir (aynı cihazda iki oyun çakışmasın diye paket adı zaten ayırır, yine de gözden geçirilsin).
5. `Scripts/LevelConfig.asset` level listesi ve `EditorBuildSettings` sahneleri export sonrası referansları korumuş mu, kontrol edilir.
6. Oyun sahnesi açılıp bir level oynanır; **hiçbir şey değişmeden** eski oyunun çalıştığı doğrulanır. Tema işine bundan sonra başlanır.

Export notu: `.unitypackage` ile alınırsa `Packages/manifest.json` ve `ProjectSettings` gelmez (URP, Input System, Newtonsoft, DOTween/Odin ayarları, katmanlar, kalite ayarları). En güvenlisi proje klasörünü `Library`, `Temp`, `Logs`, `UserSettings` hariç kopyalamak.

## 6. Gereken sanat varlıkları

- İlmek mesh'i (düşük poligon; ekranda aynı anda binlerce tane olabilir, GPU instancing açık materyal şart).
- İplik materyali / shader'ı ve renk başına varyantlar (mevcut 5 renk: Aqua, Coral, Emerald, Lilac, Saffron + referans level renkleri).
- İnce kutu modelleri veya prosedürel ölçüleri: 1x1, 1x2, 2x1, 1x3, L, Plus.
- İp hattı materyali.
- Çerçeve/kasnak, zemin, makara (feeder), engel görselleri.
- UI: ikon seti, booster ikonları, logo, loading ekranı.
- Ses seti.

Kurulu skill'ler yeni projede doğrudan kullanılabilir: `unity-color-palette` (palet ve ColorGuide), `unity-ftue-tutorial`, `unity-hard-level-presentation`, `unity-play-store-screenshots`.

## 7. Uygulama sırası

1. **Kimlik ve sağlık kontrolü** — Bölüm 5.
2. **İlmek** — prefab + materyaller, `PrefabRegistry` ayarı; yığın desen gibi görünene kadar ölçek/bindirme ayarı.
3. **İnce kutu** — mesh, yükseklik bağımlı ofsetler, gölge, etiket.
4. **Örerek dolum** — slot yüksekliği, ilmek ölçeği, dolum animasyonu.
5. **İp hattı** — toplama sırasında yığından kutuya.
6. **Çevre** — çerçeve, zemin, feeder/conveyor, engel, zincir.
7. **Efekt, ses, booster teması.**
8. **UI, tutorial metinleri, renk paleti.**
9. **Level turu** — 66 level oynanarak görsel kontrol; `verify_levels.py` kurallar değişmediği için aynen geçerli.
10. **Mağaza** — ikon, ekran görüntüleri, açıklamalar.

Her adımın sonunda oyun oynanabilir kalmalı; 2–5 arası adımlarda oynanış kuralı değişmemeli.

## 8. Başlamadan önce netleşmesi gerekenler

1. **Desenin davranışı:** İlmekler toplar gibi aşağı doğru akıp boşlukları dolduracak mı (önerilen; kod aynen kalır), yoksa örgü yerinde sabit kalıp sadece alt sıradan mı sökülecek? İkincisi simülasyonun yeniden yazılması ve bütün level'ların yeniden tasarlanması demek.
2. **Üstteki desen gerçek bir resim mi olacak** (kalp, hayvan vb. piksel-art), yoksa mevcut renk şeritleri/desenleri yeterli mi? Resim isteniyorsa level üreticiye resimden renk haritası modu eklenir.
3. **"İnceltilmiş kutu"** yalnızca daha alçak bir kutu mu, yoksa tamamen düz bir tabla mı? Dolunca üstünde ne görünecek — düz örgü parçası mı, kabaran yumak mı?
4. **Level'lar:** Mevcut 66 level aynen mi kullanılacak, yoksa yeni level seti mi hazırlanacak?
5. **Kapasite:** Top sayıları aynı kalsın mı (önerilen), yoksa kutu başına tek kat ilmek mi?
6. **Oyunun adı ve paket adı.**
7. **Sanat kaynağı:** İlmek ve kutu modelleri hazır mı gelecek, yoksa prosedürel mesh olarak kodla mı üretilecek (mevcut kutu, kilit, buz, zincir zaten prosedürel)?
