import type { MessageSchema } from './zh-CN'

/**
 * 日本語。用語は FF14 日本語版のプレイヤーになじみのある言い方に寄せています：
 * 副本 → コンテンツ、场地安全区 → 安置、机制 → ギミック、卫月（Dalamud の中国語名）→ Dalamud。
 *
 * ナビの文言は日本語だと長くなるため、ナビ用の短い語（画面例 / 投稿）を置いています。
 */
const ja: MessageSchema = {
  meta: {
    title: 'KodakkuAssist · FF14 Dalamud 向け戦闘ギミック支援プラグイン',
    description:
      'KodakkuAssist は FF14 のプラグインフレームワーク「Dalamud」向けの戦闘ギミック支援プラグインです。C# スクリプトでゲームイベントを購読し、ギミックを足元に描いたり音声で知らせたりします。このページはプラグインの紹介と公式オンラインスクリプトライブラリの入口です。',
  },

  common: {
    unknown: '不明',
    copy: 'コピー',
    copied: 'コピーしました',
    copyFailed: 'コピーに失敗',
    copyAddress: 'URL をコピー',
    listSeparator: '、',
  },

  nav: {
    home: 'ホーム',
    examples: '画面例',
    start: 'はじめに',
    contribute: '投稿',
    toTop: 'ホームの先頭へ戻る',
    label: 'セクションナビゲーション',
  },

  locale: {
    label: '言語',
  },

  hero: {
    title: 'ギミックを{accent}、耳元で知らせる',
    accent: '足元に描き',
    lead: 'KodakkuAssist は FF14 のプラグインフレームワーク「Dalamud」上で動作する戦闘ギミック支援プラグインです。コンテンツ内では本来見えない AoE 範囲や安置を描画し、TTS によるギミックの読み上げも行えるため、旧来の ACT の TTS 機能の代わりにもなります。',
    getStarted: '使ってみる',
    featuresLabel: '機能',
  },

  features: {
    aoe: {
      title: 'AoE 描画',
      body: 'ゲーム内と同じ AoE エフェクトを使い、コンテンツ内では本来見えない AoE 範囲や安置を描画します。パターンを覚える負担が減り、自由に動けます。',
    },
    tts: {
      title: 'TTS 読み上げ',
      body: '画面中央のバナー表示、チャットテキスト、TTS の読み上げなど、複数の方法でコンテンツのギミックを知らせます。',
    },
    customize: {
      title: '高いカスタマイズ性',
      body: 'スクリプト向けの API が一通り揃っています。C# で自分の用途に合わせた描画スクリプトを書けます。',
    },
  },

  stats: {
    scripts: 'オンラインスクリプト',
    contributors: 'コントリビューター',
    territories: '対応テリトリー',
    updatedAt: '最終更新',
  },

  examples: {
    eyebrow: 'スクリーンショット',
    title: 'プラグインの画面',
    shotAlt:
      'KodakkuAssist のスクリプト画面。公式 / お気に入り / オンライン / ローカルの 4 つのソースのスクリプト一覧を、名前・作者・コンテンツで検索して絞り込めます。右側には選択中のスクリプトの概要、ユーザー設定、メソッド設定が表示されます。',
  },

  start: {
    eyebrow: 'はじめに',
    title: '3 ステップで使い始められる',
    lead: 'プラグインの導入から AoE 描画の開始まで、簡単 3 ステップ',
    steps: {
      install: {
        title: 'プラグインを導入',
        body: 'Dalamud の「プラグインリポジトリ」設定にプラグインリポジトリの URL を追加し、KodakkuAssist を検索して有効化します。',
      },
      verify: {
        title: 'プラグインを認証',
        body: 'プラグイン内の案内に従い、Discord で認証を完了してプラグインを有効化します。',
      },
      enable: {
        title: 'スクリプトを有効化',
        body: '必要なコンテンツの描画スクリプトをダウンロードして有効化します。',
      },
    },
    pluginLibrary: 'プラグインリポジトリ URL',
  },

  contribute: {
    eyebrow: '投稿',
    title: 'スクリプトを共有する',
    lead: 'KodakkuAssist では自分のスクリプトを投稿して、他のプレイヤーと共有できます。手順はとても簡単です',
    repo: 'GitHub リポジトリ',
    guide: 'コントリビュートガイド',
    flow: 'Fork → PR を作成 → 自動レビュー通過後にマージ',
    process: '投稿の流れ',
    resources: '開発リソース',
    steps: {
      folder: {
        title: 'フォルダを作る',
        body: 'このリポジトリを Fork し、ルートに自分の GitHub ユーザー名のフォルダを作成して .cs スクリプトを入れます。',
      },
      pr: {
        title: 'PR を作成',
        body: '変更できるのは自分のフォルダ内のファイルだけです。変更が終わったら PR を作成します。',
      },
      merge: {
        title: 'マージを待つ',
        body: '自動レビューが guid、バージョン、ファイル名、フォルダの所属を検証します。通過するとマージされ、インデックスが再生成されます。',
      },
    },
    files: {
      guide: 'スクリプト作成の完全ガイド。書く前に必ず目を通してください。',
      sample: '公式のサンプルテンプレート。コピーして guid を書き換えればすぐ始められます。',
      index: 'プラグインの公式購読で使われるオンラインライブラリのインデックス。',
    },
  },

  footer: {
    brand: 'KodakkuAssist',
    about:
      'FF14 の「Dalamud」向け戦闘ギミック支援プラグインの公式オンラインスクリプトライブラリです。本ライブラリとプラグインはいずれもプレイヤーコミュニティによるもので、Square Enix とは関係ありません。サードパーティ製プラグインの利用は自己責任でお願いします。',
    sections: 'セクション',
    resources: 'リソース',
    discord: 'Discord',
    repo: 'GitHub リポジトリ',
    guide: 'スクリプトガイド',
    copyright: '© {year} KodakkuAssist',
  },

  time: {
    justNow: 'たった今',
    minutesAgo: '{n} 分前',
    hoursAgo: '{n} 時間前',
    daysAgo: '{n} 日前',
  },

  card: {
    updated: '{time} に更新',
    map: 'マップ {id}',
    anyTerritory: 'コンテンツ指定なし',
    updateNote: '更新内容',
    copyGuid: 'GUID をコピー',
    downloading: 'ダウンロード中…',
    download: '.cs をダウンロード',
  },

  errors: {
    indexHttp: 'インデックスの取得に失敗しました（HTTP {status}）',
    indexShape: 'インデックスの形式が不正です：最上位が配列ではありません',
  },
}

export default ja
