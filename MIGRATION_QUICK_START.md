# .NET 8 移行クイックスタートガイド

このガイドでは、プロジェクトを .NET Framework 4.5 から .NET 8.0 に段階的に移行する手順を説明します。

## 移行の優先順位

```
1. ImageSearch (PCL)           → .NET 8 (簡単、依存なし)
2. ImageProcessor.Core         → .NET 8 (中程度、Azure SDK 依存あり)
3. ImageProcessor.SimpleWorker → .NET 8 Worker Service
4. ImageProcessor.MultithreadWorker → .NET 8 Worker Service
5. ImageProcessor.SearchWorker → .NET 8 Worker Service
6. ImageProcessor.Web          → ASP.NET Core 8.0
```

## ステップ 1: ImageSearch の移行（20分）

ImageSearch は依存関係のないデータモデルなので、最も簡単に移行できます。

### 1.1 バックアップ

```bash
cd ImageSearch
cp ImageSearch.csproj ImageSearch.csproj.framework
```

### 1.2 新しいプロジェクトファイルの作成

`ImageSearch/ImageSearch.csproj` を以下の内容で置き換えます：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

### 1.3 ビルド

```bash
cd ImageSearch
dotnet build
```

### 1.4 確認

ビルドが成功すれば完了です。エラーがある場合、ソースコードを少し修正する必要があります。

## ステップ 2: ImageProcessor.Core の移行（1-2時間）

### 2.1 課題の特定

Azure ServiceRuntime への依存を削除する必要があります：

- `Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment`
- `TasksRoleEntryPoint`
- `WorkerEntryPoint`

### 2.2 新しいプロジェクトファイル

`ImageProcessor.Core/ImageProcessor.Core.csproj` を以下の内容で置き換えます：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="8.0.0" />
  </ItemGroup>

</Project>
```

### 2.3 コード変更

#### Before (RoleEnvironment 使用)

```csharp
using Microsoft.WindowsAzure.ServiceRuntime;

var connectionString = RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString");
```

#### After (IConfiguration 使用)

```csharp
using Microsoft.Extensions.Configuration;

public class SomeService
{
    private readonly IConfiguration _configuration;

    public SomeService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void DoWork()
    {
        var connectionString = _configuration.GetConnectionString("StorageAccount");
    }
}
```

### 2.4 抽象化の追加

`ServiceRuntime/IWorkerEntryPoint.cs` を作成：

```csharp
namespace ImageProcessor.ServiceRuntime;

public interface IWorkerEntryPoint
{
    Task RunAsync(CancellationToken cancellationToken);
    Task OnStartAsync(CancellationToken cancellationToken);
    Task OnStopAsync();
}
```

`ServiceRuntime/WorkerEntryPoint.cs` を更新：

```csharp
namespace ImageProcessor.ServiceRuntime;

public abstract class WorkerEntryPoint : IWorkerEntryPoint
{
    public abstract Task RunAsync(CancellationToken cancellationToken);

    public virtual Task OnStartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public virtual Task OnStopAsync()
    {
        return Task.CompletedTask;
    }
}
```

## ステップ 3: SimpleWorker の移行（2-3時間）

### 3.1 新しい Worker Service プロジェクトの作成

```bash
# 既存のプロジェクトをバックアップ
mv ImageProcessor.SimpleWorker ImageProcessor.SimpleWorker.old

# 新しい Worker Service を作成
dotnet new worker -n ImageProcessor.SimpleWorker -f net8.0
cd ImageProcessor.SimpleWorker
```

### 3.2 プロジェクトファイルの編集

`ImageProcessor.SimpleWorker.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>dotnet-ImageProcessor.SimpleWorker-xxx</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.19.0" />
    <PackageReference Include="Azure.Storage.Queues" Version="12.17.0" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ImageProcessor.Core\ImageProcessor.Core.csproj" />
  </ItemGroup>

</Project>
```

### 3.3 Worker.cs の実装

```csharp
using Azure.Storage.Queues;
using Azure.Storage.Blobs;
using ImageProcessor.Storage.Queue.Messages;

