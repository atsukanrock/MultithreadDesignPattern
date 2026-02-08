# 統合テストガイド

このガイドでは、MultithreadDesignPattern の全コンポーネントを連携させて動作確認を行う方法を説明します。

## システムアーキテクチャ

```
┌─────────────────────┐
│ ImageProcessor.Web  │ ← ユーザーがキーワードを入力
│ (ASP.NET Core)      │
└──────────┬──────────┘
           │ SignalR (WebSocket)
           ↓
┌─────────────────────┐
│ImageProcessor.Admin │ ← SignalR でキーワードを受信
│ (WPF)               │
└──────────┬──────────┘
           │ Azure Queue にメッセージ送信
           ↓
┌─────────────────────────────────┐
│   Azure Storage Queue           │
│   (processing-queue)            │
└──────────┬──────────────────────┘
           │
           ↓
┌─────────────────────┐
│ SearchWorker        │ ← キーワードで画像検索
│ (Worker Service)    │    Unsplash API 使用
└──────────┬──────────┘
           │ 画像 URL を Queue に送信
           ↓
┌─────────────────────────────────┐
│   Azure Storage Queue           │
│   (processing-queue)            │
└──────────┬──────────────────────┘
           │
           ↓
┌─────────────────────┐
│ SimpleWorker or     │ ← 画像をダウンロード・加工
│ MultithreadWorker   │    結果を Blob Storage に保存
│ (Worker Service)    │
└─────────────────────┘
```

## 前提条件

### 必須
- .NET 10 SDK
- Windows (ImageProcessor.Admin 用)
- Azurite または Azure Storage Emulator

### オプション
- Unsplash API キー (画像検索機能を使う場合)

## ステップ 1: Azure Storage のセットアップ

### 1.1 Azurite の起動

```bash
# npm でインストール (初回のみ)
npm install -g azurite

# Azurite を起動
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

または Docker で:

```bash
docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  --name azurite \
  mcr.microsoft.com/azure-storage/azurite
```

### 1.2 必要なキューとコンテナーの作成

PowerShell で以下を実行:

```powershell
# Azure.Storage パッケージを使用
dotnet new console -n StorageSetup
cd StorageSetup
dotnet add package Azure.Storage.Queues
dotnet add package Azure.Storage.Blobs

# Program.cs を以下の内容で作成
@"
using Azure.Storage.Queues;
using Azure.Storage.Blobs;

var connectionString = "UseDevelopmentStorage=true";

// Queue の作成
var queueClient = new QueueClient(connectionString, "processing-queue");
await queueClient.CreateIfNotExistsAsync();
Console.WriteLine("Queue 'processing-queue' created.");

// Blob Container の作成
var blobServiceClient = new BlobServiceClient(connectionString);
var originalContainer = blobServiceClient.GetBlobContainerClient("original-images");
await originalContainer.CreateIfNotExistsAsync();
Console.WriteLine("Container 'original-images' created.");

var processedContainer = blobServiceClient.GetBlobContainerClient("processed-images");
await processedContainer.CreateIfNotExistsAsync();
Console.WriteLine("Container 'processed-images' created.");
"@ | Out-File -Encoding UTF8 Program.cs

dotnet run
cd ..
```

または、Azure Storage Explorer を使用して手動で作成:
- Queue: `processing-queue`
- Blob Containers: `original-images`, `processed-images`

## ステップ 2: 設定ファイルの確認

### 2.1 ImageProcessor.Web の設定

`ImageProcessor.Web/appsettings.json` を確認:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### 2.2 Worker Services の設定

各 Worker の `appsettings.json` で接続文字列を確認:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  }
}
```

### 2.3 SearchWorker の設定 (オプション)

Unsplash API を使用する場合、`ImageProcessor.SearchWorker/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  },
  "BingSearch": {
    "AccessKey": "YOUR_UNSPLASH_ACCESS_KEY_HERE",
    
  }
}
```

## ステップ 3: コンポーネントの起動

### 起動順序

1. Azurite (既に起動済み)
2. ImageProcessor.Web
3. ImageProcessor.Admin
4. ImageProcessor.SearchWorker (オプション)
5. ImageProcessor.SimpleWorker または MultithreadWorker

### 3.1 ImageProcessor.Web の起動

```bash
cd ImageProcessor.Web
dotnet run
```

起動後、ブラウザで `http://localhost:5000` にアクセスできることを確認。

### 3.2 ImageProcessor.Admin の起動

**新しいターミナルで:**

```powershell
dotnet run --project ImageProcessor.Admin
```

または Visual Studio で F5 キーを押して実行。

**起動確認:**
- WPF ウィンドウが表示される
- 左上の接続状態インジケーター (赤 → 緑になれば接続成功)

### 3.3 ImageProcessor.SearchWorker の起動 (オプション)

**新しいターミナルで:**

```bash
dotnet run --project ImageProcessor.SearchWorker
```

**起動確認:**
```
info: ImageProcessor.SearchWorker.Worker[0]
      Worker started at: ...
```

### 3.4 ImageProcessor.SimpleWorker の起動

