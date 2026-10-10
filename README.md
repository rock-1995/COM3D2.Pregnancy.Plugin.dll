# COM3D2 Pregnancy Plugin

COM3D2 的怀孕与孕肚变形插件。可以让女仆在游戏过程中受孕、按天推进孕期，也可以直接指定怀孕进度，调整肚形、肚脐和服装跟随效果。

[下载最新正式版](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [更新记录](CHANGELOG.md) · [编译说明](BUILDING.md)

## 主要功能

- **怀孕与生理周期**：提供简单概率、7 天周期和 28 天周期三种模式。可设置基础受孕概率和孕期长度，也可手动修改单个女仆的怀孕状态、孕期进度与周期位置。
- **随孕期变化的肚形**：腹部由小到大逐步变化，可以分别调整开始微隆、明显隆起和中期肚形出现的时间，以及大小、宽度、高度、前突程度和下坠程度。
- **腹部下坠**：`Belly sag` 让向前隆起的腹部向下移动，并保持肚脐局部的朝向。原有的表面滑动与疏密调节保留为 `Skin slide / density`。
- **渐进肚脐外翻**：从指定的怀孕进度开始逐渐凸出，到进度 100% 达到设定的完整效果；可以调整突出深度、位置、范围和比例。肚脐饰品会跟随变形后的肚脐移动。
- **服装跟随**：处理内衣、泳装、外衣、裙装、裤装以及「背中」类别的服饰。裙装有相应的垂坠处理；受小腿骨骼影响且不受裙骨控制的衣物部分保留原有运动，减少下摆被孕肚带起的问题。
- **动作与多角色支持**：腹部和衣物可随弯腰等姿势变化；场景中的各个女仆分别保留已应用的变形，切换角色或对另一人点击 Apply 不会取消已有角色的变形。
- **换装刷新与 AYS 兼容**：支持衣物显示、隐藏时自动刷新，重复刷新会复用未变化的计算结果；可与 AddYotogiSlider（AYS）的体型变形配合使用。

## 安装与更新

需要已能正常运行插件的 **BepInEx 环境**，以及提供女仆扩展存档功能的 **ExSaveData / ExternalSaveData 组件**。怀孕状态、孕期进度和生理周期数据通过该组件读写。

1. 关闭游戏，备份现有的插件 DLL 和设置文件。
2. 在 [Releases](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) 下载 `COM3D2.Pregnancy.Plugin.dll`。
3. 将 DLL 放入游戏目录的 `BepInEx/plugins/`，更新时替换旧文件。插件扫描目录中只保留一份该 DLL。
4. 启动游戏，让女仆出现在场景中，按 **F8** 打开主界面。

PDB 是可选的调试文件，正常使用只需要 DLL。更新会保留已有设置；想使用当前版本内置的肚形默认值，可在界面中点击 `Reset Defaults`，再点击 `Apply Belly`。

## 快速上手

### 手动设置孕肚

1. 按 **F8** 打开 `COM3D2 Pregnancy`。
2. 在顶部 `Maid` 下拉框选择当前场景中的女仆。列表没有更新时点击旁边的 `R`。
3. 勾选 `Pregnant`，拖动 `Growth stage` 滑条设置怀孕进度。`0` 是起始，`1` 是足月；旁边会显示对应天数。
4. 点击 **Apply Belly** 查看效果。
5. 在下方调整肚形或肚脐参数，再次点击 **Apply Belly** 应用并保存。

如果只想摆姿势或预览肚形，也可以不勾选 `Pregnant`，直接设置进度后点击 `Apply Belly`。勾选怀孕状态后，角色才会参与每日孕期推进和怀孕状态下的自动刷新。

### 让孕期随游戏推进

在插件配置中选择受孕模式、基础概率与孕期长度。检测到夜伽中的阴道内射事件后，插件按所选模式进行受孕处理；怀孕后在游戏每日结束时推进孕期。

孕期默认 **40 周，即 280 天**。到期后会自动结束怀孕状态；在周期模式下，生理周期随后继续推进。想直接查看某一阶段的外观，可以随时通过 F8 界面修改进度。

### 查看所有女仆的状态

按 **F9** 打开 `Pregnancy - Maid Status`。列表显示存档中女仆的姓名、怀孕天数或周期天数，怀孕中的女仆会用不同颜色标出。关闭后重新打开即可刷新列表。

## 受孕模式与基础配置

**默认是 `Simple` 简单模式，不是带生理周期的复杂模式。** 修改过设置的用户会沿用自己的已保存配置。

| 模式 | 行为 |
| --- | --- |
| `Simple` | 检测到受孕事件时，直接按基础受孕概率判定；不使用周期时机进行受孕计算。 |
| `SevenDay` | 使用 7 天周期，第 3 天为受孕窗口。事件发生后保留一个逐日衰减的受孕系数，在每日结束时结合周期位置和基础概率判定。 |
| `TwentyEightDay` | 使用 28 天周期，第 14 天为主要受孕窗口，第 15 天的周期系数为前一天的一半；同样在每日结束时结合逐日衰减的受孕系数判定。 |

周期模式下，怀孕会暂停周期推进，并将周期位置设到排卵后的阶段。F8 界面的 `Cycle Coefficient` 可以手动调整当前周期位置，旁边会显示周期天数。

基础配置保存在 `BepInEx/config/com.pregnancymod.com3d2.cfg`。如安装了 BepInEx 配置管理器，可在其界面中调整；也可以关闭游戏后编辑配置文件。

| 配置项 | 用途 | 默认值 |
| --- | --- | --- |
| `Toggle UI Key` | 打开或关闭主界面 | `F8` |
| `Maid List UI Key` | 打开或关闭女仆状态列表 | `F9` |
| `Pregnancy Weeks` | 孕期总长度，单位为周 | `40` |
| `Fertility Rate` | 基础受孕概率，`0.3` 表示 30%；周期模式还会乘上周期与衰减系数 | `0.3` |
| `Fertility Cycle Mode` | 受孕模式 | `Simple` |
| `Morph Trigger Mode` | 变形刷新方式 | `VisibilityChange` |

`VisibilityChange` 会在怀孕角色的身体或衣物显示状态变化时请求刷新。`ManualOnly` 关闭这类自动触发，需要用 `Apply Belly` 主动应用。

## 主界面操作

| 控件 | 操作与效果 |
| --- | --- |
| `Maid` | 选择当前场景中的女仆，并读取她的状态和已应用肚形。 |
| `R` | 重新扫描场景女仆并重新载入界面数值。未 Apply 的形状编辑会被重新载入的数值替换。 |
| `Pregnant` | 修改所选女仆的怀孕状态，不会自动把孕期进度清零。 |
| `Growth stage` | 修改孕期进度；调整后点击 `Apply Belly`，立即更新外观。 |
| `Cycle Coefficient` | 修改生理周期位置，数值范围为 `0–1`。 |
| `Apply Belly` | 将当前进度和形状参数应用到所选女仆，并保存当前形状参数。 |
| `Reset Belly` | 撤销所选女仆当前的孕肚变形；不清除怀孕状态、孕期进度或已保存的全局形状参数。 |
| `Reset Defaults` | 将形状编辑值恢复为插件内置默认值；再次点击 `Apply Belly` 才会应用并保存。 |

形状参数可以拖动滑条、直接输入数值，或用 `− / +` 微调。滑条仅提供建议范围，输入框可接受范围外的有限数值；小数使用 `.`。修改形状后需要点击 `Apply Belly`。

如果要同时取消怀孕状态和当前孕肚效果，取消 `Pregnant`，将 `Growth stage` 设为 `0`，再点击 `Apply Belly`。仅点击 `Reset Belly` 时，仍在怀孕的角色可能在之后的自动刷新中重新出现孕肚。

## 肚形参数

### 生长时间与整体外形

| 界面参数 | 可调整的效果 |
| --- | --- |
| `Subtle belly at progress` / `Visible belly at progress` / `Mid belly at progress` | 分别指定微隆、明显隆起和中期肚形出现在哪个孕期进度。只改变外观变化的节奏，不改变孕期总天数。三个值必须满足 `0 < Subtle < Visible < Mid < 1`。 |
| `Forward fullness` / `Belly width` | 腹部向前饱满程度与左右宽度。 |
| `Upper abdomen reach` / `Vertical influence range` | 上腹延伸程度与腹部上下影响范围。 |
| `Whole-abdomen smoothing` | 整个腹部外形的平滑程度。 |
| `Skin slide / density (legacy sag)` | 腹部表面的滑动与疏密分布，保留旧版 sag 的调节方式。 |
| `Belly sag` | 腹部下坠强度，向前隆起越明显的区域下移越明显；`0` 关闭。 |
| `Second-stage volume` | 较早的明显隆起阶段的体积。 |
| `Late lower-pole lift` / `Late upper settling` | 孕晚期下腹底部的抬升，以及上部的下沉程度。 |
| `Late whole-volume forward` / `All-stage axis tilt` | 孕晚期整体前移与各阶段的生长倾斜程度。 |
| `Late volume height / front-back depth / left-right width multiplier` | 分别调整孕晚期的高度、前后厚度与左右宽度。 |
| `Skin clearance` | 腹部外形的表面余量。 |

### 肚脐

| 界面参数 | 可调整的效果 |
| --- | --- |
| `Navel eversion` | 肚脐外翻强度；`0` 关闭外翻。 |
| `Navel change starts at pregnancy progress` | 开始变化的孕期进度，默认 **`0.4`，即 40%**。从这里持续渐变，到 `1` 才达到完整效果。 |
| `Navel protrusion depth` | 肚脐突出深度。 |
| `Navel vertical offset` | 肚脐上下偏移，负值向下。 |
| `Navel patch radius` | 肚脐局部调整的范围。 |
| `Navel proportion retention` | 肚脐局部比例的保持程度。 |
| `Preview full navel response at current belly size` | 在当前肚子大小下预览完整肚脐效果，会绕过渐进过程；默认关闭。 |
| `Navel visible preset` | 设置一组明显的肚脐参数，并开启完整预览。若要恢复随孕期渐变，应取消上面的预览选项，再 Apply。 |
| `Navel off` | 将外翻强度和比例保持设为 `0`；点击 Apply 后生效。 |

带有 `/ torso span` 的距离参数按角色躯干尺寸计算，不是厘米。界面中的 `Navel stage response` 显示当前进度对应的肚脐变化比例。

### 衣物、腿部保护与弯腰姿势

| 界面参数 | 可调整的效果 |
| --- | --- |
| `Clothing displacement` | 衣物随孕肚变形的位移程度。 |
| `Thigh Guard Speed` | 下腹向大腿过渡区域的保护变化速度；`0` 关闭这一项保护。 |
| `Inner Thigh Guard` | 大腿内侧相关区域的保护强度；`0` 关闭。 |
| `Thigh Guard Smooth` | 上述保护区域的过渡平滑；至少需要一项腿部保护大于 `0` 才有作用。 |
| `Virtual axis strength` | 腹部在弯腰等姿势下使用的额外跟随强度；`0` 使用原有骨骼跟随。 |
| `Blend start` / `Full blend` | 调整从原有动作跟随过渡到腹部跟随的范围。 |
| `Lower start / transition width / curve bias` | 下腹与骨盆之间的动作过渡起点、宽度和曲线。 |
| `Upper start / transition width / curve bias / join / activation` | 上腹与躯干之间的动作过渡及接合。 |
| `Pull at small angles` / `Pull at large angles` / `Full-pull angle` | 小角度和大角度弯曲时的牵引程度，以及达到大角度牵引的角度。 |
| `Anchor height` / `Anchor forward` | 腹部跟随锚点的上下、前后位置。 |
| `Exclude breast-weighted vertices` | 排除受乳房骨骼控制的身体区域，默认开启。 |
| `Upper abdomen bone-weight filter` | 根据骨骼影响限制上腹变形范围，默认关闭。 |
| `Extra shape-field top fade` | 对腹部变形的顶部增加衰减，默认关闭。 |

调整姿势相关参数时，可先摆出需要使用的弯腰姿势，再小幅修改并 Apply。不同服饰的版型与模型差异会影响贴合效果；出现局部穿模时，可以先调整衣物位移、腹部大小和姿势过渡。

## 设置保存

- **基础配置**：快捷键、受孕模式、概率和孕期长度等保存在 `BepInEx/config/com.pregnancymod.com3d2.cfg`。
- **肚形参数**：点击 `Apply Belly` 时写入 `BepInEx/plugins/pregnancy_settings.json`，下次启动时读取。只编辑数值或点击 `Log to BepInEx` 不会保存形状。
- **角色状态**：怀孕开关、孕期进度和周期数据通过 ExSaveData 按女仆保存，修改后仍需正常保存游戏。
- **多人场景的肚形**：当前运行中的女仆分别保留已应用参数；磁盘上的 JSON 保存的是最后一次 Apply 的全局参数，不是多角色预设库。

## 导出与问题反馈

界面底部提供以下操作，适合在需要保留参数或排查服饰问题时使用：

| 操作 | 输出内容 |
| --- | --- |
| `Log to BepInEx` | 将当前界面的形状参数写入 BepInEx 日志，便于记录或反馈。 |
| `Dump All Verts` | 导出所选女仆的全部顶点诊断数据。 |
| `Dump Skirt Verts` | 导出裙装相关变形数据。 |
| `Dump Upper Verts` | 导出上装相关变形数据。 |
| `Dump Navel Accessory` | 导出肚脐饰品的跟随数据。 |

导出文件保存在 `BepInEx/plugins/` 下的 `PregnancyDumps`、`PregnancySkirtDumps`、`PregnancyUpperDumps` 或 `PregnancyNavelDumps` 中，具体路径会写入日志。导出读取当前状态，不会替你点击 Apply 或 Reset；需要对比前后效果时，分别在未变形和已变形状态下导出即可。

反馈问题时，请附上插件版本、服饰或身体模型名称、怀孕进度、相关参数，以及出现问题时的截图或对应导出文件。

## 致谢

感谢 AddYotogiSlider（AYS）和 KK PregnancyPlus 的开发者，本项目参考了其中的处理方法。
