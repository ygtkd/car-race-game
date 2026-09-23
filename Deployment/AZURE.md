# Azure公開準備
今回はAzureリソース作成・公開を実行していません。

## 構成
App Service F1（Linux/.NET 10）でゲーム配信・API・WebSocketを実行し、Azure SQL Database無料オファーで結果を保存します。他のクラウドサービスは使用しません。
main.bicepとコンパイル済みmain.jsonを用意しています。利用リージョン、無料枠適格性、.NET 10の実環境での提供状況は公開前に確認してください。

## 手順
1. Prepare-Publish.ps1でBuilds/coast-racer-appservice.zipを作成します。
2. 公開する段階でAzure CLIを導入し、az loginで認証します（現在CLIは未導入）。
3. main.jsonを対象リソースグループでwhat-ifし、確認後にデプロイします。パラメーターはlocation、appName、sqlServerName、databaseName、sqlAdminName、sqlAdminObjectIdです。SQL管理者はMicrosoft Entraユーザーを指定します。
4. SQLファイアウォールにApp Serviceの送信IPと初期設定用PCのIPだけを追加します。
5. SQL管理者としてServer/schema.sqlを実行し、App ServiceのマネージドIDをDBユーザーとして作成します。dbo.RaceResultsへのSELECT、INSERTだけを付与してください。
6. Deploy-Azure.ps1にSubscriptionId、ResourceGroup、AppName、SqlServerを指定して無料設定を読み取り検査します。
7. 実際に公開する際だけ、同じコマンドに-Publishを追加します。
8. HTTPSで2端末から対戦し、SQL結果保存、休止からの復帰、切断・再接続を確認します。

## 無料枠
F1、Always On無効、SQL useFreeLimit=true、freeLimitExhaustionBehavior=AutoPauseを固定しています。無料枠を使い切った後の有料継続は設定しません。
初期設定は全体8接続。ホストは人間＋CPUで最大8台に設定できます。FreeプランはCPU・接続数に制限があり、常時稼働は保証できません。
SQL接続はマネージドID認証で、Pooling=Falseを指定しています。秘密鍵をWebクライアントへ埋め込みません。
SQLの初期設定権限をアプリ実行IDに与える必要はありません。

## 本番とローカルの違い
本番はConnectionStrings__RacerSql必須です。開発時のみServer/App_Data/results.jsonへ保存します。
レースIDとプレイヤーIDを主キーとし、トランザクションで重複登録を防ぎます。
進行中のルームはメモリ上です。再起動後は利用者へ再参加を案内します。
APIは/api/records/ridge、/api/records/suzukaです。
無料枠の実適格性、Azureへの実接続、インターネット対戦はまだ検証していません。

## 公式資料
https://learn.microsoft.com/azure/azure-resource-manager/management/azure-subscription-service-limits#app-service-limits
https://learn.microsoft.com/azure/azure-sql/database/free-offer
https://learn.microsoft.com/azure/azure-sql/database/free-offer-faq
https://learn.microsoft.com/azure/templates/microsoft.sql/2023-08-01/servers/databases
https://learn.microsoft.com/azure/app-service/tutorial-dotnetcore-sqldb-app
