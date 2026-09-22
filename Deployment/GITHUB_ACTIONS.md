# GitHub ActionsでAzureに公開
現在はローカルにワークフローを用意した段階です。GitHubへのpush、Actions実行、Azure公開は未実施です。

## 公開先設定
GitHubリポジトリの Settings > Environments に production を作成し、Environment variables に以下を設定します。
- AZURE_CLIENT_ID: デプロイ用EntraアプリのクライアントID
- AZURE_TENANT_ID: テナントID
- AZURE_SUBSCRIPTION_ID: サブスクリプションID
- AZURE_RESOURCE_GROUP: リソースグループ名
- AZURE_APP_NAME: App Service名
- AZURE_SQL_SERVER: SQLサーバー名（.database.windows.netを除く）
- AZURE_SQL_DATABASE: データベース名（通常coastracer）

EntraアプリにはGitHubのOIDCフェデレーションを設定します。
Issuer: https://token.actions.githubusercontent.com
Subject: repo:OWNER/REPOSITORY:environment:production
Audience: api://AzureADTokenExchange
デプロイIDには対象App ServiceへのWebsite Contributorと、検査対象リソースへのReader権限が必要です。パスワードをリポジトリへ保存しないでください。

## Azureの前提
Deployment/AZURE.mdの構成とSQL初期化を先に完了します。
App Service: Linux F1、.NET 10、WebSockets有効、Always On無効。
SQL Database: 無料オファー有効、無料枠到達時AutoPause。
App ServiceのマネージドIDによるSQL接続、DBユーザーのSELECT/INSERT権限、schema.sql適用、必要な送信IPのファイアウォール許可も必要です。
このワークフローは既存リソースにデプロイします。リソース作成や有料プランへの変更は行いません。

## 実行
ソースとWebBuildをGitHubへpush後、Actions > Deploy racing game to Azure > Run workflow。
サーバーをビルドして無料枠を検査後、ZIPデプロイし、公開URLのhealthとゲームページを確認します。
SQLへの実際の記録保存とスマホ実機でのオンライン対戦は、公開後に別途確認します。

WebBuildは動作確認済みのUnity Webビルドです。Unityの更新後はBuilds/siteの内容でWebBuildを更新してください。
UnityライセンスをActionsに渡す必要はありません。GitHub Actionsの利用枠はAzure無料枠とは別です。
容量が1GiB未満になったらローカル作業を中断し、進捗を記録します。

参照:
https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions
https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-azure