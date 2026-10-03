# Simple Java Property Editor

Java の `.properties` ファイルを、文字として読める状態で開いて保存する Windows アプリです。ソースコードはこのリポジトリにあります。ライセンスは MIT です。

## ダウンロード

64 ビット版の Windows で動きます。.NET のインストールは不要です。

[![Microsoft Store から入手](https://get.microsoft.com/images/ja-jp%20dark.svg)](https://apps.microsoft.com/detail/9ppclgjm1tmv?mode=direct)

[プライバシーポリシー](docs/privacy.html)

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


