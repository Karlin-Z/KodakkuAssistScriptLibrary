/**
 * 简体中文是站点的源语言：这份文案是唯一手写的，其余语言都按它的结构翻译，
 * 键名对不上会在 `vue-tsc --noEmit` 里直接报错（见下面导出的 MessageSchema）。
 *
 * 写文案时注意 vue-i18n 的消息语法：`{name}` 是插值、`|` 是复数分隔符、`@` 是链接消息，
 * 正文里要用到这些字符得转义（`{'@'}`），英文里的撇号也尽量别用。
 */
const zhCN = {
  meta: {
    title: '可达鸭 KodakkuAssist · FF14 卫月战斗机制辅助插件',
    description:
      '可达鸭（KodakkuAssist）是 FF14「卫月」框架的战斗机制辅助插件：用 C# 脚本订阅游戏事件，把机制画在脚下、说在耳边。本页是插件介绍主页与官方在线脚本库入口。',
  },

  common: {
    unknown: '未知',
    copy: '复制',
    copied: '已复制',
    copyFailed: '复制失败',
    copyAddress: '复制地址',
    /** 列表连接符：中文用顿号，英文等用逗号 */
    listSeparator: '、',
  },

  nav: {
    home: '主页',
    examples: '示例',
    start: '开始',
    contribute: '贡献',
    toTop: '回到主页顶部',
    label: '板块导航',
  },

  locale: {
    label: '语言',
  },

  hero: {
    /** `{accent}` 会被换成带主题色的短语，位置由各语言自己决定 */
    title: '把战斗机制{accent}，报于耳边',
    accent: '画于脚下',
    lead: '可达鸭（KodakkuAssist）是 FF14「卫月」框架下的战斗机制辅助插件。它可以绘制副本内原本不可见的 AOE 范围以及场地安全区；同时可以通过 TTS 播报副本的机制，达到替代老旧 ACT 的 TTS 功能。',
    getStarted: '开始使用',
    featuresLabel: '功能',
  },

  features: {
    aoe: {
      title: 'AOE 绘制',
      body: '可使用游戏内同款 AOE 特效，绘制出副本内原本不可见的 AOE 范围以及场地安全区，让你放空大脑，自由走位。',
    },
    tts: {
      title: 'TTS 播报',
      body: '屏幕中央横幅文字、聊天文本、TTS 语音朗读等多种方式，来提醒你副本机制。',
    },
    customize: {
      title: '高度可定制化',
      body: '插件拥有完整的脚本定制接口，通过 C# 语言，你可以根据自己的需求，编写自己的绘制脚本。',
    },
    free: {
      title: '永久免费',
      body: '插件永久免费，小心提防所有需要付费获取的渠道！',
    },
  },

  stats: {
    users: '用户',
    /** 用户数是外部给的概数，不进索引；写法跟着语言走（中文说「万」，英文说 20,000） */
    usersValue: '2W+',
    scripts: '脚本',
    contributors: '贡献者',
    /** 这里的「副本」对应 ScriptEntry.TerritoryIds，即脚本声明支持的地图 */
    territories: '覆盖副本',
  },

  examples: {
    eyebrow: '示例',
    title: '插件长什么样',
    shotAlt:
      '可达鸭的脚本页：官方 / 收藏 / 在线 / 本地四个来源的脚本列表，可按名称、作者、副本搜索筛选，右侧是选中脚本的简介、用户设置与方法设置。',
  },

  start: {
    eyebrow: '开始',
    title: '三步就能用起来',
    lead: '从装插件到开始绘制 AOE，只需简单三步',
    steps: {
      install: {
        title: '安装插件',
        body: '在卫月的「在线插件库设置」里添加插件库订阅地址，然后搜索 KodakkuAssist 并启用。',
      },
      verify: {
        title: '认证插件',
        body: '跟随插件内的提示，在 Discord 中完成认证并激活插件。',
      },
      enable: {
        title: '启用脚本',
        body: '下载并启用你需要的副本绘制脚本。',
      },
    },
    pluginLibrary: '插件库订阅地址',
  },

  contribute: {
    eyebrow: '贡献',
    title: '分享你的脚本',
    lead: '可达鸭支持你提交自己的脚本，方便其他玩家共享使用，步骤很简单',
    repo: 'GitHub 仓库',
    guide: '贡献说明',
    flow: 'Fork → 提交 PR → 自动审核通过后合并',
    process: '提交流程',
    resources: '开发资源',
    steps: {
      folder: {
        title: '建目录',
        body: 'Fork 本仓库，在根目录新建以你的 GitHub 用户名命名的文件夹，把 .cs 脚本文件放进去。',
      },
      pr: {
        title: '提 PR',
        body: '只允许修改自己目录中的文件，修改完成后提交 PR。',
      },
      merge: {
        title: '等合并',
        body: '自动审核校验 guid、版本号、文件名与目录归属，通过即合并并重建索引。',
      },
    },
    files: {
      guide: '完整的脚本编写指南，写脚本前先看这份。',
      sample: '官方示例模板，复制下来改掉 guid 就能开工。',
      index: '插件官方订阅用的在线库索引。',
    },
  },

  footer: {
    brand: '可达鸭 KodakkuAssist',
    about:
      'FF14「卫月」战斗机制辅助插件的官方在线脚本库。本库与插件均为玩家社区作品，与 Square Enix 无关；使用第三方插件存在风险，请自行判断。',
    sections: '板块',
    resources: '资源',
    discord: 'Discord',
    repo: 'GitHub 仓库',
    guide: '脚本说明书',
    copyright: '© {year} 可达鸭 KodakkuAssist',
  },

  time: {
    justNow: '刚刚',
    minutesAgo: '{n} 分钟前',
    hoursAgo: '{n} 小时前',
    daysAgo: '{n} 天前',
  },

  card: {
    updated: '更新于 {time}',
    map: '地图 {id}',
    anyTerritory: '不限地图',
    updateNote: '更新说明',
    copyGuid: '复制 GUID',
    downloading: '下载中…',
    download: '下载 .cs',
  },

  errors: {
    indexHttp: '索引请求失败（HTTP {status}）',
    indexShape: '索引格式不正确：顶层不是数组',
  },
}

export default zhCN

/** 各语言文案的结构模板：少一个键、多一个键都会在类型检查时报出来。 */
export type MessageSchema = typeof zhCN
