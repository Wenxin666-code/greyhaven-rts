# 美术素材与制作记录

## 当前素材

- 单位：`source/units-v04` 与 `source/tank-prototype`；士兵步态使用 `source/unit-motion-v09`。
- 建筑：`source/buildings-v09` 保存模型与待机，`source/buildings-v10` 保存分构件搭建、局部破损与受损燃爆；`source/blast-v11` 补充烟火与独立弹片。
- [建筑模型与动画验收](review/buildings-v10/index.html) · [燃爆与弹片验收](review/blast-v11/index.html) · [早期单位素材归档](archive/unit-art-history-20261106/README.md)。
- 主菜单与操作界面沿用 `source/console` 的港区主视觉、金属纹理及本项目绘制的文字和按钮。

模型源文件、材质、正式资源包和制作脚本随源码保存。`source/**/frames` 是可重新导出的中间帧，仅本地保留；重新制作时先运行对应 `scripts/export-*`，再执行 `scripts/pack-*`。普通源码构建直接使用已保存的正式资源包，不需要中间帧或美术工具。

## 历史蓝本

[V02 港区演练场](review/index.html) 和 [V02 制作记录](source/v02/README.md) 为早期验收资料。以下保留 V01 静态蓝本的范围，不代表当前单位编制、尺寸或占格。

| 原稿 | 内容 | 验收重点 |
| --- | --- | --- |
| harbor.png | 港区战场与行军空间 | 找出公路、城区与货场，观察单位是否被地形细节淹没 |
| units.png | 步兵班、步战车、主战坦克 | 缩小后辨认轮式短炮与履带长炮，步兵能否读成一个班 |
| base.png | 指挥部、电站、车辆工厂 | 隐去名称时，能否借天线、排气筒和车库门认出用途 |

通过后先做街口原型所需的分件、地块、选中圈、阵营标识、转向与开火动作，再补兵营、工程车及建造状态。其他兵种、雷达与港区任务物件跟随战役开发。原稿中的单位尺寸、建筑占格与道路宽度需要在原型中校准。

## 灰港指挥台

`source/console/harbor.png` 是为主菜单绘制的灰港港区主视觉。`portraits.png` 和 `plate.png` 从已确认的界面效果图提取，分别用于六种单位的卡面和金属底纹。文字、按钮、订单进度、地形图和选中状态由桌面程序绘制。
