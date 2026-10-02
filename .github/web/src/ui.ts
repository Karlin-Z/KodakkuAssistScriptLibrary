/** 按钮外观只在这里写一份：AppButton（`<button>`）与 AppLink（`<a>`）共用，
 *  免得同一个样式在两处各写一半、改一处漏一处。 */

export type ButtonVariant = 'primary' | 'solid' | 'ghost'
export type ButtonSize = 'lg' | 'md' | 'sm' | 'xs'

/** 四个板块共用的内容宽度与左右留白。 */
export const PAGE_PAD = 'mx-auto max-w-[1080px] px-5 max-[620px]:px-4'

const BASE =
  'inline-flex cursor-pointer items-center justify-center gap-2 rounded-lg border font-medium whitespace-nowrap transition disabled:cursor-progress disabled:opacity-60'

const SIZE: Record<ButtonSize, string> = {
  lg: 'h-11 px-5',
  md: 'h-9.5 px-3.5',
  sm: 'h-8 px-3 text-[13px]',
  xs: 'h-6.5 px-2 text-[13px]',
}

/* primary 用主题色铺底、画布色作字：这是唯一在深色页面上「亮」起来的按钮。 */
const VARIANT: Record<ButtonVariant, string> = {
  primary: 'border-accent bg-accent text-canvas hover:border-accent-bright hover:bg-accent-bright',
  solid: 'border-line bg-surface hover:bg-subtle',
  ghost: 'border-transparent text-fg-muted hover:bg-subtle hover:text-fg',
}

export function buttonClass(variant: ButtonVariant = 'solid', size: ButtonSize = 'md'): string {
  return `${BASE} ${SIZE[size]} ${VARIANT[variant]}`
}
