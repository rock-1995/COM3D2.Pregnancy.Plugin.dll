# COM3D2 Pregnancy Plugin

为 COM3D2 加入怀孕过程和可自由调整的孕肚。女仆可以在夜伽后受孕，随着游戏天数推进逐渐显怀；你也可以随时打开界面，选好角色和孕期阶段，直接调出想要的外观。

[下载最新正式版](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) · [更新记录](CHANGELOG.md) · [编译说明](BUILDING.md)

## 可以怎么玩

**让怀孕随游戏自然推进。** 插件会识别夜伽中的受孕事件，并按设定的概率判定怀孕。可以使用简单概率模式，也可以加入 7 天或 28 天的生理周期，让受孕与周期时机有关。孕期长度可以自行设置，怀孕后每天都会推进进度，肚形也随之发展。

**按自己的喜好调整外观。** 可以从轻微隆起一直调到足月孕肚，控制腹部的大小、宽度、高度、前突程度，以及各个阶段何时出现。也能进一步调整上下腹的轮廓、下坠、表面过渡和肚脐细节。同一个孕期进度，可以搭配不同的形状设置。

**搭配服装和姿势使用。** 身体变形时，内衣、泳装、外衣、裙装、裤装以及「背中」类服饰会作相应调整，裙装会考虑垂坠，肚脐饰品也会跟随身体。腹部和衣物能随弯腰等动作变化，并提供跟随与过渡设置。支持与 AddYotogiSlider（AYS）的体型变形配合使用。

**分别设置场景中的女仆。** 每位女仆有自己的怀孕状态和进度。多人同场时，可以逐个选择、调整并应用肚形，其他人的已应用效果会保留。另有状态列表，方便查看存档中各位女仆的孕期或周期天数。

## 安装

需要能正常加载插件的 **BepInEx** 环境，以及提供女仆扩展存档功能的 **ExSaveData / ExternalSaveData** 组件。

