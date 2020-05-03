# Hatena2Hugo

## Hatena2Hugo とは

はてなブログ からエクスポートしたデータ（Movable Type 形式）を Hugo へインポートできる形式へ変換します。.NET Core 製のコンソールアプリです。

## 使い方

```
Usage: Hatena2Hugo [options...]

Options:
  -s, -src <String>         Source file (Required)
  -d, -dest <String>        Destination folder (Required)
  -f, -fotolife <String>    Download image iles from fotolife. (Default: true)
```

各記事にはそれぞれフォルダーが作成され、その下にテキストが index.md として保存されます。-f オプションが有効であれば、はてなフォトライフの画像もこのフォルダーにダウンロードされ、記事内の画像リンクの `src` がローカルリンクに書き換えられます。

- index.md
- 20151203000200.jpg
- 20151203002037.jpg
- ……

-f オプションを無効化するときは `false` と正しく入力してください。でないと、既定値の `true` と扱われます。

## 検証環境

Windows 10＋.NET Core 3.1 で開発し、Windows 10 で動作を確認しました。ほかの環境での動作は保証できませんが、たぶん動くんじゃないでしょうか。

はてなブログでエクスポートしたデータの記事部分は html 形式になっていますが、インポート先の Hugo でインデントが Markdown の引用として解釈されてしまう問題を回避するため、インデントは一律削除されています。そのため、`pre > code` 内のソースコードインデントも失われていますが、これはこのツールの制限です（修正予定はありません）。

## 謝辞

以下のライブラリを提供してくださった開発者に感謝します。

- https://github.com/Cysharp/ConsoleAppFramework 
