import type { MessageSchema } from './zh-CN'

/**
 * English. 「卫月」is the Chinese name for Dalamud, so English text says Dalamud —
 * the framework is what English-speaking players know it as.
 *
 * 时间文案用了 vue-i18n 的复数写法（`单数 | 复数`），调用处会把数量一并传给 `t()`；
 * 注意正文里别出现撇号，消息语法里它是转义字符。
 */
const en: MessageSchema = {
  meta: {
    title: 'KodakkuAssist · Combat mechanic plugin for FFXIV Dalamud',
    description:
      'KodakkuAssist is a combat mechanic plugin for the FFXIV Dalamud framework. It subscribes to game events with C# scripts and draws mechanics at your feet or calls them out in your ear. This page introduces the plugin and links to the official online script library.',
  },

  common: {
    unknown: 'Unknown',
    copy: 'Copy',
    copied: 'Copied',
    copyFailed: 'Copy failed',
    copyAddress: 'Copy URL',
    listSeparator: ', ',
  },

  nav: {
    home: 'Home',
    examples: 'Examples',
    start: 'Start',
    contribute: 'Contribute',
    toTop: 'Back to top of home',
    label: 'Section navigation',
  },

  locale: {
    label: 'Language',
  },

  hero: {
    title: 'Draw mechanics {accent}, call them in your ear',
    accent: 'at your feet',
    lead: 'KodakkuAssist is a combat mechanic plugin for FFXIV running on the Dalamud framework. It draws the AoE ranges and safe spots that are otherwise invisible inside duties, and calls out mechanics by TTS, replacing the ageing ACT TTS setup.',
    getStarted: 'Get started',
    featuresLabel: 'Features',
  },

  features: {
    aoe: {
      title: 'AoE drawing',
      body: 'Draws AoE ranges and field safe zones with the same visual effects the game itself uses, so you can stop memorizing patterns and just move.',
    },
    tts: {
      title: 'TTS callouts',
      body: 'Warns you about duty mechanics through banner text at the center of the screen, chat messages and TTS voice callouts.',
    },
    customize: {
      title: 'Highly customizable',
      body: 'A complete scripting interface: write your own drawing scripts in C# to match exactly what you need.',
    },
    free: {
      title: 'Free forever',
      body: 'The plugin is free forever. Beware of any channel that charges you to get it!',
    },
  },

  stats: {
    users: 'Users',
    usersValue: '20,000+',
    scripts: 'Scripts',
    contributors: 'Contributors',
    /** 「副本」= duty：the territories a script declares support for */
    territories: 'Duties',
  },

  examples: {
    eyebrow: 'Examples',
    title: 'What the plugin looks like',
    shotAlt:
      'The script page of KodakkuAssist: script lists from four sources (official, favourites, online, local), searchable by name, author and duty, with the summary, user settings and method settings of the selected script on the right.',
  },

  start: {
    eyebrow: 'Get started',
    title: 'Up and running in three steps',
    lead: 'From installing the plugin to drawing your first AoE, in three simple steps',
    steps: {
      install: {
        title: 'Install the plugin',
        body: 'Add the plugin repository URL in the Dalamud plugin repository settings, then search for KodakkuAssist and enable it.',
      },
      verify: {
        title: 'Verify the plugin',
        body: 'Follow the prompts inside the plugin to complete verification in Discord and activate it.',
      },
      enable: {
        title: 'Enable scripts',
        body: 'Download and enable the drawing scripts you need for each duty.',
      },
    },
    pluginLibrary: 'Plugin repository URL',
  },

  contribute: {
    eyebrow: 'Contributing',
    title: 'Share your scripts',
    lead: 'KodakkuAssist lets you submit your own scripts so other players can use them. It only takes a few steps.',
    repo: 'GitHub repository',
    guide: 'Contribution guide',
    flow: 'Fork → open a PR → merged once the automated review passes',
    process: 'How to submit',
    resources: 'Developer resources',
    steps: {
      folder: {
        title: 'Create a folder',
        body: 'Fork this repository, create a folder at the root named after your GitHub username, and put your .cs script files in it.',
      },
      pr: {
        title: 'Open a PR',
        body: 'Only files inside your own folder may be changed. Open a pull request once you are done.',
      },
      merge: {
        title: 'Wait for the merge',
        body: 'Automated review checks the guid, version, file name and folder ownership. Once it passes, the PR is merged and the index is rebuilt.',
      },
    },
    files: {
      guide: 'The complete scripting guide. Read this before writing a script.',
      sample: 'The official sample template: copy it, change the guid, and start writing.',
      index: 'The online library index used by the official plugin subscription.',
    },
  },

  footer: {
    brand: 'KodakkuAssist',
    about:
      'The official online script library for the FFXIV Dalamud combat mechanic plugin. Both the library and the plugin are community projects and are not affiliated with Square Enix. Third-party plugins carry risk, so please use your own judgement.',
    sections: 'Sections',
    resources: 'Resources',
    discord: 'Discord',
    repo: 'GitHub repository',
    guide: 'Scripting guide',
    copyright: '© {year} KodakkuAssist',
  },

  time: {
    justNow: 'Just now',
    minutesAgo: '{n} minute ago | {n} minutes ago',
    hoursAgo: '{n} hour ago | {n} hours ago',
    daysAgo: '{n} day ago | {n} days ago',
  },

  card: {
    updated: 'Updated {time}',
    map: 'Map {id}',
    anyTerritory: 'Any duty',
    updateNote: 'Changes',
    copyGuid: 'Copy GUID',
    downloading: 'Downloading…',
    download: 'Download .cs',
  },

  errors: {
    indexHttp: 'Failed to load the index (HTTP {status})',
    indexShape: 'Malformed index: the top level is not an array',
  },
}

export default en
