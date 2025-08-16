# Unity AI Image Tagging Tool

CLIP（Contrastive Language-Image Pre-training）モデルを使用したUnityベースの画像タグ付けツールです。

## 🎯 機能

- **画像フォルダの一括読み込み**: JPG、JPEG、PNG形式の画像を自動検出
- **AI自動タグ付け**: CLIPモデルによる高精度な画像内容認識
- **タグフィルタリング**: 特定タグでの画像絞り込み機能
- **スコア閾値調整**: タグの信頼度による表示制御
- **CSV出力**: タグ付け結果の一括エクスポート
- **リアルタイムプレビュー**: 画像とタグの同時表示

## 🛠️ 技術スタック

- **Unity 2021.3+**: UIフレームワーク、画像処理
- **Python 3.12**: バックエンドサーバー
- **FastAPI**: RESTful API サーバー
- **CLIP**: OpenAI の画像-テキストモデル
- **PyTorch**: 機械学習フレームワーク

## 📋 セットアップ手順

### 1. リポジトリのクローン
```bash
git clone https://github.com/IAGP2025/UnityAI.git
cd UnityAI
```

### 2. Python環境のセットアップ
```bash
# Python仮想環境を作成
python -m venv .venv

# 仮想環境をアクティベート
source .venv/bin/activate  # macOS/Linux
# または
.venv\Scripts\activate     # Windows

# 依存関係をインストール
pip install -r requirements.txt
```

### 3. Pythonサーバーの起動
```bash
# プロジェクトルートディレクトリで実行
python demo_server.py
```

サーバーが `http://localhost:8000` で起動します。

### 4. Unity プロジェクトの設定

1. **Unity 2021.3以降** でプロジェクトを開く
2. **Scene** を開く
3. **RunCLIP** コンポーネントの設定：
   - `Progress Bar Fill`: プログレスバー用Image
   - `Progress Text`: プログレス表示用TextMeshPro
   - `Threshold Slider`: 閾値調整用Slider
   - `Threshold Value Text`: 閾値表示用TextMeshPro

## 🎮 使用方法

### 基本的な流れ
1. **Pythonサーバーを起動**
2. **UnityでPlayモードに入る**
3. **「フォルダ選択」ボタンで画像フォルダを選択**
4. **自動的に画像読み込み・タグ付けが実行される**
5. **タグフィルターで画像を絞り込み**
6. **スライダーで閾値を調整**
7. **CSVボタンで結果をエクスポート**

### フィルタリング機能
- **タグ選択**: 下部のタグボタンから1つ選択
- **閾値調整**: ヘッダーのスライダーで信頼度を設定
- **リアルタイム更新**: 条件に合う画像のみが表示される

## 📁 プロジェクト構造

```
UnityAI/
├── Assets/
│   ├── Clip/
│   │   ├── RunCLIP.cs          # メインUnityスクリプト
│   │   ├── ImageCard.cs        # 画像カードUI
│   │   ├── demo_server.py      # FastAPIサーバー
│   │   └── 要件定義書.md        # 仕様書
│   └── StreamingAssets/
│       └── tags.json           # タグ日英対応表
├── requirements.txt            # Python依存関係
└── README.md                  # このファイル
```

## 🔧 設定ファイル

### tags.json
日本語・英語のタグ対応表です。新しいタグを追加する場合は編集してください。

```json
{
  "cat": "猫",
  "dog": "犬",
  "person": "人物"
}
```

## 📊 対応フォーマット

- **画像**: JPG, JPEG, PNG
- **出力**: CSV形式（ファイル名, タグ, スコア）

## ⚙️ API仕様

### POST /classify
画像を送信してタグ付け結果を取得

**リクエスト:**
```json
{
  "image_b64": "data:image/jpeg;base64,/9j/4AAQSkZJRgABA...",
  "top_k": 3
}
```

**レスポンス:**
```json
{
  "top_k": [
    {
      "tag": "cat",
      "tag_ja": "猫",
      "score": 0.85
    }
  ]
}
```

## 🚀 開発・デバッグ

### ログの確認
Unityのコンソールで以下のログを確認できます：
- 画像読み込み進捗
- タグ付け結果
- フィルタリング状況
- サーバー通信状況

### よくある問題
1. **サーバーに接続できない**: `demo_server.py` が起動しているか確認
2. **画像が表示されない**: 対応フォーマット（JPG/JPEG/PNG）か確認
3. **スライダーが動かない**: Slider設定（Max Value: 1, Whole Numbers: OFF）を確認

## 📈 今後の拡張予定

- [ ] より多くの画像フォーマット対応
- [ ] カスタムタグの追加機能
- [ ] 画像メタデータの保存
- [ ] バッチ処理の高速化
- [ ] 実際のCLIPモデル統合

## 🤝 コントリビューション

1. このリポジトリをフォーク
2. フィーチャーブランチを作成 (`git checkout -b feature/amazing-feature`)
3. 変更をコミット (`git commit -m 'Add amazing feature'`)
4. ブランチにプッシュ (`git push origin feature/amazing-feature`)
5. プルリクエストを開く

## 📄 ライセンス

このプロジェクトは MIT ライセンスの下で公開されています。

## 🙏 謝辞

- [OpenAI CLIP](https://github.com/openai/CLIP) - 画像認識モデル
- [FastAPI](https://fastapi.tiangolo.com/) - Webフレームワーク
- [Unity](https://unity.com/) - ゲームエンジン・UIフレームワーク
