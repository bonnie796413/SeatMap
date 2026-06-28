import type { GlobalThemeOverrides } from 'naive-ui'

/**
 * Naive UI 全域主題覆寫 —— Mistral 奶油色調。
 * 在 App.vue 的 <n-config-provider :theme-overrides="themeOverrides"> 套用。
 */
export const themeOverrides: GlobalThemeOverrides = {
  common: {
    primaryColor: '#fa520f',
    primaryColorHover: '#ff6a2a',
    primaryColorPressed: '#cc3a05',
    primaryColorSuppl: '#ff6a2a',

    infoColor: '#1f1f1f',
    infoColorHover: '#2c2c2c',
    infoColorPressed: '#000000',
    infoColorSuppl: '#2c2c2c',

    successColor: '#1f9d57',
    successColorHover: '#27ab63',
    successColorPressed: '#177a43',

    warningColor: '#ff8105',
    warningColorHover: '#ffa110',
    warningColorPressed: '#cc3a05',

    borderRadius: '8px',
    borderRadiusSmall: '6px',

    textColorBase: '#1f1f1f',
    textColor1: '#1f1f1f',
    textColor2: '#4a4a4a',
    textColor3: '#6a6a6a',

    bodyColor: '#fff8e0',
    cardColor: '#fffaeb',
    modalColor: '#fffaeb',
    popoverColor: '#fffaeb',
    borderColor: '#e6d5a8',

    fontFamily:
      "Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', 'PingFang TC', 'Microsoft JhengHei', 'Noto Sans TC', sans-serif",
  },
  Button: {
    textColorPrimary: '#ffffff',
    borderRadiusMedium: '8px',
    borderRadiusSmall: '6px',
    fontWeight: '500',
  },
  Input: {
    borderRadius: '8px',
    color: '#fffaeb',
    colorFocus: '#fffaeb',
    border: '1px solid #e6d5a8',
    borderHover: '1px solid #fa520f',
    borderFocus: '1px solid #fa520f',
    boxShadowFocus: '0 0 0 1px #fa520f',
  },
  Tag: {
    borderRadius: '8px',
  },
}
