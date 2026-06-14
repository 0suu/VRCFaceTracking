## 基本方針

- 日本語で回答する。
- 結論から書く。
- お世辞や曖昧な励ましはいらない。
- 間違っていそうな前提、弱い設計、見落としは率直に指摘する。
- ただし、指摘だけで終わらず、具体的な次の行動や修正案を出す。

## このリポジトリでの現在の目標

- 目標は、`suu/fer-emotion-osc-helper` ブランチから VRCFaceTracking 本家へPRを出せる状態にすること。
- PR対象は、このブランチで追加している Facial Emotion OSC helper 機能全体。
- 本家 `origin/master` には現時点で表情解析/FER helper機能はないため、「既存本家機能の小修正」ではなく「Core内のopt-in helper OSC output追加」として扱う。
- この `AGENTS.md` はローカル作業メモであり、今回のPRには含めない。
- `artifacts/` 配下のビルド成果物も今回のPRには含めない。

## 背景

- GitHub Issue #358 で、本家メンテナから以下の方向性が示されている。
  - 外部Tracking ModuleからUnifiedTrackingを読んで後処理する使い方は想定されていない。
  - moduleから独自OSC出力する方式は推奨されない。
  - 追加helper OSC messageを出すなら、VRCFaceTracking Core側に入れる方針なら受け入れ可能。
  - 実装位置と設計が論理的で、regressionを起こさないことが重要。
- そのため、今回のPRは外部OSC proxyやmodule API拡張ではなく、Core内のoptional helper outputとしてまとめる。

## 実装範囲

- 出力パラメータは以下に限定する。
  - `/avatar/parameters/v2/FER_EmotionIndex`
  - `/avatar/parameters/v2/FER_EmotionPower`
- EmotionIndexは初回PRでは以下のみ扱う。
  - `0 = Neutral`
  - `1 = Joy`
  - `2 = Angry`
  - `3 = Sad`
  - `4 = Surprise`
- Issue本文にある `Shy`, `Smug`, `Sleepy` は今回のPRには含めない。
- 分類モードのUI設定は増やさない。
  - RuleBased / Template などのmode選択UIは作らない。
- Joy/Angry/Sad/Surprise の個別activation threshold設定は必須とする。
  - 表情ごとに必要な発火強度が違うため、初回PRでも単一thresholdへ戻さない。
- Neutral, Joy, Angry, Sad, Surprise のすべてが校正済みになるまでは helper output を無効にする。
  - 未校正または一部校正状態では `0 = Neutral`, `0.0 = Power` を出す。
  - RuleBased fallback は採用しない。

## 設計上の前提

- `Calibrate Neutral` はNeutral baselineを保存するためのもの。
- `Calibrate Joy/Angry/Sad/Surprise` は「この顔はその表情です」というユーザー別テンプレート保存として扱う。
- Template分類は直接ラベルを返さず、既存の `EmotionRawScores` 相当の `0..1` scoreを返す。
- UIで調整できる分類パラメータは `smoothing` と Joy/Angry/Sad/Surprise の個別activation thresholdに限定する。
- `switchMargin`, `holdTimeSeconds`, `calibrationDurationSeconds` はUI設定にせず、実装内の固定値として扱う。
- Surpriseだけ校正済みの状態では出力を有効にしない。Template分類は Neutral と4表情すべてが完備した場合だけ使う。
- Neutral baselineを再校正しても保存済み感情テンプレートは自動クリアしない。テンプレートを消す場合はResetを使う。
- 設定シリアライズ形式は現在の実装を正とし、旧設定に存在した削除済みフィールドは無視される前提で扱う。

## 既存の重要な修正

- `UnifiedTrackingMutator` の mutation load/init 順序修正は、起動時の collection modified クラッシュ対策。
- `ModuleProcessMain` の接続timeout延長は、LiveLink系moduleが `Initialize` 中に長く待つケースへの対策。
- これらはユーザーのローカル検証で必要になった修正なので、PRに含めるかどうかは最終的に差分整理時に確認する。

## 作業方針

- まず既存コードとプロジェクトの流儀を確認する。
- 変更は最小限にする。
- 不要なリファクタや大規模変更をしない。
- 新しい依存関係は、必要性が明確な場合だけ追加する。
- public API、設定ファイル、シリアライズ形式、ビルド設定を変える場合は、破壊的変更になりうることを明記する。
- Unity/C#ではライフサイクル、static event、UniTaskキャンセル、IL2CPP、Android/iOS差分に注意する。
- コードは賢すぎる抽象化より、読みやすく明示的な実装を優先する。

## 検証

- 最低限、以下を通す。
  - `dotnet build VRCFaceTracking.sln -c Release -p:Platform=x64 --no-restore`
  - `git diff --check`
- 手動確認では以下を見る。
  - 未校正では `FER_EmotionIndex = 0`, `FER_EmotionPower = 0.0` として出力される。
  - Neutralのみ、または一部感情のみ校正済みでも helper output は有効化されない。
  - Neutral/Joy/Angry/Sad/Surpriseすべて校正後にTemplate分類が有効化される。
  - Surprise校正後、同じ驚き顔で `3=Sad` ではなく `4=Surprise` が出る。
  - Reset後にテンプレートが消え、helper output が無効状態へ戻る。
