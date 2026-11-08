# 建筑动作与状态衔接 V09

保留 V08 全部源文件和图集。当前程序读取 buildings-v09 与 unit-motion-v09，仍为离线单文件窗口游戏。

军用建筑待机扩为 96 帧、24 帧每秒的四秒循环：雷达整周扫描、卫星转向、风扇、军用旗帜、桥架与独立小车、矿料输送。中立房屋主体保持静止，只保留克制灯光。

双烟囱排烟、卸矿粉尘、蒸汽、焊接和状态灯由真实模型挂点发出，工作状态服从订单、暂停、资金、供电与卸货。完工后逐渐启动，停电原位停止，恢复供电平滑续转。烟柱逐步生成；暂停时全部冻结。

工程车先停车转正，再播放 96 帧机构展开：支腿、履带支撑、侧模块、门式吊臂、车体收拢及建筑升起，收起倒放同一序列。保留血量比例、订单与原有 1.2 秒逻辑用时；转正可取消。主菜单展开慢放用 3.2 秒展示，验收录像对应片段标明半速。

士兵沿用已确认模型，新增八方向十二帧的髋、膝和脚掌姿态，按实际行进距离选帧并平滑启停。尺寸、移速、碰撞、装备和涂装保持不变，预览可切换行走。另补充弹壳、枪口余烟以及上部结构受损烟火。

kit.js 与 models.js 保存机构，bake.js 固定投影，models 目录保存可重载显式几何和自定义 UV。人物来源为既有 V04，材质沿用已有生成记录，本轮未改画风。

设置 PLAYWRIGHT_MODULE 后执行 node scripts/export-building-v09.cjs --resume，再执行 python scripts/pack-building-v09.py。node scripts/check-building-v09.cjs 验证重载与循环。步态分别使用 export-gait-v09.cjs 与 pack-gait-v09.py。

scripts/build.ps1 -MotionPreview 导出真实窗口录像，需 GREYHAVEN_FFMPEG；-Test 包含状态、颜色、默认比例可见性及原有玩法回归。源帧和最终画面缓存各限 64 MiB；命中显示缓存时跳过重复解码；启停混合另有 128 项小图上限。

状态分层参考 [OpenRA 待机文档](https://docs.openra.net/en/release/traits/#withidleoverlay)、[施工序列文档](https://docs.openra.net/en/release/traits/#withmakeanimation) 和 [EA 开源建筑实现](https://github.com/electronicarts/CnC_Remastered_Collection/blob/master/REDALERT/BUILDING.CPP)。模型、粒子与动作独立制作，没有复制外部游戏素材。本轮未上传 GitHub。
