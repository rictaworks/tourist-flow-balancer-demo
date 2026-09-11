# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Claude Safety Rules

### 削除系コマンドの禁止（重要）

以下のルールはこのワークスペース内のすべての会話で絶対に守られる：

- Claude はファイルまたはディレクトリを削除するコマンドを一切生成してはならない。
  例：rm, rm -rf, rm *, rmdir, unlink, cache --delete,
      lftp mirror --delete, rsync --delete, git clean -df, find -delete 等。

- 削除が必要な場合でも、Claude は削除コマンドを提案せず、
  「手動で削除してください」といった説明に留めること。

- 削除の推奨・削除操作の自動判断も禁止。

- ssh / lftp / デプロイ系スクリプトを生成する場合でも、
  削除コマンドの生成は禁止。

これらはすべての会話・コード生成に適用される。

### シークレット管理（重要）

- `config/master.key` など機密ファイルを `git add` するコードを生成してはならない
- デプロイスクリプト・セットアップ手順でも同様
- シークレットは必ず環境変数（RAILS_MASTER_KEY 等）で渡すこと
- `.gitignore` への追加を確認する手順を必ずコードに含めること
- 初回コミット前に `git status` でステージング確認を促すこと

---

## プロジェクト概要

観光客が1つの地域（中央観光地）に集中してオーバーツーリズムが起きる課題を題材に、プレイヤーが予算内の施策（混雑情報の掲示・クーポン・プロモーション・臨時バス・イベント・入場制限）で観光客の流れを6地域へ分散させる、Unity WebGL のルールベース戦略ゲームのデモ版展示物。**仕様の正は [requirements.md](requirements.md)。** 本ファイルはその要約であり、数値・アルゴリズムの詳細（配分モデルの数式、定数値、データ構造）は必ず requirements.md を参照する。

- サーバー・DB・外部通信・外部APIを一切持たない。ブラウザ内で完結する。
- 認証なし。ブラウザ内で不透明なオーナーIDを生成し、PlayerPrefs（WebGLではIndexedDB経由）の全キーの接頭辞にする。
- 観光客の行き先選択は決定的なルールベースの配分モデル。学習・推論モデル・外部AI APIは使用しない。
- デプロイ先は Unity Play のみ。独自ドメイン・Vercel/Railway/Cloudflare 等の Web インフラは持たない。

## 開発コマンド

- **ビルド**（CLI・バッチモード）：
  ```
  Unity.exe -batchmode -nographics -executeMethod BuildScript.BuildWebGL -quit
  ```
  `BuildScript.cs` は `Assets/Editor/` に置く（未実装。requirements.md 14.5節）。
- **テスト**：Unity Test Framework（NUnit）を使う。Editor から実行するほか、CLIでも実行できる（例：`Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults results.xml`）。単一テストのみ実行する場合は `-testFilter <クラス名または完全修飾テスト名>` を付ける。テストコードは `Assets/Scripts/` の各層に対応するテストアセンブリに置く（未実装のため、具体的な配置は実装時に確定する）。
- Unity Editor のライセンス有効化・実機ブラウザ確認の手順は Claude Desktop 側の `.claude/agents/unity-dev.md`（`20_開発`ワークスペース側）を参照。このリポジトリ単体には持たない。

## アーキテクチャ

すべて Unity WebGL・C# で完結し、シーン・UI・地図・観光客の点はすべて `GameBootstrap` が起動時にコードで動的生成する（Prefab・シーンファイルの手作業編集を前提としない。requirements.md 14.3節）。

リポジトリ構成（未実装。requirements.md 14.5節が正）：

```
Assets/Scripts/
├── Core/      # Season・Region・RegionState・Segment・EventDay・Plan・Measure（データモデル）
├── Logic/     # Allocator・DayEvaluator・DayAdvancer・SeasonFinalizer・PlanValidator・AutoPlanner（ルールベースの計算）
├── Infra/     # Persistence（PlayerPrefs保存）・SplitRng（乱数系列分離）・GameFlow（状態遷移）
├── UI/        # UIPresenter・各画面の動的生成
└── Bootstrap/ # GameBootstrap
Assets/Editor/BuildScript.cs  # CLIビルド用
```

