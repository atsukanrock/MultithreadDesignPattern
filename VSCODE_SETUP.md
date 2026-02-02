# VSCode セットアップガイド

このドキュメントでは、VSCode で MultithreadDesignPattern プロジェクトを開発するための環境構築方法を説明します。

## 前提条件

### Windows で開発する場合（.NET Framework プロジェクト）

1. **Visual Studio Build Tools** または **Visual Studio 2022**
   - [Visual Studio Build Tools のダウンロード](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2022)
   - 「.NET デスクトップ ビルド ツール」をインストール

2. **.NET Framework 4.5 SDK**（Visual Studio に含まれる）

3. **NuGet CLI**
   ```powershell
   # PowerShell で実行
   choco install nuget.commandline
   # または
   winget install Microsoft.NuGet
   ```

4. **MSBuild** が PATH に含まれていることを確認
   ```powershell
   msbuild -version
   ```

   PATH に追加する必要がある場合：
   ```powershell
   C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin
   ```

### Linux/WSL2 で開発する場合（.NET 8 移行後のプロジェクト）

1. **.NET 8 SDK**
   ```bash
   # Ubuntu/Debian
   wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 8.0

   # または
   sudo apt-get update
   sudo apt-get install -y dotnet-sdk-8.0
   ```

2. **確認**
   ```bash
   dotnet --version
   # 8.0.x と表示されることを確認
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

### .NET Framework プロジェクト（Windows のみ）

1. **NuGet パッケージの復元**
   ```bash
   # VSCode のターミナルで実行
   Ctrl+Shift+P → "Tasks: Run Task" → "restore-nuget"
   ```

   または手動で：
   ```powershell
   nuget restore MultithreadDesignPattern.sln
   ```

2. **ソリューションのビルド**
   ```bash
   Ctrl+Shift+B → "build-framework" を選択
   ```

   または手動で：
   ```powershell
   msbuild /p:Configuration=Debug MultithreadDesignPattern.sln
   ```

3. **個別プロジェクトのビルド**
   ```bash
   Ctrl+Shift+P → "Tasks: Run Task" →
   - "build-web-framework" (Web プロジェクト)
   - "build-simpleworker-framework" (Worker プロジェクト)
   ```

### .NET 8 プロジェクト（Linux/Windows/macOS）

1. **ビルド**
   ```bash
   Ctrl+Shift+B (デフォルトタスク: build-dotnet8)
   ```

   または手動で：
   ```bash
   dotnet build
   ```

2. **クリーンビルド**
   ```bash
   Ctrl+Shift+P → "Tasks: Run Task" → "clean-dotnet8"
   dotnet build
   ```

## デバッグ

### .NET Framework プロジェクトのデバッグ（Windows）

1. VSCode のサイドバーから「実行とデバッグ」を選択（Ctrl+Shift+D）

2. デバッグ構成を選択：
   - **.NET Framework: ProducerConsumer Console** - コンソールアプリのデバッグ
   - **.NET Framework: MultithreadDesignPattern Console** - デザインパターンサンプル
   - **.NET Framework: Web (Windows)** - Web アプリケーション

3. F5 を押してデバッグ開始

### .NET 8 プロジェクトのデバッグ（Linux/Windows/macOS）

1. デバッグ構成を選択：
   - **.NET 8: Web (移行後)** - Web アプリケーション
   - **.NET 8: SimpleWorker (移行後)** - Worker サービス
   - **.NET 8: MultithreadWorker (移行後)** - マルチスレッド Worker

2. F5 を押してデバッグ開始

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

### ビルド可能なプロジェクト

#### Windows のみ（.NET Framework 4.5）
- ✅ MultithreadDesignPattern (コンソール)
- ✅ ProducerConsumer.ConsoleApp (コンソール)
- ✅ ImageProcessor.Web (Web)
- ✅ ImageProcessor.SimpleWorker (Worker Role)
- ✅ ImageProcessor.MultithreadWorker (Worker Role)
- ✅ ImageProcessor.SearchWorker (Worker Role)
- ✅ ImageProcessor.Core (ライブラリ)
- ✅ ImageSearch (PCL)

#### Linux/WSL2
- ❌ 現在はビルド不可（.NET Framework のため）
- ⏳ .NET 8 への移行が必要

## .NET 8 への移行ステップ

### フェーズ 1: ImageSearch の移行（最も簡単）

ImageSearch は依存関係のない PCL プロジェクトなので、最初に移行するのに最適です。

1. **新しいプロジェクトファイルの作成**
   ```bash
   cd ImageSearch
   # 既存の .csproj をバックアップ
   mv ImageSearch.csproj ImageSearch.csproj.old
   # 新しい .NET 8 プロジェクトを作成
   dotnet new classlib -n ImageSearch -f net8.0
   ```

2. **既存のソースファイルをそのまま使用**（すでに存在するため、上書きしない）

3. **ビルドとテスト**
   ```bash
   dotnet build
   ```

詳細な移行手順は後で提供します。

### フェーズ 2: ImageProcessor.Core の移行

Azure ServiceRuntime 依存を削除する必要があります。

### フェーズ 3: Worker プロジェクトの移行

Worker Service テンプレートを使用します。

### フェーズ 4: Web プロジェクトの移行

ASP.NET Core に移行します。

## トラブルシューティング

### Windows: "msbuild が見つかりません"

```powershell
# Developer Command Prompt for VS 2022 を開く
# または PATH に MSBuild を追加
$env:PATH += ";C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin"
```

### Windows: "nuget が見つかりません"

```powershell
# chocolatey でインストール
choco install nuget.commandline

# または手動ダウンロード
# https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
# PATH に追加
```

### OmniSharp が起動しない

1. VSCode を再起動
2. OmniSharp のログを確認: `Ctrl+Shift+P` → "OmniSharp: Show OmniSharp Log"
3. .NET SDK が正しくインストールされているか確認

### Linux: .NET Framework プロジェクトがビルドできない

正常な動作です。.NET Framework は Windows 専用のため、Linux ではビルドできません。
.NET 8 への移行を進めてください。

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
   - Windows の場合: Visual Studio Build Tools のインストール
   - Linux の場合: .NET 8 SDK のインストール

2. **プロジェクトのビルド**
   - Windows で .NET Framework プロジェクトをビルド
   - 動作確認

3. **.NET 8 移行の開始**
   - ImageSearch プロジェクトから移行開始
   - ビルドとテストの実施

4. **段階的な移行**
   - Core → Workers → Web の順に移行
   - 各ステップでテストを実施

## 参考資料

- [VSCode C# 開発](https://code.visualstudio.com/docs/languages/csharp)
- [.NET 8 ドキュメント](https://learn.microsoft.com/ja-jp/dotnet/core/whats-new/dotnet-8)
- [ASP.NET Core 移行ガイド](https://learn.microsoft.com/ja-jp/aspnet/core/migration/proper-to-2x/)
- [VSCode タスク](https://code.visualstudio.com/docs/editor/tasks)
- [VSCode デバッグ](https://code.visualstudio.com/docs/editor/debugging)
