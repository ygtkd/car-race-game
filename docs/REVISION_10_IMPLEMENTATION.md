# 改修10 実装記録

対象: COAST RACER。2026-09-27。実装はローカルまで。push・Azure公開は未実施。

## 変更
- 表示名を「湘南海岸コース」に統一。
- 1周4,707.44m、高速道路本線978.34m。入口／出口ランプは本線長に含めない。
- 一般道の新スタートから橋入口まで74.33m。ユーザーの追加指示により従来の600m制約を解除。
- 島内は山道と洞窟による八の字。交差する中心線は上段約52m／下段約12m。島の裏から洞窟へ入り、橋の西側へ戻る。
- 海岸、砂浜、烏帽子岩、道の駅の建物・看板・駐車場、上りランプ、ETC、高架道路、出口、街並み・江ノ電を接続。
- Blenderの湘南モデルとUnity用生成アセットを更新。橋の往復車線の中心を補正し、展望塔を道路から離した。地形に隠れた砂浜とETC文字の向きも画面確認後に修正。
- 車体選択の横スワイプを追加。40px以上かつ横移動が縦移動の1.3倍を超える操作で1車種切替。図鑑では従来の回転操作を維持。
- 湘南の実測記録キーを shonan-v4 に変更。旧v3ベストはアーカイブし、累計・アイテム・設定を保持。

## 高速道路の速度
共有ロジック RaceCore.cs の区間データで判定。通常道路0、洞窟1、高速道路本線2、ランプ3。
車種の最高速度 × 区間倍率（本線1.5、それ以外1） × 既存スペシャル倍率 × 防御成功時倍率 × スリップストリーム倍率の順で上限を計算する。車種カタログ自体は変更しない。

高速道路では加速の速度依存項と空気抵抗も上限に合わせ、上限だけが高く実際には加速できない状態を防ぐ。入口で現在速度を跳ね上げず、出口では倍率を直ちに1へ戻して滑らかに減速する。高さ差3m未満・舗装内という条件を満たさない場合は補正しない。CPUの曲率に応じた減速を維持する。

サーバーとUnityは同じ走行コードを使用。旧クライアントとの混在を防ぐためWebSocketは physics=10 を要求する。公開時はサーバーとWebBuildを同時に更新し、旧画面は再読み込みが必要。

## 主要ファイル
- Assets/Scripts/Core/RaceCore.cs: 経路、区間、最高速度、CPU速度、記録キー
- Art/Blender/build_assets.py / tracks.json / Models/course_shonan.blend: 編集可能モデルと景観生成
- Assets/Resources/Blender/course_shonan.bytes: 配信用メッシュ
- Assets/Resources/CoastSurface.shader / Assets/Scripts/RevisionSevenPresentation.cs: 洞窟の陰影・ヘッドライト
- Assets/Scripts/RevisionEightPresentation.cs: 新しい線路上の江ノ電
- Assets/WebGLTemplates/CoastRacer/club.js / racer.js / index.html: スワイプ、名称、記録移行、通信世代
- Server/Program.cs: コース表示・通信世代

## 検証
最終集計は REVISION_10_VALIDATION.json に保存する。
- 全10車種、CPUレベル1～5の8台混走を10レース。80台が2周完走、強制復帰0。
- 全10車種で高速道路内の実際の速度増加、出口の滑らかな減速、洞窟復帰、上下道路・路肩への誤適用防止を検査。
- 地形1,920地点、洞窟天井213地点、車両／カメラ高さの景観遮蔽レイ7,680本を検査。エラー0、洞窟の検査地点の最低天井高10.24m。
- 2クライアントのオンライン自動走行で2周完走、順位・実測タイム共有、再戦を確認。
- 既存ロジック回帰、30ミッション・v3保存移行、通信切断／再接続を確認。
- 最終版ブラウザー37項目・リリース8項目、PC GPUの昼夜32サンプルの負荷測定を完了。

[経路・高低差・コイン配置図](REVISION_10_ROUTE.svg)

## 確認の限界
PCの自動走行、Headless Edge、タッチの模擬入力を使用。iPhone・Galaxy・Pixelの実機、長時間発熱、本番8人の継続通信負荷は未検証。自動走行の完走は手動操作のすべての状況で不具合がない保証ではない。

## インストール・保存
新規インストールなし。既存のBlender 5.2.2、Unity 6000.6.2f1、.NET、Edge、Unity同梱Nodeを利用。
CarController.csの既存ユーザー変更は未編集・ビルドに未取り込み。空き容量1GiBの停止ガードを使用。

## 完了状態
最終Build **8d24358e4132** を WebBuild に反映済み。最終ビルドでブラウザー37項目、リリース8項目を確認し、実行時エラー0。旧世代のWebSocket接続はHTTP 426で拒否し、現行クライアントのオンライン完走は確認済み。
PC GPUでの負荷測定32サンプルを完了。新コース各区間は約29.7～30.1回／秒の描画更新。[測定結果と限界](REVISION_10_PERFORMANCE.md)を参照。

### 実行画面
- [車体選択](revision10/vehicle-selection.png)
- [島内山道](revision10/island-day.png)・[洞窟入口](revision10/cave-entry-day.png)・[洞窟内部](revision10/cave-day.png)・[洞窟出口](revision10/cave-exit-day.png)
- [海岸・砂浜](revision10/coast-day.png)・[道の駅付近](revision10/station-day.png)・[江ノ電並走区間](revision10/railway-day.png)
- [ETC](revision10/etc-day.png)・[高速道路（昼）](revision10/highway-day.png)・[高速道路（夜）](revision10/highway-night.png)

### 再検証
Unity同梱Nodeを node として以下を実行。サーバー検証はDevelopment環境のローカル接続を使用し、実際のAzure SQLを対象にしていない。

```text
dotnet run --project Tests/RevisionTen
dotnet run --project Tests/CoreTests.csproj
node Deployment/Test-Revision-Ten-Missions.mjs
node Deployment/Test-Revision-Seven-Network.mjs "ws://127.0.0.1:5113/ws?physics=10"
dotnet run --project Tests/Network -- "ws://127.0.0.1:5113/ws?physics=10" shonan
node Deployment/Test-Revision-Ten-Browser.mjs http://127.0.0.1:5113
node Deployment/Test-Revision-Ten-Performance.mjs docs/REVISION_10_PERFORMANCE.json native
node Deployment/Test-Release.mjs http://127.0.0.1:5113
```

検証の残件は上記の実機・継続負荷等。今回の実装範囲は完了。push・Azure公開は未実施。


## Azure公開（2026-09-27）
実装コミット `77ddaa2b34c5b5ab8ce0ebc1aba4e7333a8afa5e` をmainへpushし、[GitHub Actions 36316495397](https://github.com/ygtkd/car-race-game/actions/runs/36316495397)で公開完了。App Service F1・SQL無料枠の設定確認も成功。

公開Build `8d24358e4132`、health=ok／protocol=3／physics=10。公開URLで改修10ブラウザー37項目を通過し、起動、スワイプ、湘南レース開始、山道・洞窟・海岸・ETC・高速道路・江ノ電の昼夜を確認。実行時エラー0。湘南記録APIは初回25秒タイムアウト後、再試行でHTTP 200（新コース記録0件）。スマホ実機・本番8人の継続負荷は未確認。

[公開ゲーム](https://car-race-game-gmczdrgba5bph0gp.japanwest-01.azurewebsites.net/play/) ／ [公開検証記録](REVISION_10_PUBLIC_VALIDATION.json)。新規インストールなし。CarController.csの既存ユーザー変更と中間ビルド31d84f229152は未コミット。
