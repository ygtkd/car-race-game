最終更新: 2026-09-22 19:37:00

# 現在の状態: ローカル改修と検証完了

再開後、最新ソースのWebビルド、車種バランス再計測、実通信、梱包版のブラウザ検証を完了。
現在のプレビュー: http://localhost:5080/play/ （Builds/server、開発モード、起動PID 26140）。
Azureへの実公開とGitHub Actions設定は未実施。実機スマホ、Azure SQL接続、実機での音の聴感評価も未実施。

## 最終成果物
- Unityビルド: Logs/club-final-build.log（成功・終了コード0）
- 公開用ZIP: Builds/coast-racer-appservice.zip（約19.1MB）
- ブラウザ: Logs/club-browser-checks.json（55項目、実行時エラー0）
- 実通信: Logs/protocol-checks.json（13項目）
- 両コースの2人完走・同一順位とタイム・再戦: Logs/club-online-fullrace.log（成功）
- 反復走行: Logs/vehicle-balance.json（16レース、64台分全完走）
- 音源・DOM検証: Logs/club-assets-checks.json（170項目）
- 仕様、車種性能、バランス実測、素材の出所: docs/CLUB_EXPANSION.md
- スクリーンショット: Logs/club-menu.png、club-settings.png、club-vehicles.png、club-gallery.png、club-gallery-badge.png、club-records.png、club-driving.png、club-confirm.png、club-online.png
- インストール: 新規追加なし。INSTALLATIONS.mdに記録。

## 容量条件
ユーザーから、容量がいっぱいになったら中断するよう指示あり。
Deployment/Build-Guarded.ps1は残り1GiB未満で対象ビルドを停止し、Logs/capacity-stop.flagとこの文書に記録する。
再開後の最終空き容量は約7.8GiB。今回の再開後は容量停止条件に達しなかった。
将来の追加作業も空き容量を確認してから開始し、ユーザーの中断指示時は進捗を保存して停止する。

---
以下は中断時点と再開後の作業履歴。上記の最終状態を優先する。
最終更新: 2026-09-22 18:51:58

# 拡張改修の進捗 — 再開・最終検証中

状態: 容量確保後、ユーザーの再開指示により作業を再開。中断時点の履歴を以下に保持。
現在は容量監視付きで最終検証中。残り1GiB未満で進捗を保存して停止する。

## 対象
直前の改修プロンプト全体。BGM音量と効果音音量の個別調整・保存を含む。
Unity製スマホWebゲーム。Azure App Service Free / Azure SQL無料枠のみ。

