# インストール・依存関係の記録

今回の改修でOS全体へ新しいアプリケーションはインストールしていません。追加したものは以下のプロジェクト内に限定しています。

| 区分 | 名前・バージョン | 用途 | 導入方法・範囲 | 配布元 |
|---|---|---|---|---|
| 追加 | Microsoft.Data.SqlClient 6.1.4 | Azure SQLへの接続 | dotnet restore Server/CoastRacer.Server.csproj --packages .packages / プロジェクト内 .packages | https://www.nuget.org/packages/Microsoft.Data.SqlClient/6.1.4 |
| 既存を使用 | .NET SDK 10.0.401 | 対戦サーバー・テスト | 既存環境を使用 | https://dotnet.microsoft.com/ |
| 既存を使用 | Unity 6000.6.2f1 / Web Build Support | 3DゲームとWebビルド | 既存環境を使用 | https://unity.com/ |
| 既存を使用 | Unity同梱 Node.js 22.16.0 | ブラウザと通信検証 | Unityのインストール先を直接使用 | Unity Web Build Support |
| 既存を使用 | Microsoft Edge | ローカルWeb動作検証 | 既存環境を使用 | https://www.microsoft.com/edge |

導入日: 2026-09-22。SqlClientの間接依存関係と固定バージョンは Server/packages.lock.json に記録しています。
恒久的なPATH・レジストリ・システム環境変数の変更はありません。
ローカル検証サーバーの子プロセスには ASPNETCORE_ENVIRONMENT=Development を指定します。
Unityビルドの子プロセスには DOTNET_PROCESSOR_COUNT=2、EMCC_CORES=2 を指定します。これらはセッション限定です。

戻すにはServer/CoastRacer.Server.csprojのPackageReferenceを除去して復元し、他で使用していないことを確認して .packages 内の該当パッケージを削除します。SQL機能は利用できなくなります。
今回作成したバックアップは Backups/BeforeProRacer-*.zip です。ゲーム全体の変更を戻す場合はこちらを使用してください。

## 追加: Bicep CLI 0.47.16

- 用途: Azure構成のローカルコンパイル・スキーマ検証。
- 配布元: https://github.com/Azure/bicep/releases/download/v0.47.16/bicep-win-x64.exe
- 導入方法: 公式GitHub ReleasesからInvoke-WebRequestで取得。
- 導入先: Tools/Bicep/bicep.exe（プロジェクト内のみ）。PATH変更なし。
- 導入日: 2026-09-22。
- SHA256: 3F343AB1CE41FEAC156464ADEE3DC499CB6C197366FC731AED276192011D867C
- 使用: Tools/Bicep/bicep.exe build Deployment/main.bicep --outfile Deployment/main.json
- 取り消し: 他で使用していないことを確認し Tools/Bicep/bicep.exe を削除。生成済みARM JSONは実行時にBicepを必要としません。
- ダウンロードは完了し、構成のコンパイルにも成功しました。

## メニュー・左右タッチUI改修
追加インストール・新規依存関係はありません。既存のUnity、Node.js、Edge、.NETのみを使用しました。

## Club拡張
新規アプリ・依存パッケージのインストールはありません。音源は既存Node.jsで生成しました。容量不足を避けるため、Unityビルド時のみDOTNET_PROCESSOR_COUNT=1、DOTNET_gcServer=0、EMCC_CORES=1、BEE_BUILD_THREADS=1、BINARYEN_CORES=1を指定します。恒久的なシステム設定変更はありません。Deployment/Build-Guarded.ps1は残り1GiBで停止します。

GitHub Actions公開準備: 新規ローカルツールのインストールなし。既存.NET SDKによる復元・公開のみ。
