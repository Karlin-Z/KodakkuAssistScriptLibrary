import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  // 项目页子路径
  base: '/KodakkuAssistScriptLibrary/',
  plugins: [vue(), tailwindcss()],
  define: {
    // i18n 开关
    __VUE_I18N_FULL_INSTALL__: 'true',
    __VUE_I18N_LEGACY_API__: 'false',
    __INTLIFY_PROD_DEVTOOLS__: 'false',
  },
  build: {
    // 与索引共用目录
    outDir: '../../_site',
    emptyOutDir: true,
  },
})
