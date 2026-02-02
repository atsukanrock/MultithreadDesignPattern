# .NET 8 移行ステータス

このドキュメントは、MultithreadDesignPattern プロジェクトの .NET Framework 4.5 から .NET 8 への移行状況を記録しています。

## 移行完了日: 2026-02-03

## 移行完了プロジェクト

### ✅ 1. ImageSearch (PCL)
- **移行日**: 2026-02-03
- **難易度**: ⭐ (簡単)
- **ターゲット**: net8.0
- **主な変更**:
  - SDK スタイルのプロジェクトファイルに変換
  - 依存関係なしのため、スムーズに移行完了

### ✅ 2. ImageProcessor.Core
- **移行日**: 2026-02-03
- **難易度**: ⭐⭐ (中程度)
- **ターゲット**: net8.0
- **主な変更**:
  - Azure ServiceRuntime 依存を削除
  - `RoleEnvironment` → `IConfiguration` に置き換え
  - `WorkerEntryPoint` 抽象クラスを独自実装
  - Microsoft.Extensions.Configuration.Abstractions を追加
  - Microsoft.Extensions.Hosting.Abstractions を追加

### ✅ 3. ImageProcessor.SimpleWorker
- **移行日**: 2026-02-03
- **難易度**: ⭐⭐ (中程度)
- **ターゲット**: net8.0
- **プロジェクトタイプ**: Worker Service
- **主な変更**:
  - Azure Cloud Service → .NET Worker Service に移行
  - `BackgroundService` を使用した実装
  - Azure Storage SDK v12 に更新
  - System.Drawing → SixLabors.ImageSharp に移行

### ✅ 4. ImageProcessor.MultithreadWorker
- **移行日**: 2026-02-03
- **難易度**: ⭐⭐⭐ (やや複雑)
- **ターゲット**: net8.0
- **プロジェクトタイプ**: Worker Service
- **主な変更**:
  - Producer-Consumer パターンを `System.Threading.Channels` で実装
  - マルチスレッド処理を .NET 8 の Task ベースに変更
  - Azure Storage SDK v12 に更新
  - System.Drawing → SixLabors.ImageSharp に移行

### ✅ 5. ImageProcessor.SearchWorker
- **移行日**: 2026-02-03
- **難易度**: ⭐⭐ (中程度)
- **ターゲット**: net8.0
- **プロジェクトタイプ**: Worker Service
- **主な変更**:
  - Azure Cloud Service → .NET Worker Service に移行
  - 画像検索機能の .NET 8 対応
  - Azure Storage SDK v12 に更新

### ✅ 6. ImageProcessor.Admin
- **移行日**: 2026-02-03
- **難易度**: ⭐⭐⭐⭐ (複雑)
- **ターゲット**: net8.0-windows
- **プロジェクトタイプ**: WPF Application
- **主な変更**:
  - MVVM Light → CommunityToolkit.Mvvm 8.2.2 に移行
  - MahApps.Metro 0.13 → 2.4.10 に更新
  - Microsoft.AspNet.SignalR.Client → Microsoft.AspNetCore.SignalR.Client 8.0
  - WindowsAzure.Storage → Azure.Storage.Blobs/Queues 12.x
  - Reactive Extensions 2.x → System.Reactive 6.0
  - ImageProcessor → SixLabors.ImageSharp 3.1.6
  - グローバル例外ハンドラを追加（起動時エラーの可視化）
  - MahApps.Metro 2.x のリソースパスに更新
- **動作確認**: ✅ Windows で起動確認済み

## 未移行プロジェクト

### ⏳ 7. ImageProcessor.Web
- **ターゲット**: ASP.NET Core 8.0
- **推定難易度**: ⭐⭐⭐⭐⭐ (非常に複雑)
- **プロジェクトタイプ**: ASP.NET MVC 5 → ASP.NET Core MVC
- **主な課題**:
  - ASP.NET MVC 5 → ASP.NET Core MVC への移行
  - ASP.NET SignalR → ASP.NET Core SignalR への移行
  - Controllers の移行
  - Views (Razor) の移行
  - 依存性注入の再設計
  - 認証・認可の移行（必要な場合）
  - 静的ファイルの移行
  - Bundling & Minification の移行