## 完了した編集（全体の動作検証は未完了）
- Assets/Scripts/Core/RaceFeatures.cs: 4車種の性能、共有3コイン、取得から2秒復活、重量を考慮した接触、4種類のスペシャル、ボットのコイン追従と発動判断。
- RaceCore.cs: 車種性能反映、走行距離・ゲージ（1.2周で満タン、コイン0.5周分）、復帰による充填防止。
- Server/Program.cs: 車種・装飾・名前、全員の読み込み完了待ち、共有状態、サーバーによる取得・発動・衝突判定。
- Assets/Scripts/RacePresentation.cs: 車種別の形状、展示カメラ、コイン表示、名前表示用スクリーン座標。
- CoastRacer.cs / CoastSurface.shader: 表示・走行統合、金属反射と路面の粒状感。ローディング後にbeginでソロ開始。
- WebGLテンプレート club.js / club.css / racer.js / index.html: 大きいタイトル、車体選択、図鑑、記録、名前とバッジのローカル保存、退出確認、円形速度メーター、遷移演出、音量設定を追加。
- audio.jsとaudio/*.wav: メニュー40秒、レース30秒、結果約18.46秒、操作・コイン・スペシャル音を生成。BGM/SE個別音量、ミュート、保存を実装。
- Deployment/Generate-Audio.mjs: オリジナル音源を再生成できるスクリプト。
- Tests/ExpansionTests.cs: コイン、衝突、スペシャル、64台分（16レース）の反復走行検証。
- Deployment/Test-Network.mjs / Tests/Network/Program.cs: 新プロトコルに対応する編集済み。まだ実行していない。

## 検証済み
- 既存の共通走行テストは成功。
- 共有コインの単独取得、41.67%充填、2秒復活、無充填の発動拒否、シールドの妨害耐性、重量差の接触、復帰で充填しないことを確認。
- 初回の反復走行でボットのコイン追従による逸脱を検出し修正。
- Logs/vehicle-balance.json の最新完了計測では64台分すべて完走。両コース、全開始順位、スペシャル有無を計測。
- その計測後、APEXの加速・グリップ、SWIFTの加速・グリップ、VORTEXのグリップ、ブースト中のボット目標速度を調整した。最新ソースに対する再計測は未実施。
- Web JS構文チェックは通過。Unity C#コンパイルは通ったが最終Webビルドは未完了。

## ビルド障害・停止
- Cドライブの空きが約200MBになり、最初のUnity処理がクラッシュ。
- 以下のプロジェクト内の再生成できるキャッシュを削除済み: Library、Temp/CoastRacerBrowserCheck、Temp/ProRacerBrowserCheck、Temp/ServerVerify、Builds/server、Server/bin。
- ソースと改修前バックアップは保持。
- 空きを一時約880MBまで確保したが、ビルド生成物で再び減少。
- 次の試行は並列コンパイルのMemoryError/ページング不足で失敗。
- BEE_BUILD_THREADS=1によりコンパイルを直列化したがwasm-optが失敗。
- BINARYEN_CORES=1も追加した最新試行（Logs/club-build-lowmemory.log）はビルド中にユーザー指示で停止。
- 停止直前: 空き約239MB、物理空き約164MB、仮想空き約79MB。
- 当該Unityビルドのプロセスと子プロセスを停止。以前からのプレビューサーバー（PID 24932、旧版）やユーザーのブラウザは停止対象にしていない。

## 次回の再開手順
1. ユーザーから再開指示を受け、十分なディスク空き容量とメモリを確認する。
2. ソースをBuilds/Unity6Validation/Assetsへ同期する。正本はルートAssets、Server、Deployment。
3. 最終調整後の dotnet run --project Tests/CoreTests.csproj --no-restore を実行し、完走率と車種差を確認する。
4. サーバーを再ビルドして別ポートで実通信テストを行う。現行5080は旧版の可能性があるため、そのまま新版検証に使わない。
5. UnityビルドはDOTNET_PROCESSOR_COUNT=1、DOTNET_gcServer=0、EMCC_CORES=1、BEE_BUILD_THREADS=1、BINARYEN_CORES=1、-job-worker-count 1で再試行する。設定はプロセス限定。
6. Unity Webビルド成功後にのみ、検証プロジェクトのBuilds/siteをルートBuilds/siteへ反映。
7. ブラウザ検証スクリプトは新しい車体選択、ローディング、確認ダイアログ等に合わせて更新する必要がある。現在のSmoke-Web.mjsは旧UI用。
8. 実際の画面表示、360度操作、音声再生、音量保存、プロフィール・記録・バッジ、マルチタッチ、全員ロード待ちを確認し、必要な修正を行う。
9. 新しいスクリーンショット、文書、INSTALLATIONS.md、公開ZIPを更新する。現時点では旧公開ZIPのまま。

## 保持している記録
- Backups/Before-Club-Expansion-20260922-181900.zip: 改修前バックアップ。
- Logs/vehicle-balance-before.json / vehicle-balance-pass2.json / vehicle-balance.json: 調整の比較データ。
- Logs/club-first-build.log / club-build-retry.log / club-build-serial.log / club-build-lowmemory.log: ビルド履歴。
- Assets/WebGLTemplates/CoastRacer/audio/manifest.json: 音源の生成方法、サイズ、長さ。
- 新規インストールはなし。既存Unity・Node.js・.NETを使用。ビルド並列数などの環境変数はプロセス限定。

## 未完了
最終ビルド、最新バランス計測、実通信とブラウザ検証、視覚・聴感の確認、実機スマホ、スクリーンショット、最終文書、公開ZIP、Azure公開。
この拡張を完成済みとして扱わない。
停止確認: 対象Unityビルド残存数 0。停止対象PID: 。

再開: ユーザーの再開指示を受領。空き約8.2GiBを確認。容量不足時は停止する条件を追加。UnityはDeployment/Build-Guarded.ps1で残り1GiB未満なら自動停止し、Logs/capacity-stop.flagを記録する。

再開後: Unityビルド成功（club-resumed-build.log）。最新バランス64台すべて完走、サーバービルド警告0。実通信プロトコルテスト13項目成功。新UIブラウザ初回51項目成功。起動ロゴが展示画面に残る視覚問題を修正し、club-showroom-build.logで再ビルド中。オンライン完走試験は別サーバー5084、Logs/club-online-fullrace.logに進行中。

## GitHub Actions公開準備
- .github/workflows/deploy-azure.yml を追加。手動実行、production環境、OIDC認証、F1/SQL無料オファー検査、ZIP公開、HTTP確認。
- WebBuildに検証済みUnity Webビルド26ファイルを収録。
- Deployment/Prepare-CI.ps1のローカル実行成功（restore/publish/ZIP）。
- Deployment/GITHUB_ACTIONS.mdに公開先変数・認証・SQL初期化の前提を記録。
- GitHubリポジトリURLとAzure公開先情報が未提示。GitHubへのpush、Actions実行、Azure公開は未実施。
- 新規ツールのインストールなし。Cドライブ空き約7.65GiB。

## 2026-09-22 GitHub Actions公開実行
- originをhttps://github.com/ygtkd/car-race-game.gitに変更し、mainの5955df4をpush成功。
- production環境のAZURE_CLIENT_ID/AZURE_TENANT_ID末尾の改行を除去。
- Actions run 35739721017: 設定検査・公開パッケージビルド成功、Azure login失敗。
- エラー: No subscriptions found。デプロイ用Entraアプリ84d81fa2-07e3-4089-8e25-cde24fd88192のAzure RBAC/テナントとsubscription割当を要確認。
- 対象: takeda-resource / car-race-game / takeda-server1 / takeda-free-database。
- AzureへのZIP送信と公開確認は未実行。CarController.csのユーザー未コミット変更は保持。
- https://github.com/ygtkd/car-race-game/actions/runs/35739721017

## 2026-09-23 Azure公開結果
- Azure RBAC修正後のOIDC認証成功。F1とSQL無料オファーAutoPauseの検査成功。
- 実行設定(.NET10/WebSocket/AlwaysOn/startup)を自動適用。システム割当マネージドIDを有効化。
- コミット0e2da83、Actions 35744543578全ステップ成功。
- 公開URL: https://car-race-game-gmczdrgba5bph0gp.japanwest-01.azurewebsites.net/play/
- /health protocol3正常、/play/ HTTP200。初回Azure起動追跡タイムアウトは実HTTP起動確認へ変更して解消。
- /api/records/ridge はID有効化後もHTTP503。SQLファイアウォール、Entra DBユーザー、schemaおよび接続設定の検証が残る。DB保存成功は未確認。
- CarController.csの未コミット変更は保持。追加ツールインストールなし。

## 作業再開後の確認（2026-09-23）
- SQL送信IP13件登録はActions 35749119389で成功済み。
- 再検証: /health HTTP200 protocol3、ridge/suzukaの記録APIは双方HTTP503。
- 空き容量約4.5GiB、容量による停止なし。
- Deployment/Initialize-Production-Sql.sqlを確認済み。既存データ保持でテーブル作成とApp Service DBユーザーSELECT/INSERTを設定する。
- DB内管理権限はGitHub実行IDにない。Microsoft Entra SQL管理者による初期化SQL実行結果の確認が必要。503原因がDB初期化不足かどうかはまだ未確定。

## 2026-09-23 SQL記録API復旧確認
- ユーザーが初期化SQLを実行成功。
- 503原因をサーバーの安全な診断ログで特定: SQL40615、実送信元20.78.155.116が初回outboundIpAddresses13件に含まれず。
- possibleOutboundIpAddressesと実送信元を個別IP規則へ追加。Actions35751750423成功。
- 公開検証: /api/records/ridge HTTP200 []、/api/records/suzuka HTTP200 []。SQL読み取り復旧。
- サーバー修正はビルド成功、デプロイ35751162794成功。秘密情報は診断ログに含めない。
- 実レース終了後の本番SQL書き込みは未検証。未コミットCarController.cs変更は保持。

螳ｹ驥冗屮隕悶↓繧医ｋ荳ｭ譁ｭ: 2026-09-23T02:19:34 Stopped build 18240 below free-space threshold. Log: mobile-revision-build.log

## モバイル改修・容量不足による中断（2026-09-23）
ユーザー指示: docs/COAST_RACER_MOBILE_REVISION_PROMPT.mdを実行しpushまで。追加指示で旧3周記録は削除可。
Unityビルド中に1GiB閾値を下回りBuild-Guarded.ps1が停止。最後の空き486395904 bytes。Logs/capacity-stop.flagあり。容量確保とユーザーの再開指示まで実装・ビルド・pushを中断。

ローカル変更（未コミット・未push）:
- Simulation.Laps=2、自動復帰3秒と範囲復帰時キャンセル、復帰後2秒接触保護。手動recover入力をWeb/Unity/serverから削除。
- 車種データ8種、固有スペシャル、新規MobilePresentation.csの専用モデル・追加景観。
- 2周API保存キーをtrack-2lapへ分離。旧ローカル履歴をprofile version4でリセット、名前/バッジ/音量保持。
- mobile.js: 選択・長押し・ダブルクリック防止、角の戻るボタン、信号とブザー、エンジン音。メーター半径とタッチ範囲CSS。
- 156BPMのオリジナル合成レースBGM、PNGホーム画面アイコン/manifest。
- SQLの旧3周削除はDeployment/Remove-Legacy-Three-Lap-Records.sqlを用意。実DB削除は未実施。

検証済み:
- dotnet server Release build成功、Web JS --check成功。
- CoreTests: 自動復帰3秒・解除・進行保持、2周終了成功。
- 8車種128サンプルすべて完走。Logs/mobile-core-tests.log、Logs/vehicle-balance.json。平均タイムはAPEX239.7〜kebab244.1秒（両コース混合）。さらなる特殊技差異評価は未完。
- Unity builds/Unity6Validationへソースをコピー済み。Logs/mobile-revision-build.logは容量による中断、WebBuildはまだ旧版。

再開時に必要な作業:
1. 空き容量を十分確保して確認後、容量停止フラグを解除する。前回は4.2GiBから開始して不足したため、8GiB以上を目安とする。
2. racer.js localize()がdocument.titleを従来表記へ戻す箇所をCOAST RACER固定にする。
3. mobile.jsのハンドル見た目・CSS #wheelFace位置と2倍判定、歯車と戻るボタン、8種選択の収まりをブラウザで検証し修正。
4. 新特殊技は既存効果の組合せ。車種ごとの違いとバランスを確認し調整する。日本語車種表示、旧ラップ数ハードコード残存を確認。
5. 全ソースを検証用Unityコピーへ再同期し容量監視ビルド。出力Builds/Unity6Validation/Builds/siteをBuilds/siteとWebBuildへ反映しBrotli再生成。旧ビルドと新serverを混在させない。
6. Deployment/Test-Network.mjsの旧recover期待を自動復帰/手動入力拒否へ更新、ブラウザSmokeテストの4車種/旧UI期待を変更。端末サイズ検証と視覚確認、再接続/入力解除確認。
7. 実機は未接続。iPhone17 Safari、Galaxy S26 Ultra Chrome/Samsung Internet、Google Pixel11 Chromeは未検証。公式候補 https://www.samsung.com/latin/smartphones/galaxy-s26-ultra/specs/ と https://store.google.com/us/category/phones?hl=en-US 。エミュレーションを実機検証と呼ばない。
8. README/INSTALLATIONS/VALIDATION更新、git diff確認、ユーザー既存CarController.csを除外して改修をcommit/push。Azure再デプロイは今回依頼に含めない。

注意: Assets/Scripts/CarController.csはユーザー既存の未コミット変更であり触れていない。WORK_PROGRESS.mdも以前から未コミット。新規インストールなし。描画アイコンはSystem.Drawingによるオリジナル図形、BGMは既存生成器で作成。

## 2026-09-23 モバイル改修の検証結果
- Unity 6000.6.2f1のWebGLビルド成功。旧Editor削除後も必要なEditor・WebGLモジュールは残存。
- 共通走行テスト成功。8車種・両コース・開始位置・スペシャル有無を含む32レース、128台分で全車完走。
- ブラウザー73項目、通信13項目、音源・UI静的検査168項目が成功。ブラウザー実行時エラー0。
- 旧プロファイルの記録リセット、名前・バッジ・音量維持、スタート信号を検証。
- 2クライアントが両コースを2周完走し、順位・タイム一致と再戦を確認。
- 852×393、915×412、900×412のEdgeエミュレーションでレイアウト確認。実機iPhone・Galaxy・Pixel、および実機音声・ホーム画面追加は未検証。
- 新規インストールなし。詳細は docs/MOBILE_REVISION.md。以前の3周・4車種の検証記載は過去バージョンの結果。
## 2026-09-23 レース進行改修
実装とWebGLビルド完了。通信異常時の切断処理を追加。最終ブラウザー検証後にpush・Azure公開を行う（ユーザー承認済み）。詳細: docs/RACE_FLOW_REVISION.md。CarController.csの既存変更は今回のコミットに含めない。

## レース進行改修の最終検証
ブラウザー84項目（実行時エラー0）、ルーム・8台・放置接続13項目、通信プロトコル13項目に合格。WebGLビルド成功。旧版の2接続・手動開始・3秒復帰の仕様は今回更新。詳細: docs/RACE_FLOW_REVISION.md。

## 2026-09-23 改修プロンプト2のローカル実装
- docs/COAST_RACER_REVISION_PROMPT_2.mdに基づいて実装。開始文字の抑止、芝生だけの5秒警告、最後の道路中心・区間・向きへの復帰、完走表示の補間遅延対策、CPU推定、オンライン終了・復旧、バッジ画像とレース別管理、鈴鹿遠景を更新。
- 最終WebGLは65bd238ef35d。Unity/JS/CSS/音声を版別ディレクトリへまとめ、設定にBuild表示とメニューでの更新操作を追加。WebBuild反映済み。
- 共通走行、ブラウザー88項目、通信13項目、両コース実通信完走・再戦、最終版8項目を検証。詳細と未確認事項はdocs/REVISION_2_IMPLEMENTATION.md。
- push・Azure公開は未実施。実機のホーム画面起動、音の聴感、Azure無料枠での8人継続負荷は未検証。新規インストールなし。
- Assets/Scripts/CarController.csの既存変更は変更・ステージしていない。今回はコミットもしていない。
- 容量は約12.9GiB空き。1GiB未満なら大きな書き込みを停止する条件を維持。

最終確認: 最終版で入力による実走行2周完走→初完走バッジ画像→メニュー復帰の検証成功。公開パッケージ再作成も成功。残作業は公開と実機・Azure負荷検証。詳細はREVISION_2_IMPLEMENTATION.md。

## 改修プロンプト3・再開後の進捗
実装と最終WebGLビルド8d6b44e43f67をWebBuildへ反映。操作68項目・演出21項目・既存回帰118項目・通信13項目成功。公開用ZIP作成成功。現在は実走行での特殊技発動中の停止復帰を追加検証中。詳細はdocs/REVISION_3_IMPLEMENTATION.md。push・公開未実施。新規インストールなし。容量約10.9GiB空き。

改修3の最終検証完了: 8d6b44e43f67で実走行によるゲージ蓄積→特殊技発動→停止中の残時間保持→再開・効果終了→再充填→2周完走→バッジ→メニュー復帰が成功。実行時エラー0。実装・ビルド・ローカル検証・公開用ZIP完了。push・Azure公開・実機確認は未実施。空き容量約10.6GiB。証拠と未確認範囲はdocs/REVISION_3_IMPLEMENTATION.md。

## 改修2・3 公開完了
mainへf209459をpush。GitHub Actions 35839454546 success。Azure公開Buildは8d6b44e43f67。health=ok、公開ブラウザー8項目成功・実行時エラー0。実機・8人継続負荷は未確認。詳細はdocs/REVISION_3_IMPLEMENTATION.md。

## 改修4 進捗
プロンプト4作成・実装・ビルドc99d23daba52まで完了。操作77項目、交差上下10地点、共通走行、鈴鹿オンライン2周と再戦成功。鈴鹿ソロのブラウザー実走行と回帰再確認を実施中。公開用ZIP作成中、まだpush・公開していない。CarController.csは除外。詳細docs/REVISION_4_IMPLEMENTATION.md。
改修4のソロ鈴鹿2周完走・記録・バッジ・メニュー復帰まで成功。公開ZIPも作成成功。既存回帰を単独再実行中。その後、版別パッケージ起動確認→commit/push→Actions公開→公開URL確認を行う。新規インストールなし。
改修4のローカル検証完了: 既存回帰118項目・版別起動8項目成功、エラー0。Build c99d23daba52をpush・Azure公開へ進める。

## 改修4 公開完了（2026-09-23）
mainへ0c6d1beをpush。Actions 35855242252 success。公開Build c99d23daba52、health=ok、公開ブラウザー8項目成功・エラー0、記録API再確認HTTP 200（初回503）。ソロとオンラインの鈴鹿2周完走確認済み。実機・8人継続負荷は未確認。新規インストールなし。空き容量約8.5GiB。詳細はdocs/REVISION_4_IMPLEMENTATION.md。
