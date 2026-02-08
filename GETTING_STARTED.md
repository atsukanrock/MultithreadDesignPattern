# Getting Started - プロジェクト起動ガイド

このガイドでは、MultithreadDesignPattern の各プロジェクトをローカル環境で起動する方法を説明します。

## 前提条件

### 必須
- **.NET 10 SDK** - [ダウンロード](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Windows** (ImageProcessor.Admin の実行に必要)

### オプション
- **Visual Studio 2022** - WPF アプリのデバッグに推奨
- **Visual Studio Code** - Worker Services のデバッグに推奨
- **Azurite** - ローカル Azure Storage エミュレーター

## プロジェクト構成

```
MultithreadDesignPattern/
├── ImageSearch/                    # データモデル (net10.0)
├── ImageProcessor.Core/            # 共通ロジック (net10.0)
├── ImageProcessor.SimpleWorker/    # シングルスレッド Worker (net10.0)
├── ImageProcessor.MultithreadWorker/ # マルチスレッド Worker (net10.0)
├── ImageProcessor.SearchWorker/    # 画像検索 Worker (net10.0)
├── ImageProcessor.Admin/           # WPF 管理ツール (net10.0-windows)
└── ImageProcessor.Web/             # ASP.NET Core Web UI (net10.0)
```

## クイックスタート

### 1. リポジトリのクローン

```bash
git clone <your-repo-url>
cd MultithreadDesignPattern
```

### 2. 依存関係の復元とビルド

```bash
dotnet restore
dotnet build
```

## API キーの設定

### Unsplash Access Key の取得

ImageProcessor.SearchWorker と ImageProcessor.Admin の画像検索機能には Unsplash API キーが必要です。

1. [https://unsplash.com/developers](https://unsplash.com/developers) でアカウント登録
2. 「New Application」でアプリを作成
3. **Access Key** をコピー（無料、50リクエスト/時間）

### SearchWorker への設定

`appsettings.json` にはプレースホルダーが入っています。実際のキーは **git 管理外** のファイルに書きます。

```bash
# ImageProcessor.SearchWorker/ に以下のファイルを作成（gitignore 済み）
```

`ImageProcessor.SearchWorker/appsettings.Development.json`:

```json
{
  "Unsplash": {
    "AccessKey": "あなたのキーをここに"
  }
}
```

起動時に環境変数で渡す方法もあります:

```bash
UNSPLASH_ACCESS_KEY="あなたのキー" dotnet run --project ImageProcessor.SearchWorker
```

### Admin (WPF) への設定

`app.config` の `UnsplashAccessKey` 設定に直接入力するか、Visual Studio の Settings デザイナーで設定します。

### ImageSearchTest（動作確認用）への設定

```bash
# 環境変数にセットして実行
export UNSPLASH_ACCESS_KEY="あなたのキー"   # Linux/macOS
$env:UNSPLASH_ACCESS_KEY="あなたのキー"     # PowerShell
dotnet run --project ImageSearchTest
```

正常に動作すると画像の ID と URL が出力されます:

```
Requesting: https://api.unsplash.com/search/photos?query=Ninja&per_page=5&...
Status: OK
Total results: 515
id: fB824nd3WWU, url: https://images.unsplash.com/...
```

> **注意**: Unsplash は英語キーワード向けのストック写真サービスです。日本語キーワードでは結果が少ない場合があります。

## 各プロジェクトの起動方法

### ImageProcessor.Admin (WPF アプリ)

**Windows でのみ動作します。**

#### 方法1: Visual Studio で実行

```powershell
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
- ヘッダー右側に赤い接続状態インジケーター（接続後は緑に変わる）
- デバッグモードの場合、サンプルキーワードが表示される

### ImageProcessor.SimpleWorker (Worker Service)

```bash
dotnet run --project ImageProcessor.SimpleWorker
```

### ImageProcessor.MultithreadWorker (Worker Service)

```bash
dotnet run --project ImageProcessor.MultithreadWorker
```

### ImageProcessor.SearchWorker (Worker Service)

```bash
# Unsplash キーを設定してから起動
dotnet run --project ImageProcessor.SearchWorker
```

### ImageProcessor.Web (ASP.NET Core)

```bash
dotnet run --project ImageProcessor.Web
# ブラウザで http://localhost:5000 にアクセス
```

## ローカル開発環境のセットアップ

### Azurite のセットアップ

Worker Services は Azure Storage を使用します。ローカル開発には Azurite を推奨します。

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

接続文字列（各 `appsettings.json` にデフォルト設定済み）:

```
UseDevelopmentStorage=true
```

### ログレベルの変更

`appsettings.Development.json`（gitignore 済み）を各プロジェクトに作成:

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

### "Unsplash access key not found in configuration"

環境変数または `appsettings.Development.json` にキーが設定されていません。
上記「API キーの設定」を参照してください。

### "Azure Storage に接続できない"

```bash
# Azurite が起動しているか確認
netstat -an | grep "10000\|10001\|10002"  # Linux
netstat -an | findstr "10000 10001 10002"  # Windows

# Azurite を再起動
azurite --silent
```

### "ポートが既に使用されている"

```bash
netstat -ano | findstr ":5000"   # Windows
kill $(lsof -t -i:5000)          # Linux/macOS
```

### ImageProcessor.Admin が起動しない

- Visual Studio でデバッグ実行して例外発生箇所を特定
- イベントビューア (`eventvwr.msc`) でエラーログを確認

## 参考資料

- [.NET 10 ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-10)
- [Unsplash API ドキュメント](https://unsplash.com/documentation)
- [Azurite ドキュメント](https://learn.microsoft.com/ja-jp/azure/storage/common/storage-use-azurite)
- [Worker Service ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/extensions/workers)
