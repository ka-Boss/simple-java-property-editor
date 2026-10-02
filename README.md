# Simple Java Property Editor

Java の `.properties` ファイルを、文字として読める状態で開いて保存する Windows アプリです。ソースコードはこのリポジトリにあります。ライセンスは MIT です。

## ダウンロード

64 ビット版の Windows で動きます。.NET のインストールは不要です。

入手は Microsoft Store を予定しています。ストア公開前のファイルは [Releases](../../releases) の `Simple Java Property Editor.exe` です。この EXE は未署名です。

## できること

- `.properties` を開く、上書き保存する、名前を付けて保存する
- ファイルをウィンドウへドラッグして開く
- 表示を切り替える
  - **ファイル形式** … ファイルと同じ並びのテキスト
  - **表形式** … キーと値の表
- `\u3053` のような表記は、画面では「こ」のように表示する
- 保存する文字コードを選ぶ
  - UTF-8
  - UTF-8（BOMつき）
  - Shift_JIS
- 上書き保存の直前に、今のファイルを `ファイル名.properties.bk` として1つ残す

文字コードは、画面下の「文字コード」から切り替えます。ファイルを開いたあとで選ぶと、その文字コードで読み直します。

## 起動時の注意

未署名の EXE は、Windows が起動を止めることがあります。署名付きで配るのは Microsoft Store の MSIX です。

## ビルド

EXE は GitHub Actions の `Build` ワークフローが作ります。`master` への push で、64 ビット Windows 向けの単一ファイルを出力します。
