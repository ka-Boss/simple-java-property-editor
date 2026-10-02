# ビルド

EXE と Microsoft Store 提出用の MSIX は、GitHub Actions の `Build` ワークフローが作ります。

`master` への push で動きます。対象は 64 ビット Windows の単一ファイルです。成果物は Actions の Artifacts に出ます。

- EXE: `publish/SimpleJavaPropertyEditor.exe`
- MSIX: `packaging/SimpleJavaPropertyEditor.msix`

ワークフロー定義は `.github/workflows/build.yml` です。
