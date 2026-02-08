# トラブルシューティングガイド

このドキュメントでは、.NET 10 移行後のプロジェクトで発生する可能性のある問題と解決方法をまとめています。

## 目次

- [ImageProcessor.Admin (WPF)](#imageprocessoradmin-wpf)
- [Worker Services](#worker-services)
- [Azure Storage](#azure-storage)
- [ビルドエラー](#ビルドエラー)
- [実行時エラー](#実行時エラー)

---

## ImageProcessor.Admin (WPF)

### 問題: ウィンドウが起動しない（エラーなし）

#### 症状
- `dotnet run` を実行してもウィンドウが表示されない
- エラーメッセージも表示されない
- プロセスがすぐに終了する

#### 原因
- XAML リソースの読み込みエラー
- 起動時の例外が表示されていない

#### 解決方法

**手順1: Visual Studio でデバッグ**

```powershell
# Visual Studio で開く
start MultithreadDesignPattern.sln

# ImageProcessor.Admin を右クリック → デバッグ → 新しいインスタンスを開始
```

Visual Studio の出力ウィンドウで例外の詳細を確認。

**手順4: イベントビューアで確認**

```powershell
eventvwr.msc
```

Windows ログ > アプリケーション で、.NET Runtime のエラーを確認。

### 問題: SignalR 接続エラー

#### 症状
「サーバーに繋がりません。」エラーが表示される

#### 原因
- ImageProcessor.Web が起動していない
- URL が間違っている
- SignalR Hub のエンドポイントが見つからない

#### 解決方法

**手順1: ImageProcessor.Web の起動確認**

```powershell
# ImageProcessor.Web が起動しているか確認
netstat -ano | findstr ":53344"
```

**手順2: URL の確認**

`App.config` の WebSiteUrl 設定を確認:

```xml
<setting name="WebSiteUrl" serializeAs="String">
  <value>http://localhost:53344/</value>
</setting>
```

**手順3: SignalR Hub のエンドポイント確認**

MainViewModel.cs の接続部分を確認:

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl(Settings.Default.WebSiteUrl + "/hubs/keyword")
    .Build();
```

ImageProcessor.Web 側で `/hubs/keyword` が正しくマッピングされているか確認。

---

## Worker Services

### 問題: Worker が起動しない

#### 症状
```
Unhandled exception. System.InvalidOperationException: Unable to resolve service...
```

#### 原因
依存性注入の設定が不足している

#### 解決方法

`Program.cs` を確認:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// 必要なサービスを登録
builder.Services.AddSingleton(new QueueClient(
    builder.Configuration.GetConnectionString("StorageAccount"),
    "processing-queue"));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

### 問題: Azure Queue からメッセージが取得できない

#### 症状
- Worker は起動するが、メッセージを処理しない
- ログに「Waiting for messages...」だけが表示される

#### 原因
- Queue が存在しない
- 接続文字列が間違っている
- Azurite が起動していない

#### 解決方法

**手順1: Azurite の起動確認**

```bash
# Azurite が起動しているか確認
netstat -an | findstr "10001"

# 起動していない場合
azurite --silent
```

**手順2: Queue の作成**

```csharp
#r "nuget: Azure.Storage.Queues, 12.17.0"
using Azure.Storage.Queues;

var queueClient = new QueueClient("UseDevelopmentStorage=true", "processing-queue");
await queueClient.CreateIfNotExistsAsync();
Console.WriteLine("Queue created successfully!");
```

**手順3: 接続文字列の確認**

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  }
}
```

**手順4: テストメッセージの送信**

```csharp
var queueClient = new QueueClient("UseDevelopmentStorage=true", "processing-queue");
await queueClient.SendMessageAsync("test message");
Console.WriteLine("Message sent!");
```

### 問題: 画像処理でエラーが発生する

#### 症状
```
System.UnauthorizedAccessException: Access to the path is denied.
```

#### 原因
一時ファイルのアクセス権限の問題

#### 解決方法

```csharp
// 一時ファイルのパスを確認
var tempPath = Path.GetTempFileName();
Console.WriteLine($"Temp file: {tempPath}");

// または明示的なパスを使用
var tempDir = Path.Combine(Path.GetTempPath(), "ImageProcessor");
Directory.CreateDirectory(tempDir);
var tempFile = Path.Combine(tempDir, $"{Guid.NewGuid()}.jpg");
```

---

## Azure Storage

### 問題: Azurite に接続できない

#### 症状
```
No connection could be made because the target machine actively refused it.
```

#### 解決方法

**方法1: Azurite を起動**

```bash
# npm でインストール済みの場合
azurite --silent

# Docker で起動
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 \
  mcr.microsoft.com/azure-storage/azurite
```

**方法2: 接続文字列を確認**

```json
{
  "ConnectionStrings": {
    "StorageAccount": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;"
  }
}
```

### 問題: Queue または Blob Container が見つからない

#### 症状
```
Azure.RequestFailedException: The specified queue does not exist.
```

#### 解決方法

Azure Storage Explorer または以下のスクリプトで作成:

```csharp
#!/usr/bin/env dotnet-script
#r "nuget: Azure.Storage.Queues, 12.17.0"
#r "nuget: Azure.Storage.Blobs, 12.19.0"

using Azure.Storage.Queues;
using Azure.Storage.Blobs;

var connectionString = "UseDevelopmentStorage=true";

// Queue の作成
var queues = new[] { "processing-queue", "search-queue" };
foreach (var queueName in queues)
{
    var queueClient = new QueueClient(connectionString, queueName);
    await queueClient.CreateIfNotExistsAsync();
    Console.WriteLine($"Queue '{queueName}' created.");
}

// Blob Container の作成
var containers = new[] { "original-images", "processed-images" };
var blobServiceClient = new BlobServiceClient(connectionString);
foreach (var containerName in containers)
{
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    await containerClient.CreateIfNotExistsAsync();
    Console.WriteLine($"Container '{containerName}' created.");
}
```

---

## ビルドエラー

### 問題: Nullable 参照型の警告が大量に出る

#### 症状
```
warning CS8600: Converting null literal or possible null value to non-nullable type.
warning CS8602: Dereference of a possibly null reference.
```

#### 解決方法

**方法1: Nullable を無効化（一時的）**

`.csproj` ファイル:

```xml
<PropertyGroup>
  <Nullable>disable</Nullable>
</PropertyGroup>
```

**方法2: 特定の警告を抑制**

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <NoWarn>CS8600;CS8602;CS8603;CS8604</NoWarn>
</PropertyGroup>
```

**方法3: コードを修正（推奨）**

```csharp
// Before
string value = null;

// After
string? value = null;

// Before
var result = items.FirstOrDefault().Name;

// After
var result = items.FirstOrDefault()?.Name ?? "default";
```

### 問題: パッケージの復元エラー

#### 症状
```
error NU1102: Unable to find package...
```

#### 解決方法

```bash
# NuGet キャッシュをクリア
dotnet nuget locals all --clear

# パッケージを再復元
dotnet restore --force

# ビルド
dotnet build
```

---

## 実行時エラー

### 問題: DLL が見つからない

#### 症状
```
Could not load file or assembly 'SixLabors.ImageSharp'...
```

#### 解決方法

```bash
# クリーンビルド
dotnet clean
dotnet build

# bin/obj フォルダを削除
Remove-Item -Recurse -Force bin, obj
dotnet build
```

### 問題: 設定ファイルが読み込まれない

#### 症状
`Configuration.GetConnectionString()` が null を返す

#### 解決方法

**手順1: appsettings.json がコピーされているか確認**

`.csproj` ファイル:

```xml
<ItemGroup>
  <Content Include="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
  <Content Include="appsettings.Development.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

**手順2: ビルド出力を確認**

```bash
ls bin/Debug/net10.0/appsettings.json
```

**手順3: 環境変数を使用（代替手段）**

```bash
# Windows
$env:ConnectionStrings__StorageAccount="UseDevelopmentStorage=true"

# Linux/Mac
export ConnectionStrings__StorageAccount="UseDevelopmentStorage=true"
```

---

## デバッグのヒント

### ログレベルを上げる

`appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Debug",
      "System": "Debug"
    }
  }
}
```

### Visual Studio でのデバッグ

1. ブレークポイントを設定
2. F5 でデバッグ開始
3. 変数の値を確認
4. コールスタックを確認

### dotnet-trace でプロファイリング

```bash
# dotnet-trace のインストール
dotnet tool install --global dotnet-trace

# トレースの開始
dotnet trace collect --process-id <PID>
```

---

## さらにサポートが必要な場合

### 情報収集

問題を報告する際は、以下の情報を含めてください:

1. **エラーメッセージの全文**
2. **スタックトレース**
3. **再現手順**
4. **環境情報**:
   ```bash
   dotnet --info
   ```
5. **関連するログファイル**

### 関連リソース

- [.NET トラブルシューティング](https://learn.microsoft.com/ja-jp/dotnet/core/diagnostics/)
- [Azure Storage トラブルシューティング](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-monitoring-diagnosing-troubleshooting)
- [WPF トラブルシューティング](https://learn.microsoft.com/ja-jp/dotnet/desktop/wpf/advanced/troubleshooting-wpf-applications)