**新しいターミナルで:**

```bash
dotnet run --project ImageProcessor.SimpleWorker
```

**起動確認:**
```
info: ImageProcessor.SimpleWorker.Worker[0]
      Worker started at: ...
      Polling queue: processing-queue
```

## ステップ 4: 動作確認

### テストシナリオ 1: Web → SignalR → Admin

1. ブラウザで `http://localhost:5000` を開く
2. キーワード入力欄に「test」と入力
3. 「指定」ボタンをクリック
4. ImageProcessor.Admin で SignalR 経由でキーワードが受信されることを確認

**期待される動作:**
- Web: Ajax リクエストが成功
- Admin: キーワードが表示される

### テストシナリオ 2: Queue へのメッセージ送信

ImageProcessor.Admin から Queue にメッセージを送信:

1. Admin アプリで処理を開始
2. Worker のログでメッセージが受信されることを確認

### テストシナリオ 3: 完全なワークフロー (SearchWorker 含む)

1. Web でキーワード「cat」を入力
2. Admin で受信確認
3. SearchWorker が画像検索を実行
4. SimpleWorker/MultithreadWorker が画像を処理
5. Blob Storage に結果が保存される

**確認方法:**

Azure Storage Explorer で:
- Queue `processing-queue` にメッセージが追加される
- Blob Container `original-images` に画像がアップロードされる
- Blob Container `processed-images` に加工済み画像が保存される

## ステップ 5: 手動テスト (コンポーネント単独)

### Queue に直接メッセージを送信

PowerShell スクリプトでテストメッセージを送信:

```powershell
$code = @"
using Azure.Storage.Queues;

var connectionString = "UseDevelopmentStorage=true";
var queueClient = new QueueClient(connectionString, "processing-queue");

var message = "{\"keyword\":\"test\",\"imageUrl\":\"https://example.com/image.jpg\"}";
await queueClient.SendMessageAsync(message);
Console.WriteLine("Message sent: " + message);
"@

$code | Out-File -Encoding UTF8 test.csx
dotnet script test.csx
```

Worker がメッセージを受信して処理することを確認。

## トラブルシューティング

### 1. Azurite に接続できない

```bash
# Azurite が起動しているか確認
netstat -an | findstr "10000 10001 10002"

# 再起動
taskkill /F /IM azurite.exe
azurite --silent
```

### 2. SignalR 接続が失敗する

- ImageProcessor.Web が起動しているか確認
- CORS 設定を確認 (Program.cs で AllowAll に設定済み)
- ブラウザの開発者ツールで WebSocket 接続エラーを確認

### 3. Worker がメッセージを受信しない

- Queue が作成されているか確認 (Azure Storage Explorer)
- 接続文字列が正しいか確認
- Worker のログレベルを Debug に変更して詳細を確認

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### 4. ImageProcessor.Admin が起動しない

- Visual Studio でデバッグ実行
- グローバル例外ハンドラのエラーダイアログを確認
- イベントビューアでエラーログを確認 (`eventvwr.msc`)

## パフォーマンステスト

### MultithreadWorker の性能確認

```bash
# マルチスレッド Worker を起動
dotnet run --project ImageProcessor.MultithreadWorker

# appsettings.json でスレッド数を変更
"Worker": {
  "ConsumerThreadCount": 8,  // スレッド数を増やす
  "ChannelCapacity": 200
}
```

複数のメッセージを Queue に送信して、並列処理の効果を確認。

## 自動テストスクリプト (PowerShell)

```powershell
# test-integration.ps1

Write-Host "=== 統合テスト開始 ===" -ForegroundColor Green

# 1. Azurite の起動確認
$azuriteRunning = Get-Process azurite -ErrorAction SilentlyContinue
if (-not $azuriteRunning) {
    Write-Host "Azurite を起動しています..." -ForegroundColor Yellow
    Start-Process azurite -ArgumentList "--silent"
    Start-Sleep -Seconds 3
}

# 2. ビルド
Write-Host "プロジェクトをビルドしています..." -ForegroundColor Yellow
dotnet build

# 3. テストメッセージの送信
Write-Host "テストメッセージを送信しています..." -ForegroundColor Yellow
# (ここに Queue へのメッセージ送信コードを追加)

Write-Host "=== 統合テスト完了 ===" -ForegroundColor Green
```

## CI/CD パイプライン

GitHub Actions の例:

```yaml
name: Integration Test

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 10.0.x
      - name: Start Azurite
        run: |
          npm install -g azurite
          Start-Process azurite -ArgumentList "--silent"
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Run integration tests
        run: dotnet test --no-build --verbosity normal
```

## 次のステップ

1. **自動化**: テストスクリプトの作成
2. **監視**: Application Insights の統合
3. **デプロイ**: Azure へのデプロイ
4. **ドキュメント**: API ドキュメントの生成

## 参考資料

- [Azure Storage Emulator](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-use-azurite)
- [SignalR Documentation](https://learn.microsoft.com/ja-jp/aspnet/core/signalr/introduction)
- [Worker Services](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/workers)
