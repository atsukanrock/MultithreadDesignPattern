# MultithreadDesignPattern

マルチスレッド設計パターンのコードサンプル集 - .NET 8 に移行しました

## 概要

このプロジェクトは、マルチスレッドプログラミングの設計パターンを実装したサンプルコードです。
画像処理ワークフローを通じて、以下のパターンを学ぶことができます：

- **Producer-Consumer パターン** - Queue を使った非同期処理
- **Single Thread Worker** - シンプルな順次処理
- **Multi Thread Worker** - Channel を使った並列処理
- **WPF アプリケーション** - MVVM パターンと Reactive Extensions

## プロジェクト構成

```
MultithreadDesignPattern/
├── ImageSearch/                    # 画像検索データモデル (net8.0)
├── ImageProcessor.Core/            # 共通ロジック (net8.0)
├── ImageProcessor.SimpleWorker/    # シングルスレッド Worker (net8.0)
├── ImageProcessor.MultithreadWorker/ # マルチスレッド Worker (net8.0)
├── ImageProcessor.SearchWorker/    # 画像検索 Worker (net8.0)
├── ImageProcessor.Admin/           # WPF 管理ツール (net8.0-windows)
└── ImageProcessor.Web/             # ASP.NET Core Web UI (net8.0) ✅
```

## 技術スタック

### .NET 8 移行完了 (7/7) ✅

| プロジェクト | フレームワーク | 主要技術 |
|------------|--------------|---------|
| ImageSearch | .NET 8 | データモデル |
| ImageProcessor.Core | .NET 8 | 共通ロジック, IConfiguration |
| SimpleWorker | .NET 8 | Worker Service, Azure Storage |
| MultithreadWorker | .NET 8 | Worker Service, System.Threading.Channels |
| SearchWorker | .NET 8 | Worker Service, Bing Search API |
| ImageProcessor.Admin | .NET 8 (Windows) | WPF, MVVM, SignalR Client, Reactive Extensions |
| ImageProcessor.Web | .NET 8 | ASP.NET Core MVC, SignalR, Web API |

### 使用ライブラリ (.NET 8)

- **UI**: MahApps.Metro 2.4, CommunityToolkit.Mvvm 8.2
- **Web**: ASP.NET Core MVC, SignalR 8.0
- **画像処理**: SixLabors.ImageSharp 3.1
- **Azure**: Azure.Storage.Blobs 12.x, Azure.Storage.Queues 12.x
- **リアルタイム通信**: Microsoft.AspNetCore.SignalR 8.0 (Server/Client)
- **リアクティブ**: System.Reactive 6.0

## クイックスタート

### 前提条件

- .NET 8 SDK ([ダウンロード](https://dotnet.microsoft.com/download/dotnet/8.0))
- Windows (ImageProcessor.Admin の実行に必要)
- Azurite または Azure Storage Emulator (オプション)

### ビルド

```bash
# リポジトリのクローン
git clone <your-repo-url>
cd MultithreadDesignPattern

# 依存関係の復元
dotnet restore

# ビルド
dotnet build
```

### 実行

#### 1. ImageProcessor.Admin (WPF アプリ)

```powershell
# Windows で実行
dotnet run --project ImageProcessor.Admin
```

#### 2. ImageProcessor.Web (ASP.NET Core アプリ)

```bash
# Web アプリケーションの起動
dotnet run --project ImageProcessor.Web

# ブラウザで http://localhost:5000 にアクセス
```

#### 3. Worker Services

```bash
# シングルスレッド Worker
dotnet run --project ImageProcessor.SimpleWorker

# マルチスレッド Worker
dotnet run --project ImageProcessor.MultithreadWorker

# 画像検索 Worker
dotnet run --project ImageProcessor.SearchWorker
```

## ドキュメント

詳細なドキュメントが利用可能です：

- **[MIGRATION_STATUS.md](MIGRATION_STATUS.md)** - .NET 8 移行の詳細な記録
- **[GETTING_STARTED.md](GETTING_STARTED.md)** - 各プロジェクトの起動方法とセットアップ
- **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** - よくある問題と解決方法
- **[MIGRATION_QUICK_START.md](MIGRATION_QUICK_START.md)** - 移行手順のステップバイステップガイド

## 学べる設計パターン

### 1. Producer-Consumer パターン (MultithreadWorker)

`System.Threading.Channels` を使用した効率的な Producer-Consumer 実装：

```csharp
var channel = Channel.CreateBounded<ProcessingRequestMessage>(new BoundedChannelOptions(capacity)
{
    FullMode = BoundedChannelFullMode.Wait
});

// Producer
await foreach (var item in ProduceAsync())
{
    await channel.Writer.WriteAsync(item);
}
channel.Writer.Complete();

// Consumer (複数)
await foreach (var item in channel.Reader.ReadAllAsync())
{
    await ProcessAsync(item);
}
```

### 2. シングルスレッド処理 (SimpleWorker)

BackgroundService を使用したシンプルな順次処理：

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        var message = await _queueClient.ReceiveMessageAsync(stoppingToken);
        if (message.Value != null)
        {
            await ProcessMessageAsync(message.Value);
        }
        await Task.Delay(1000, stoppingToken);
    }
}
```

### 3. MVVM + Reactive Extensions (ImageProcessor.Admin)

WPF アプリケーションでの MVVM パターンと Reactive Extensions の組み合わせ：

```csharp
// ViewModel with CommunityToolkit.Mvvm
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private int _processingMilliseconds;

    [RelayCommand(CanExecute = nameof(CanStartProcessing))]
    private async Task StartProcessing()
    {
        // 処理実装
    }

    private bool CanStartProcessing() => OriginalImagePaths.Any();
}

// Reactive Extensions でのイベント処理
Observable.FromEventPattern<ImageProcessedEventArgs>(
    eh => processor.ImageProcessed += eh,
    eh => processor.ImageProcessed -= eh)
    .Subscribe(args => OnImageProcessed(args));
```

## 移行の成果

### パフォーマンス
- .NET 8 の最適化により、画像処理が約 20-30% 高速化（予想）
- Channel ベースの実装により、メモリ使用量が削減

### 開発体験
- Source Generators による MVVM のボイラープレート削減
- Nullable 参照型によるバグの早期発見
- 最新の C# 機能の活用

### 保守性
- SDK スタイルのプロジェクトファイルでシンプルに
- 最新の Azure SDK でセキュリティ向上
- 長期サポート（LTS）の恩恵

## 今後の予定

- [x] ImageProcessor.Web を ASP.NET Core 8.0 に移行 ✅
- [ ] 完全な統合テストの実装
- [ ] パフォーマンスベンチマークの実施
- [ ] Azure へのデプロイ自動化
- [ ] Docker コンテナ化

## トラブルシューティング

問題が発生した場合は、[TROUBLESHOOTING.md](TROUBLESHOOTING.md) を参照してください。

よくある問題：
- **ImageProcessor.Admin が起動しない** → Visual Studio でデバッグ実行
- **Worker が Azure Storage に接続できない** → Azurite の起動確認
- **ビルドエラー** → `dotnet clean && dotnet build`

## ライセンス

このプロジェクトはマルチスレッド設計パターンの学習目的で作成されています。

## 貢献

移行作業の詳細は [MIGRATION_STATUS.md](MIGRATION_STATUS.md) を参照してください。

---

**移行進捗**: 7/7 プロジェクト完了 (100%) 🎉✅

全プロジェクトの .NET 8 移行が完了しました！

最終更新: 2026-02-05
