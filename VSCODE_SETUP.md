# VSCode セットアップガイド

このドキュメントでは、VSCode で MultithreadDesignPattern プロジェクトを開発するための環境構築方法を説明します。

## 前提条件

### Windows/Linux/WSL2 で開発する場合

1. **.NET 10 SDK**
   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 10.0

   # または
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-10.0
   ```

2. **確認**
   ```bash
   dotnet --version
   # 10.0.x と表示されることを確認
   ```

## VSCode 拡張機能のインストール

VSCode を開くと、推奨拡張機能のインストールを促すメッセージが表示されます。以下の拡張機能をインストールしてください：

### 必須

- **C# Dev Kit** (`ms-dotnettools.csdevkit`)
- **C#** (`ms-dotnettools.csharp`)

### 推奨

- **Azure Functions** (`ms-azuretools.vscode-azurefunctions`)
- **Azure App Service** (`ms-azuretools.vscode-azureappservice`)
- **GitLens** (`eamodio.gitlens`)
- **EditorConfig** (`editorconfig.editorconfig`)

手動でインストールする場合：
```bash
code --install-extension ms-dotnettools.csdevkit
code --install-extension ms-dotnettools.csharp
```

## プロジェクトのビルド

### .NET 10 プロジェクト（Linux/Windows/macOS）

1. **ビルド**
   ```bash
   Ctrl+Shift+B
   ```

   または手動で：
   ```bash
   dotnet build
   ```

2. **クリーンビルド**
   ```bash
   dotnet clean
   dotnet build
   ```

## デバッグ

### .NET 10 プロジェクトのデバッグ（Linux/Windows/macOS）

1. VSCode のサイドバーから「実行とデバッグ」を選択（Ctrl+Shift+D）

2. デバッグ構成を選択：
   - **Web** - Web アプリケーション
   - **SimpleWorker** - Worker サービス
   - **MultithreadWorker** - マルチスレッド Worker

3. F5 を押してデバッグ開始

### ブレークポイントの設定

1. ソースコードの行番号の左側をクリック
2. 赤い点が表示されればブレークポイントが設定されています
3. F5 でデバッグを開始すると、ブレークポイントで停止します

### デバッグコマンド

- **F5** - 続行
- **F10** - ステップオーバー
- **F11** - ステップイン
- **Shift+F11** - ステップアウト
- **Ctrl+Shift+F5** - 再起動
- **Shift+F5** - 停止

## 現在のプロジェクト状態

### ビルド可能なプロジェクト（全て .NET 10）

#### Windows/Linux/macOS
- ✅ MultithreadDesignPattern (コンソール)
- ✅ ProducerConsumer.ConsoleApp (コンソール)
- ✅ ImageProcessor.Core (ライブラリ)
- ✅ ImageSearch (ライブラリ)
- ✅ ImageProcessor.Web (ASP.NET Core)
- ✅ ImageProcessor.SimpleWorker (Worker Service)
- ✅ ImageProcessor.MultithreadWorker (Worker Service)
- ✅ ImageProcessor.SearchWorker (Worker Service)

#### Windows のみ
- ✅ ImageProcessor.Admin (WPF)

## トラブルシューティング

### OmniSharp が起動しない

1. VSCode を再起動
2. OmniSharp のログを確認: `Ctrl+Shift+P` → "OmniSharp: Show OmniSharp Log"
3. .NET SDK が正しくインストールされているか確認

### デバッグが開始しない

1. プロジェクトが正しくビルドされているか確認
2. `launch.json` の `program` パスが正しいか確認
3. ビルド出力ディレクトリに DLL/EXE が存在するか確認

## 便利なキーボードショートカット

### 一般
- **Ctrl+Shift+P** - コマンドパレット
- **Ctrl+P** - ファイルを開く
- **Ctrl+,** - 設定

### 編集
- **Ctrl+Space** - IntelliSense
- **F12** - 定義に移動
- **Shift+F12** - 参照の検索
- **Ctrl+.** - クイックフィックス
- **Shift+Alt+F** - コードフォーマット

### デバッグ
- **F5** - デバッグ開始/続行
- **Ctrl+F5** - デバッグなしで実行
- **F9** - ブレークポイントの切り替え
- **F10** - ステップオーバー
- **F11** - ステップイン

### ビルド
- **Ctrl+Shift+B** - ビルド
- **Ctrl+Shift+T** - タスクの実行

## 次のステップ

1. **開発環境の確認**
   - .NET 10 SDK がインストールされているか確認
   - `dotnet --version` で `10.0.x` と表示されることを確認

2. **プロジェクトのビルド**
   - `dotnet build` でビルド確認

3. **実行**
   - [GETTING_STARTED.md](GETTING_STARTED.md) を参照

## 参考資料

- [VSCode C# 開発](https://code.visualstudio.com/docs/languages/csharp)
- [.NET 10 ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-10)
- [VSCode タスク](https://code.visualstudio.com/docs/editor/tasks)
- [VSCode デバッグ](https://code.visualstudio.com/docs/editor/debugging)
