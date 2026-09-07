# DynamicIslandWin 胶囊插件模板说明

灵动岛的所有胶囊都以“插件”形式存在于本目录（`plugins\`）中。

## 目录结构

plugins\
├── media\plugin.json            # 插件 1：媒体播放器（内置默认）
├── notification\plugin.json     # 插件 2：通知与时钟（内置默认）
├── hardware\plugin.json         # 插件 3：硬件监控（内置默认）
├── note\plugin.json             # 插件 4：记事备忘（内置默认）
├── _TEMPLATE\                   # 模板（下划线开头目录不会被加载，仅供复制参考）
│   ├── plugin.json              # ← 从这份文件复制并修改
│   └── README.txt
└── 其它插件文件夹\plugin.json

## plugin.json 字段格式

{
  "id": "插件唯一标识，英文小写，不能与已有插件重复，建议 my-xxx",
  "kind": "胶囊类型，只能是以下四种之一：media | notification | hardware | note",
  "name": "胶囊显示名称（设置面板、胶囊标题使用）",
  "icon": "胶囊图标（推荐单个 emoji，如 🎵 💬 📊 📝 📌）",
  "description": "一句话简介，显示在“可用预设胶囊”列表里"
}

注意：description 可省略；id / kind / name 必填。

## 如何添加自己的胶囊插件

1. 复制 `_TEMPLATE` 文件夹为例如 `my-note`；
2. 修改 `plugin.json`：填唯一的 id、把 kind 设为四类之一、自定义 name/icon；
3. 重启灵动岛 → 设置 → 胶囊交互设置 → “➕ 可用预设胶囊”里会出现你的插件；
4. 点击该行即可把它“添加”为独立胶囊，或点“设为主胶囊”。

## 引擎说明（当前版本约束）

- 每个 kind 对应一条物理胶囊（引擎为固定岛位：1 主胶囊 + 若干副胶囊），
  同一时刻每个 kind 只能有一个“生效插件”——后添加的同 kind 插件会顶替前一个生效插件，
  副胶囊移除后会自动还原为该 kind 的内置默认插件。
- kind=note 的文字内容在 设置 → 该卡片 的多行文本框中维护。
- 以“_”或“.”开头的文件夹、缺 plugin.json 或字段不合法的文件夹会被跳过并在日志记录。
- 插件目录随程序一起放在 exe 同级的 plugins\ 文件夹中；删除某个插件文件夹即可卸载该胶囊预设。
