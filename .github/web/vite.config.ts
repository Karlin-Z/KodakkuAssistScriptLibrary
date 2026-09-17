import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  // 部署在项目页 https://<user>.github.io/<repo>/ 下，base 不设的话
  // 打包出的 JS/CSS 会按站点根路径去请求，全部 404。
  base: '/KodakkuAssistScriptLibrary/',
  plugins: [vue(), tailwindcss()],
  define: {
    // vue-i18n 的 esm-bundler 构建用这三个开关做 tree-shaking（不定义会在构建时警告）：
    // 站点只用组合式 API，也不需要 intlify 的 devtools 集成。
    __VUE_I18N_FULL_INSTALL__: 'true',
    __VUE_I18N_LEGACY_API__: 'false',
    __INTLIFY_PROD_DEVTOOLS__: 'false',
  },
  build: {
    // 与 merge_repos.mjs 共用同一个站点目录：vite 先清空并写入 index.html 与资源，
    // 随后 merge_repos --no-html 再把机器可读的 index.json 放进来。
    // outDir 在项目根之外，必须显式开启 emptyOutDir 才允许清空。
    outDir: '../../_site',
    emptyOutDir: true,
  },
})
