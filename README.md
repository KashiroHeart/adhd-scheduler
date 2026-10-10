# adhd-scheduler

自分用の To Do アプリ（Windows / .NET 8 / WPF）。

- 実行画面（最前面）には、取り組む目標の「次の1手」の目的と理由だけを表示する
- 詰まったら［前段階追加］で、その1手の直前に新しい1手を割り込ませる
- 浮かんだ別の考えは `Ctrl+Shift+A` で「後でやる」に1行メモして、元の作業に戻る
- 目標・1手の作成や並べ替え、取り組む目標の切り替えは計画画面でのみ行う

締め切り・優先度・通知はありません。

## 構成

| パス | 内容 |
| --- | --- |
| `src/AdhdScheduler.Core` | モデル・SQLite 保存層・キュー操作・ViewModel |
| `src/AdhdScheduler.App` | WPF の画面・タスクトレイ・グローバルホットキー |
| `tests/AdhdScheduler.Tests` | xUnit テスト（保存層・キュー操作・ViewModel） |

データは `%LOCALAPPDATA%\AdhdScheduler\data.db` に保存されます。

## ビルドと実行

.NET 8 SDK が必要です。

```
dotnet build AdhdScheduler.sln
dotnet test
dotnet run --project src/AdhdScheduler.App
```

アプリは Windows でのみ動作します（テストは Linux / macOS でも実行できます）。

## 注意

`Ctrl+Shift+A` はグローバルホットキーとして登録するため、常駐中は Chrome（タブ検索）・Slack・Visual Studio などの同じキーの機能が使えなくなります。他のアプリが先に登録している場合は起動時にメッセージを表示し、ホットキーなしで起動します。
