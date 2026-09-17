import type { MessageSchema } from './zh-CN'

/**
 * 繁體中文（台灣用語）。除了字形轉換，用詞也按台灣習慣走：
 * 線上 / 設定 / 使用者 / 資料夾 / 檔案 / 儲存庫 / 透過 / 支援，而不是直接照搬簡中詞。
 */
const zhTW: MessageSchema = {
  meta: {
    title: '可達鴨 KodakkuAssist · FF14 衛月戰鬥機制輔助外掛',
    description:
      '可達鴨（KodakkuAssist）是 FF14「衛月」框架的戰鬥機制輔助外掛：用 C# 腳本訂閱遊戲事件，把機制畫在腳下、說在耳邊。本頁是外掛介紹首頁與官方線上腳本庫入口。',
  },

  common: {
    unknown: '未知',
    copy: '複製',
    copied: '已複製',
    copyFailed: '複製失敗',
    copyAddress: '複製位址',
    listSeparator: '、',
  },

  nav: {
    home: '首頁',
    examples: '範例',
    start: '開始',
    contribute: '貢獻',
    toTop: '回到首頁頂端',
    label: '區塊導覽',
  },

  locale: {
    label: '語言',
  },

  hero: {
    title: '把戰鬥機制{accent}，報於耳邊',
    accent: '畫於腳下',
    lead: '可達鴨（KodakkuAssist）是 FF14「衛月」框架下的戰鬥機制輔助外掛。它可以繪製副本內原本不可見的 AOE 範圍以及場地安全區；同時可以透過 TTS 播報副本的機制，達到取代老舊 ACT 的 TTS 功能。',
    getStarted: '開始使用',
    featuresLabel: '功能',
  },

  features: {
    aoe: {
      title: 'AOE 繪製',
      body: '可使用遊戲內同款 AOE 特效，繪製出副本內原本不可見的 AOE 範圍以及場地安全區，讓你放空大腦，自由走位。',
    },
    tts: {
      title: 'TTS 播報',
      body: '螢幕中央橫幅文字、聊天文字、TTS 語音朗讀等多種方式，來提醒你副本機制。',
    },
    customize: {
      title: '高度可自訂',
      body: '外掛擁有完整的腳本自訂介面，透過 C# 語言，你可以依照自己的需求，編寫自己的繪製腳本。',
    },
  },

  stats: {
    scripts: '線上腳本',
    contributors: '貢獻者',
    territories: '涵蓋地圖',
    updatedAt: '最近更新',
  },

  examples: {
    eyebrow: '範例',
    title: '外掛長什麼樣子',
    shotAlt:
      '可達鴨的腳本頁：官方 / 收藏 / 線上 / 本地四個來源的腳本清單，可依名稱、作者、副本搜尋篩選，右側是選中腳本的簡介、使用者設定與方法設定。',
  },

  start: {
    eyebrow: '開始',
    title: '三步就能用起來',
    lead: '從安裝外掛到開始繪製 AOE，只需簡單三步',
    steps: {
      install: {
        title: '安裝外掛',
        body: '在衛月的「線上外掛庫設定」裡加入外掛庫訂閱位址，然後搜尋 KodakkuAssist 並啟用。',
      },
      verify: {
        title: '認證外掛',
        body: '跟隨外掛內的提示，在 Discord 中完成認證並啟用外掛。',
      },
      enable: {
        title: '啟用腳本',
        body: '下載並啟用你需要的副本繪製腳本。',
      },
    },
    pluginLibrary: '外掛庫訂閱位址',
  },

  contribute: {
    eyebrow: '貢獻',
    title: '分享你的腳本',
    lead: '可達鴨支援你提交自己的腳本，方便其他玩家共享使用，步驟很簡單',
    repo: 'GitHub 儲存庫',
    guide: '貢獻說明',
    flow: 'Fork → 提交 PR → 自動審核通過後合併',
    process: '提交流程',
    resources: '開發資源',
    steps: {
      folder: {
        title: '建立資料夾',
        body: 'Fork 本儲存庫，在根目錄新增以你的 GitHub 使用者名稱命名的資料夾，把 .cs 腳本檔案放進去。',
      },
      pr: {
        title: '提交 PR',
        body: '只允許修改自己目錄中的檔案，修改完成後提交 PR。',
      },
      merge: {
        title: '等待合併',
        body: '自動審核會驗證 guid、版本號、檔名與目錄歸屬，通過即合併並重建索引。',
      },
    },
    files: {
      guide: '完整的腳本編寫指南，寫腳本前先看這份。',
      sample: '官方範例範本，複製下來改掉 guid 就能開工。',
      index: '外掛官方訂閱用的線上庫索引。',
    },
  },

  footer: {
    brand: '可達鴨 KodakkuAssist',
    about:
      'FF14「衛月」戰鬥機制輔助外掛的官方線上腳本庫。本庫與外掛均為玩家社群作品，與 Square Enix 無關；使用第三方外掛存在風險，請自行判斷。',
    sections: '區塊',
    resources: '資源',
    discord: 'Discord',
    repo: 'GitHub 儲存庫',
    guide: '腳本說明書',
    copyright: '© {year} 可達鴨 KodakkuAssist',
  },

  time: {
    justNow: '剛剛',
    minutesAgo: '{n} 分鐘前',
    hoursAgo: '{n} 小時前',
    daysAgo: '{n} 天前',
  },

  card: {
    updated: '更新於 {time}',
    map: '地圖 {id}',
    anyTerritory: '不限地圖',
    updateNote: '更新說明',
    copyGuid: '複製 GUID',
    downloading: '下載中…',
    download: '下載 .cs',
  },

  errors: {
    indexHttp: '索引請求失敗（HTTP {status}）',
    indexShape: '索引格式不正確：頂層不是陣列',
  },
}

export default zhTW