### コアループ（関数A〜Hの責務。詳細は requirements.md 5章）

1. **`initSeason`（関数A）**：シードから出来事・対象地域・来訪数の揺らぎの3系列（`SplitRng`）を独立に生成する。乱数系列を分離しているため、施策の内容が出来事や来訪数に影響しない＝同一シードなら施策の違いだけが結果の違いになる（requirements.md 14.1節）。
2. **`allocateVisitors`（関数C, `Allocator`）**：セグメント別スコア（魅力×タグ一致×認知度×アクセス×価格×混雑回避の各係数）から最大剰余法でシェアを人数に変換する。**配分予測（確定前）と確定時の実績は必ず同一の関数を呼ぶ**——両者の差は来訪数の揺らぎだけに限定される（14.1節、恣意的に別ロジックを作らないこと）。混雑情報の掲示がある日は、混雑回避係数をすべて1.0とした一段階目の結果を「混雑予測」とし、それを使って二段階目を計算する（前日実績は使わない）。入場制限のあふれは `waterfall`（満員地域を候補から外しながら振り分け、繰り返し上限は地域数）で処理する。
3. **`validatePlan`（関数B, `PlanValidator`）**：予算超過・施策重複はエラー、閉鎖地域への施策や逆効果になる施策は警告、過密見込み・断念者見込みは助言。エラーのみ確定を妨げる。
4. **`evaluateDay`（関数D, `DayEvaluator`）**→**`advanceDay`（関数E, `DayAdvancer`）**：評価（住民感情・口コミ認知度更新）→認知度減衰→予算繰越→当日限りの効果解除→翌日公開→保存、の**順序を固定**する（14.1節。順序を変えると認知度・出来事の不整合が起きる）。
5. **`finalizeSeason`（関数F）**・**`autoPlan`（関数H, `AutoPlanner`）**・**`persist`/`resetIfNeeded`（関数G, `Persistence`）**：それぞれシーズン精算、おまかせプラン生成、PlayerPrefs保存とJST 03:00境界での日次リセットを担う。

### 設計上の不変条件（requirements.md 14.1節。実装・レビュー時に必ず確認する）

- 配分は同一関数で予測と実績を計算する。
- 混雑回避係数は連続関数（閾値で不連続に変化させない）。
- 人数の整数化は最大剰余法で、地域別合計は来訪数と必ず一致する。
- 乱数は出来事種類・対象地域・来訪数の3系列に分離する。
- 反発状態の入り（住民感情−50以下）と解除（−20以上）にヒステリシスを持たせる。
- 施策の変更は計画中（PLANNING状態）のみ受け付け、確定後は当日の配分を変更しない。

状態遷移（`GameFlow`）は `LOADING → TITLE → PLANNING ⇄ INFLOW → REPORT → PLANNING`（13日目以前）または `→ RESULT`（14日目）。詳細と地域・観光客個別の状態遷移図は requirements.md 11章。

## 開発フロー

- ブランチ運用：`Assets/**` の変更は必ずブランチを切って PR を作成する（main への直接 push 禁止）。`Assets/**` 以外（ドキュメント・`SPEC/`・`TASKS/`等）は main への直接 push を許可する。
- 1 issue のワンショット実装（requirements.md 1.4節）。
- リリースフロー（デモ版）：`issue → setting & coding → security review → add, commit, push → reviewer & pr-checker → merge →（Unity Playへの手動アップロード）→ user test`。code-review・audit・security-gate・正式release・reportは省略する。Unity WebGL は PR の merge では自動デプロイされないため、merge の直後に手動アップロード工程を挟む（`.claude/init-prompt.md` 参照、2026-09-12 本人確認）。
- コミット前にセキュリティレビューを行う。マージ前に reviewer・pr-checker を実行する。
- バージョン番号は `メジャー2桁.マイナー2桁.デバッグ2桁`（`01.01.00` が初期値）。タグは注釈付き（`git tag -a`）。