## 移行による主な技術スタックの変更

| カテゴリ | .NET Framework 4.5 | .NET 8 |
|---------|-------------------|---------|
| ランタイム | .NET Framework 4.5 | .NET 8.0 |
| プロジェクト形式 | 旧形式 (.csproj) | SDK スタイル |
| Azure SDK | WindowsAzure.Storage 4.x | Azure.Storage.* 12.x |
| 画像処理 | System.Drawing / ImageProcessor | SixLabors.ImageSharp |
| MVVM (WPF) | MVVM Light 4.x | CommunityToolkit.Mvvm 8.x |
| UI Framework (WPF) | MahApps.Metro 0.13 | MahApps.Metro 2.4 |
| SignalR | Microsoft.AspNet.SignalR 2.1 | Microsoft.AspNetCore.SignalR 8.0 |
| Reactive Extensions | Rx 2.2 | System.Reactive 6.0 |
| Worker Service | Azure Cloud Service | .NET Worker Service |

## 既知の問題

### ImageProcessor.Admin
- ✅ **解決済み**: 初回起動時にウィンドウが表示されない問題
  - **原因**: MahApps.Metro のリソースパスが古い
  - **対処**: App.xaml のリソースパスを 2.x 用に更新
  - **追加対応**: グローバル例外ハンドラを追加してエラーを可視化

### SixLabors.ImageSharp
- ⚠️ **警告**: バージョン 3.1.6 に既知の脆弱性あり
  - **対処予定**: より新しいバージョンへの更新を検討
  - **現状**: 開発環境での使用のため、当面は問題なし

## 次のステップ

### 優先度 1: ImageProcessor.Web の移行
最後の未移行プロジェクト。全体のシステムを .NET 8 で動作させるために必須。

**推奨アプローチ**:
1. 新しい ASP.NET Core プロジェクトを作成
2. Controllers を段階的に移行
3. SignalR Hub を ASP.NET Core SignalR に移行
4. Views を移行
5. 依存性注入の設定
6. 統合テスト

### 優先度 2: 完全な統合テスト
全プロジェクトが .NET 8 に移行された後、エンドツーエンドでの動作確認。

1. ImageProcessor.Web 起動
2. ImageProcessor.Admin 起動
3. SignalR 経由でのキーワード送信/受信
4. 画像検索機能
5. Worker Service での画像処理
6. 結果の確認

### 優先度 3: パフォーマンス測定
.NET Framework 4.5 と .NET 8 でのパフォーマンス比較。

### 優先度 4: デプロイメント戦略
Azure Cloud Service から Azure App Service / Azure Container Apps への移行計画。

## 移行の成果

### メリット
- ✅ 最新の .NET ランタイムによるパフォーマンス向上
- ✅ クロスプラットフォーム対応（Worker Services）
- ✅ 最新の Azure SDK によるセキュリティ向上
- ✅ 長期サポート（LTS）の恩恵
- ✅ 最新のライブラリとツールの利用可能

### 課題
- ⚠️ ImageProcessor.Web の移行が残っている
- ⚠️ 一部ライブラリの API 変更による学習コスト
- ⚠️ WPF アプリ（ImageProcessor.Admin）は引き続き Windows 専用

## 参考資料

- [.NET 8 の新機能](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-8)
- [.NET Framework から .NET への移行](https://learn.microsoft.com/ja-jp/dotnet/core/porting/)
- [ImageSharp ドキュメント](https://docs.sixlabors.com/articles/imagesharp/)
- [Azure Storage SDK v12 移行ガイド](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-dotnet-migration-guide)
- [ASP.NET Core SignalR 移行ガイド](https://learn.microsoft.com/ja-jp/aspnet/core/signalr/migration)