namespace ImageProcessor.SimpleWorker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly QueueClient _queueClient;
    private readonly BlobServiceClient _blobServiceClient;

    public Worker(
        ILogger<Worker> logger,
        IConfiguration configuration,
        QueueClient queueClient,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _configuration = configuration;
        _queueClient = queueClient;
        _blobServiceClient = blobServiceClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Queue からメッセージを取得
                var message = await _queueClient.ReceiveMessageAsync(
                    cancellationToken: stoppingToken);

                if (message.Value != null)
                {
                    // メッセージを処理
                    await ProcessMessageAsync(message.Value.Body.ToString(), stoppingToken);

                    // メッセージを削除
                    await _queueClient.DeleteMessageAsync(
                        message.Value.MessageId,
                        message.Value.PopReceipt,
                        stoppingToken);
                }
                else
                {
                    // メッセージがない場合は少し待機
                    await Task.Delay(1000, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(string messageBody, CancellationToken cancellationToken)
    {
        // 画像処理ロジックをここに実装
        _logger.LogInformation("Processing message: {message}", messageBody);

        // TODO: 既存の Worker.cs からロジックを移植
    }
}
```

### 3.4 Program.cs の設定

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using ImageProcessor.SimpleWorker;

var builder = Host.CreateApplicationBuilder(args);

// Azure Storage クライアントの登録
var storageConnectionString = builder.Configuration.GetConnectionString("StorageAccount");

builder.Services.AddSingleton(new QueueClient(
    storageConnectionString,
    "processing-queue"));

builder.Services.AddSingleton(new BlobServiceClient(storageConnectionString));

// Worker の登録
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

### 3.5 appsettings.json の作成

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "ConnectionStrings": {
    "StorageAccount": "UseDevelopmentStorage=true"
  }
}
```

### 3.6 既存コードの移植

`ImageProcessor.SimpleWorker.old/Worker.cs` から画像処理ロジックを新しい `Worker.cs` に移植します。

主な変更点：
- `System.Drawing` → `SixLabors.ImageSharp`
- `CloudStorageAccount` → `BlobServiceClient`
- `CloudQueue` → `QueueClient`

### 3.7 ビルドとテスト

```bash
dotnet build
dotnet run
```

## ステップ 4: MultithreadWorker の移行（3-4時間）

SimpleWorker と同様の手順で、Producer-Consumer パターンを `System.Threading.Channels` を使って実装します。

```bash
dotnet new worker -n ImageProcessor.MultithreadWorker -f net8.0
```

### Channel を使った実装例

```csharp
using System.Threading.Channels;

public class MultithreadWorker : BackgroundService
{
    private readonly Channel<ProcessingRequestMessage> _channel;
    private readonly int _consumerCount;

    public MultithreadWorker(IConfiguration configuration)
    {
        var capacity = configuration.GetValue<int>("Worker:ChannelCapacity", 100);
        _consumerCount = configuration.GetValue<int>("Worker:ConsumerThreadCount", 4);

        _channel = Channel.CreateBounded<ProcessingRequestMessage>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Producer タスク
        var producerTask = Task.Run(() => ProduceAsync(stoppingToken), stoppingToken);

        // Consumer タスク（複数）
        var consumerTasks = Enumerable
            .Range(0, _consumerCount)
            .Select(i => Task.Run(() => ConsumeAsync(i, stoppingToken), stoppingToken))
            .ToArray();

        // Producer が完了するまで待機
        await producerTask;

        // Channel を閉じる
        _channel.Writer.Complete();

        // すべての Consumer が完了するまで待機
        await Task.WhenAll(consumerTasks);
    }

    private async Task ProduceAsync(CancellationToken cancellationToken)
    {
        // Queue からメッセージを取得して Channel に書き込む
    }

    private async Task ConsumeAsync(int consumerId, CancellationToken cancellationToken)
    {
        // Channel からメッセージを読み取って処理
        await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            // 画像処理
        }
    }
}
```

## ステップ 5: Web プロジェクトの移行（1-2週間）

これは最も複雑な移行です。別途詳細ガイドを参照してください。

### 大まかな手順

1. 新しい ASP.NET Core プロジェクトを作成
2. Controllers を移行
3. SignalR Hub を移行
4. Views を移行
5. 静的ファイルを移行
6. 依存性注入の設定

## 画像処理ライブラリの移行

### System.Drawing から ImageSharp への移行

#### Before

```csharp
using System.Drawing;
using System.Drawing.Imaging;

using (var image = Image.FromStream(inputStream))
using (var resized = new Bitmap(width, height))
using (var graphics = Graphics.FromImage(resized))
{
    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
    graphics.DrawImage(image, 0, 0, width, height);
    resized.Save(outputStream, ImageFormat.Jpeg);
}
```

#### After

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

using (var image = await Image.LoadAsync(inputStream))
{
    image.Mutate(x => x.Resize(new ResizeOptions
    {
        Size = new Size(width, height),
        Mode = ResizeMode.Max,
        Sampler = KnownResamplers.Bicubic
    }));

    await image.SaveAsJpegAsync(outputStream);
}
```

## Azure Storage SDK の移行

### WindowsAzure.Storage から Azure.Storage.* への移行

#### Before

```csharp
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

var account = CloudStorageAccount.Parse(connectionString);
var blobClient = account.CreateCloudBlobClient();
var container = blobClient.GetContainerReference("images");
var blob = container.GetBlockBlobReference("image.jpg");

await blob.UploadFromStreamAsync(stream);
var url = blob.Uri.ToString();
```

#### After

```csharp
using Azure.Storage.Blobs;

var blobServiceClient = new BlobServiceClient(connectionString);
var containerClient = blobServiceClient.GetBlobContainerClient("images");
var blobClient = containerClient.GetBlobClient("image.jpg");

await blobClient.UploadAsync(stream, overwrite: true);
var url = blobClient.Uri.ToString();
```

## トラブルシューティング

### Nullable 参照型の警告

.NET 8 では nullable 参照型がデフォルトで有効です。警告が多数出る場合：

```xml
<PropertyGroup>
  <Nullable>disable</Nullable>
</PropertyGroup>
```

または

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <NoWarn>CS8600;CS8602;CS8603;CS8604</NoWarn>
</PropertyGroup>
```

### ImplicitUsings の問題

`ImplicitUsings` が有効だと、一部の using が自動的に含まれます。問題がある場合：

```xml
<PropertyGroup>
  <ImplicitUsings>disable</ImplicitUsings>
</PropertyGroup>
```

## 次のステップ

1. **ImageSearch を移行** - 最も簡単なので練習に最適
2. **ImageProcessor.Core を移行** - 他のプロジェクトの基盤
3. **Worker を一つずつ移行** - SimpleWorker → MultithreadWorker → SearchWorker
4. **Web プロジェクトを移行** - 最後に実施

各ステップでテストを実施し、問題がないことを確認してから次に進んでください。

## 参考資料

- [.NET 8 の新機能](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-8)
- [.NET Framework から .NET への移行](https://learn.microsoft.com/ja-jp/dotnet/core/porting/)
- [ImageSharp ドキュメント](https://docs.sixlabors.com/articles/imagesharp/)
- [Azure Storage SDK v12 移行ガイド](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-dotnet-migration-guide)
