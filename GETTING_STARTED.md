# Getting Started - プロジェクト起動ガイド

このガイドでは、MultithreadDesignPattern の各プロジェクトをローカル環境で起動する方法を説明します。

## 前提条件

### 必須
- **.NET 8 SDK** - [ダウンロード](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Windows** (ImageProcessor.Admin の実行に必要)

### オプション
- **Visual Studio 2022** - WPF アプリのデバッグに推奨
- **Visual Studio Code** - Worker Services のデバッグに推奨
- **Azure Storage Emulator** または **Azurite** - ローカル開発用

## プロジェクト構成

```
MultithreadDesignPattern/
├── ImageSearch/                    # データモデル (net8.0)
├── ImageProcessor.Core/            # 共通ロジック (net8.0)
├── ImageProcessor.SimpleWorker/    # シングルスレッド Worker (net8.0)
├── ImageProcessor.MultithreadWorker/ # マルチスレッド Worker (net8.0)
├── ImageProcessor.SearchWorker/    # 画像検索 Worker (net8.0)
├── ImageProcessor.Admin/           # WPF 管理ツール (net8.0-windows)
└── ImageProcessor.Web/             # Web UI (.NET Framework 4.5) ⚠️ 未移行
```

## クイックスタート

### 1. リポジトリのクローン

```bash
git clone <your-repo-url>
cd MultithreadDesignPattern
```

### 2. 依存関係の復元

```bash
dotnet restore
```

### 3. ビルド

```bash
# 全プロジェクトをビルド
dotnet build

# または個別にビルド
dotnet build ImageProcessor.Core
dotnet build ImageProcessor.Admin
```

## 各プロジェクトの起動方法

### ImageProcessor.Admin (WPF アプリ)

**Windows でのみ動作します。**

#### 方法1: Visual Studio で実行

```powershell
# ソリューションを開く
start MultithreadDesignPattern.sln
```

Visual Studio で:
1. ImageProcessor.Admin をスタートアッププロジェクトに設定
2. F5 キーで実行

#### 方法2: コマンドラインで実行

```powershell
dotnet run --project ImageProcessor.Admin
```

#### 起動後の確認

- ウィンドウが表示される
- 左上に赤い接続状態インジケーター
- デバッグモードの場合、サンプルキーワードが表示される

#### トラブルシューティング

**ウィンドウが表示されない場合**:
1. グローバル例外ハンドラがエラーダイアログを表示するはず
2. イベントビューアでエラーログを確認: `eventvwr.msc`
3. Visual Studio でデバッグ実行して例外発生箇所を特定

**設定の変更**:
`ImageProcessor.Admin/App.config` を編集:

```xml
<setting name="WebSiteUrl" serializeAs="String">
  <value>http://localhost:53344/</value>
</setting>
```

### ImageProcessor.SimpleWorker (Worker Service)

#### 起動

```bash
dotnet run --project ImageProcessor.SimpleWorker
```

#### 設定

`ImageProcessor.SimpleWorker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  },
  "Worker": {
    "QueueName": "processing-queue",
    "PollingInterval": 1000
  }
}
```

#### 動作確認

ログに以下が表示されれば正常:
```
info: ImageProcessor.SimpleWorker.Worker[0]
      Worker started at: 2026-02-03 XX:XX:XX +09:00
```

### ImageProcessor.MultithreadWorker (Worker Service)

#### 起動

```bash
dotnet run --project ImageProcessor.MultithreadWorker
```

#### 設定

`ImageProcessor.MultithreadWorker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  },
  "Worker": {
    "QueueName": "processing-queue",
    "ChannelCapacity": 100,
    "ConsumerThreadCount": 4
  }
}
```

### ImageProcessor.SearchWorker (Worker Service)

#### 起動

```bash
dotnet run --project ImageProcessor.SearchWorker
```

#### 設定

画像検索機能を使用する場合は Bing Search API キーが必要です。

`ImageProcessor.SearchWorker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  },
  "BingSearch": {
    "ApiKey": "your-api-key-here",
    "Endpoint": "https://api.bing.microsoft.com/v7.0/images/search"
  }
}
```

### ImageProcessor.Web (未移行)

⚠️ このプロジェクトはまだ .NET Framework 4.5 です。

#### Windows での起動

```powershell
# IIS Express で起動
start ImageProcessor.Web/ImageProcessor.Web.csproj
```

または Visual Studio で:
1. ImageProcessor.Web をスタートアッププロジェクトに設定
2. F5 キーで実行

## ローカル開発環境のセットアップ

### Azure Storage Emulator のセットアップ

Worker Services は Azure Storage を使用します。ローカル開発には Azurite を推奨します。

#### Azurite のインストール (推奨)

```bash
# npm でインストール
npm install -g azurite

# 起動
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

または Docker で:

```bash
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

#### 接続文字列

```
UseDevelopmentStorage=true
```

または明示的に:

```
DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;
```

### 必要なキューとコンテナーの作成

```bash
# Azure Storage Explorer を使用するか、以下のコードで作成

# .NET Interactive または C# スクリプトで
#r "nuget: Azure.Storage.Queues, 12.17.0"
#r "nuget: Azure.Storage.Blobs, 12.19.0"

using Azure.Storage.Queues;
using Azure.Storage.Blobs;

var connectionString = "UseDevelopmentStorage=true";

// Queue の作成
var queueClient = new QueueClient(connectionString, "processing-queue");
await queueClient.CreateIfNotExistsAsync();

// Blob Container の作成
var blobServiceClient = new BlobServiceClient(connectionString);
var containerClient = blobServiceClient.GetBlobContainerClient("original-images");
await containerClient.CreateIfNotExistsAsync();
```

## 統合動作確認

### シナリオ1: Admin → Web → Worker (完全なワークフロー)

⚠️ ImageProcessor.Web が .NET 8 に移行されるまで、完全なワークフローは動作しません。

### シナリオ2: Worker Services の単独テスト

1. Azurite を起動
2. Queue にテストメッセージを追加
3. Worker を起動して処理を確認

```csharp
// テストメッセージの送信例
var queueClient = new QueueClient("UseDevelopmentStorage=true", "processing-queue");
await queueClient.SendMessageAsync("test-message");
```

### シナリオ3: Admin の単独テスト

1. ImageProcessor.Admin を起動
2. デバッグモードでサンプルキーワードが表示されることを確認
3. UI の動作を確認

## 開発時のヒント

### Visual Studio Code での開発

#### 推奨拡張機能
- C# Dev Kit
- .NET Extension Pack
- Azure Account

#### tasks.json と launch.json

`.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/ImageProcessor.SimpleWorker/ImageProcessor.SimpleWorker.csproj"
      ]
    }
  ]
}
```

### ホットリロード

Worker Services でホットリロードを有効にする:

```bash
dotnet watch run --project ImageProcessor.SimpleWorker
```

### ログレベルの変更

`appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

## トラブルシューティング

### よくある問題

#### 1. "プロジェクトが見つからない"

```bash
# ソリューションの復元
dotnet restore
```

#### 2. "Azure Storage に接続できない"

```bash
# Azurite が起動しているか確認
netstat -an | findstr "10000 10001 10002"

# Azurite を再起動
azurite --silent
```

#### 3. "ポートが既に使用されている"

```bash
# Windows でポート使用状況を確認
netstat -ano | findstr ":53344"

# プロセスを終了
taskkill /PID <PID> /F
```

#### 4. ImageProcessor.Admin が起動しない

- Visual Studio でデバッグ実行
- イベントビューア (`eventvwr.msc`) でエラーログを確認
- グローバル例外ハンドラがエラーを表示するはず

### ログの確認

```bash
# Worker Services のログ
dotnet run --project ImageProcessor.SimpleWorker

# 詳細ログ
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project ImageProcessor.SimpleWorker
```

## 次のステップ

1. **ImageProcessor.Web の移行**: ASP.NET Core への移行を完了
2. **統合テスト**: 全コンポーネントを連携させて動作確認
3. **デプロイ**: Azure へのデプロイ戦略を検討

## 参考資料

- [.NET 8 ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-8)
- [Azurite ドキュメント](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-use-azurite)
- [Worker Service ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/workers)