1. 关闭游戏。更新旧版本时，先备份原来的 DLL 和设置。
2. 从 [Releases](https://github.com/rock-1995/COM3D2.Pregnancy.Plugin.dll/releases/latest) 下载 `COM3D2.Pregnancy.Plugin.dll`，放入游戏目录的 `BepInEx/plugins/`。插件目录中只保留一份该 DLL。
3. 启动游戏，让女仆出现在场景中，按 **F8** 打开设置界面。

正常使用只需 DLL，PDB 是可选的调试文件。更新会保留已有设置。

## 三个菜单怎么用

### F1：设置玩法规则

安装了 **BepInEx ConfigurationManager** 时，按 **F1** 打开配置菜单，找到 `COM3D2 Pregnancy`。想改受孕概率、选择简单或周期模式、缩短孕期，或决定穿脱衣物时是否自动刷新，都在这里设置。也可以修改 F8、F9 两个窗口的快捷键，以及开启或关闭排查问题用的日志。

这里的选项对整个插件生效，影响所有女仆。配置菜单由 ConfigurationManager 提供；如果它没有安装，仍可关闭游戏后直接编辑插件的配置文件。

### F8：调整当前女仆

按 **F8** 打开 `COM3D2 Pregnancy` 主界面。顶部选择场景中的女仆，设置她是否怀孕、处于哪个孕期阶段，以及当前生理周期位置。下方是肚形、衣物和动作跟随等参数，可以一边看角色一边调整。

改好外观后点击 **Apply Belly** 应用并保存；想撤销当前变形用 **Reset Belly**，想从内置形状重新调起用 **Reset Defaults**。界面可以向下滚动，底部还有记录参数和导出数据的按钮。换人后点击 `R` 重新扫描列表；它会重新载入数值，未 Apply 的形状编辑也会被替换。

### F9：查看所有女仆

按 **F9** 打开 `Pregnancy - Maid Status`，一次查看存档中各位女仆的状态，包括不在当前场景中的人。怀孕者显示已经过了多少天、总孕期多少天，并用不同颜色标出；未怀孕者显示周期天数。

这是状态总览，需要修改某位女仆时，让她进入场景后用 F8 调整。列表在打开时读取数据，关闭后重新打开即可刷新，也可以点右上角 `X` 关闭。`Simple` 模式下列表同样会显示周期天数，但受孕仍使用固定概率。

F8、F9 再按一次即可关闭对应窗口。上面列出的是默认快捷键；如果改过，以自己保存的设置为准。

## 先试一试

按 **F8** 打开主界面，在 `Maid` 下拉框中选择女仆，然后：

1. 勾选 **Pregnant**，设置她为怀孕状态。
2. 拖动 **Growth stage** 选择孕期进度。`0` 为起始，`1` 为足月，旁边会显示对应天数。
3. 点击 **Apply Belly**，查看身体与服装的变化。
4. 调整下方的形状参数，再点 **Apply Belly**，直到满意。

如果只是想拍照或预览外观，也可以不勾选 `Pregnant`，直接选进度并 Apply。只有设为怀孕的女仆才会参与每日孕期推进。

## 受孕和孕期

新配置默认使用 **Simple 模式、30% 基础受孕概率、40 周孕期**。这些都可以调整；已有配置会沿用你之前保存的值。

| 模式 | 受孕方式 |
| --- | --- |
| `Simple` | 检测到夜伽中的阴道内射事件时，直接按基础概率判定怀孕。 |
| `SevenDay` | 使用 7 天周期，第 3 天为受孕窗口。事件发生后保留逐日衰减的受孕系数，每日结束时结合周期时机和基础概率判定。 |
| `TwentyEightDay` | 使用 28 天周期，第 14 天为主要受孕窗口，第 15 天的周期系数减半；同样结合逐日衰减的受孕系数，在每日结束时判定。 |

怀孕后，每到游戏一天结束就会推进孕期。默认 40 周对应 280 天，也可以缩短或延长。周期模式下，怀孕期间会暂停生理周期；孕期结束后自动取消怀孕状态，周期继续推进。

你可以随时通过 F8 界面的 `Pregnant`、`Growth stage` 和 `Cycle Coefficient` 手动修改怀孕状态、孕期进度及周期位置。修改外观进度后，点一次 `Apply Belly` 即可看到结果。

基础选项可在 BepInEx 配置管理器中修改；没有配置界面时，可以关闭游戏后编辑 `BepInEx/config/com.pregnancymod.com3d2.cfg`。

<details>
<summary>基础配置项对照</summary>

| 配置项 | 用途 | 默认值 |
| --- | --- | --- |
| `Toggle UI Key` | 主界面快捷键 | `F8` |
| `Maid List UI Key` | 女仆状态列表快捷键 | `F9` |
| `Pregnancy Weeks` | 孕期长度，单位为周 | `40` |
| `Fertility Rate` | 基础受孕概率，`0.3` 表示 30%；周期模式还会乘上周期与衰减系数 | `0.3` |
| `Fertility Cycle Mode` | 上述三种受孕模式 | `Simple` |
| `Morph Trigger Mode` | 变形刷新方式 | `VisibilityChange` |

`VisibilityChange` 会在怀孕角色的身体或衣物显示状态变化时刷新变形，例如穿脱衣物。`ManualOnly` 关闭这类自动触发，由你点击 `Apply Belly` 应用。

</details>

## 调整成喜欢的肚形

可以先选一个便于观察的孕期进度，调整整体大小和轮廓，再换几个阶段看看生长过程。除了拖动滑条，参数也支持直接输入数值和用 `− / +` 微调；每次修改后点击 **Apply Belly** 应用。

- **大小和轮廓**：调整腹部的前突、宽度、上下范围和上腹延伸，也可以分别改变孕晚期的高度、厚度与宽度。
- **生长节奏**：设置微隆、明显隆起和中期肚形出现在哪个进度。例如让显怀更早，或让明显增大的阶段更靠后。这不改变孕期总天数。
- **局部形状**：调整下腹底部、上腹下沉、整体倾斜、下坠和表面平滑；肚脐的位置、范围及外翻程度也可以单独设置。
- **衣服和动作**：调整衣物位移、腹部在弯腰时的跟随程度，以及与骨盆、上身之间的过渡。建议在准备使用的姿势下观察并微调。

滑条是建议范围，输入框可以填写范围外的数值，小数使用 `.`。如果想重新开始，点击 **Reset Defaults** 恢复内置形状，再点击 **Apply Belly**。

<details>
<summary>形状参数对照</summary>

| 界面参数 | 调整内容 |
| --- | --- |
| `Subtle belly at progress` / `Visible belly at progress` / `Mid belly at progress` | 三个生长阶段的位置，需满足 `0 < Subtle < Visible < Mid < 1`。 |
| `Forward fullness` / `Belly width` | 向前饱满程度与左右宽度。 |
| `Upper abdomen reach` / `Vertical influence range` | 上腹延伸与上下影响范围。 |
| `Second-stage volume` | 较早的明显隆起阶段的体积。 |
| `Late volume height / front-back depth / left-right width multiplier` | 孕晚期的高度、前后厚度与左右宽度。 |
| `Late lower-pole lift` / `Late upper settling` | 孕晚期下腹底部的抬升与上部下沉。 |
| `Late whole-volume forward` / `All-stage axis tilt` | 孕晚期整体前移与各阶段的生长倾斜。 |
| `Belly sag` | 腹部下坠，向前隆起越明显的区域下移越明显，并保持肚脐局部朝向；`0` 关闭。 |
| `Skin slide / density (legacy sag)` | 腹部表面滑动与疏密分布。 |
| `Whole-abdomen smoothing` / `Skin clearance` | 腹部外形平滑与表面余量。 |
| `Navel eversion` / `Navel protrusion depth` | 肚脐外翻强度与突出深度，外翻强度为 `0` 时关闭外翻。 |
| `Navel change starts at pregnancy progress` | 肚脐开始变化的进度，默认 `0.4`。从这里逐渐变化，到进度 `1` 达到设定的完整效果。 |
| `Navel vertical offset` / `Navel patch radius` / `Navel proportion retention` | 肚脐上下偏移、局部范围与比例保持程度；偏移负值向下。 |
| `Clothing displacement` | 衣物随孕肚变化的位移程度。 |
| `Thigh Guard Speed` / `Inner Thigh Guard` | 下腹向大腿过渡区域及大腿内侧的保护；各项设为 `0` 可关闭。 |
| `Thigh Guard Smooth` | 腿部保护区域的过渡平滑，需要至少一项腿部保护大于 `0`。 |

`Navel visible preset` 会设置一组明显的肚脐效果，并开启 `Preview full navel response at current belly size`。该预览选项会绕过孕期渐变；要恢复正常渐变，取消它后再 Apply。`Navel off` 会将外翻强度和比例保持设为 `0`，也需要 Apply 才生效。

带有 `/ torso span` 的距离参数按角色躯干尺寸计算，不是厘米。

</details>

<details>
<summary>动作跟随与变形范围</summary>

| 界面参数 | 调整内容 |
| --- | --- |
| `Virtual axis strength` | 弯腰等姿势下的额外腹部跟随强度；`0` 使用原有骨骼跟随。 |
| `Blend start` / `Full blend` | 从原有动作跟随过渡到腹部跟随的范围。 |
| `Lower start / transition width / curve bias` | 下腹与骨盆之间的过渡起点、宽度和曲线。 |
| `Upper start / transition width / curve bias / join / activation` | 上腹与躯干之间的过渡与接合。 |
| `Pull at small angles` / `Pull at large angles` / `Full-pull angle` | 不同弯曲角度下的牵引程度，以及达到大角度牵引的角度。 |
| `Anchor height` / `Anchor forward` | 腹部跟随锚点的上下和前后位置。 |
| `Exclude breast-weighted vertices` | 排除受乳房骨骼控制的身体区域，默认开启。 |
| `Upper abdomen bone-weight filter` | 按骨骼影响限制上腹变形范围，默认关闭。 |
| `Extra shape-field top fade` | 增加腹部变形顶部的衰减，默认关闭。 |

不同服饰的版型和模型会影响贴合效果。调整时可结合腹部大小、衣物位移与姿势过渡观察局部穿模情况。

</details>

## 应用、撤销和保存

**Apply Belly** 会应用当前设置，并把形状参数保存到 `BepInEx/plugins/pregnancy_settings.json`，下次启动时读取。只在输入框里改数值，还没有保存；`Log to BepInEx` 也只是记录参数。

**Reset Belly** 撤销所选女仆当前的孕肚效果，但保留怀孕状态和进度，所以之后自动刷新时可能再次出现孕肚。如果要一起取消怀孕和外观效果，取消 `Pregnant`，把 `Growth stage` 设为 `0`，再点 `Apply Belly`。**Reset Defaults** 则只恢复形状编辑值，需要再次 Apply 才会应用并保存。

怀孕状态、孕期和周期数据通过 ExSaveData 按女仆保存，修改后仍需正常保存游戏。多人场景中的已应用肚形会分别保留，但磁盘上的 JSON 只保存最后一次 Apply 的全局形状参数。更新插件不会覆盖已有设置。

<details>
<summary>记录参数与反馈问题</summary>

点击 `Log to BepInEx` 可以把当前界面里的形状参数写入日志。需要排查服饰或饰品问题时，还可使用：

| 按钮 | 导出内容 |
| --- | --- |
| `Dump All Verts` | 全部顶点诊断数据 |
| `Dump Skirt Verts` | 裙装相关数据 |
| `Dump Upper Verts` | 上装相关数据 |
| `Dump Navel Accessory` | 肚脐饰品的跟随数据 |

文件保存在 `BepInEx/plugins/` 下相应的 `Pregnancy*Dumps` 目录中，具体路径会写入日志。导出读取当前状态，不会改变变形；需要对比时，可分别在未变形和已变形状态下导出。

反馈时请提供插件版本、服饰或身体模型名称、孕期进度、相关参数和问题截图；有对应导出文件时也可一并附上。

</details>

感谢 AddYotogiSlider（AYS）和 KK PregnancyPlus 的开发者，本项目参考了其中的处理方法。
