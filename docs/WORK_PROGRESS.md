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
