# SQL接続修復（2026-09-23）
診断: publicNetworkAccess=Disabled、firewall規則0件。接続先・DB名・マネージドID認証は正しい。
App Service identity: d6095586-f63d-4a33-a477-cc2ab61f1044
GitHub用アプリのidentityとは別。DBアクセス権を付けるのはApp Serviceのcar-race-game。

Azureポータル > SQLサーバー takeda-server1 > ネットワークで、パブリックアクセスを「選択されたネットワーク」にする。
以下の各IPを開始IPと終了IPに同じ値で登録する。任意の名前（app-01等）を使用。
20.210.141.205
20.210.139.103
20.210.169.195
20.210.136.75
20.210.138.244
20.210.140.203
20.78.154.251
20.78.155.2
20.78.155.5
20.78.155.21
20.78.155.33
20.78.155.42
40.74.100.138
「現在のクライアントIPを追加」も初期化用に実行する。Azure全サービス許可は不要。
保存後、DB takeda-free-database > クエリエディターでMicrosoft Entra管理者としてログイン。
Deployment/Initialize-Production-Sql.sqlを実行する（既存テーブルとデータは保持）。
SQL無料オファーとAutoPauseは変更しない。
確認URL: https://car-race-game-gmczdrgba5bph0gp.japanwest-01.azurewebsites.net/api/records/ridge
HTTP200かつJSON配列が正常。空配列[]も正常。
現在のGitHubデプロイIDにはSQL変更権限もDB管理権限もないため、管理者による上記操作が必要。