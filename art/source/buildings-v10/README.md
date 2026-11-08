# 分阶段搭建、受损与燃爆 V10

本轮仅本地更新。成品外观沿用已验收的 V09，旧源文件、素材包与录像保留。

搭建为 72 帧刚性构件装配：基础、内部承重骨架、独立墙板、屋顶、设备依次安装。构件保持自身尺寸，通过短距离搬运和转正就位，不再缩放整个墙体。基地展开保留原工程车机构，并接入同一装配过程。游戏原有 1.2 秒装配用时、生产价格、占地、碰撞与单位参数保持不变。

受损分为三档。生命值低于 75% 时出现局部焦痕与烟；低于 50% 加入破窗、墙板缺口与小火；低于 25% 扩大缺口并增加火点。破损由同一建筑模型及相同相机绘制，采用不规则焦化、薄墙缺口和小幅弯折边板。保留势力色区域，维修后随生命值恢复相应外观。设备待机继续使用 V09，工厂另有受损卷帘门。

生命值耗尽后的表现为：外壳保持站立并持续燃烧，1.28 秒开始集中爆炸，1.43 秒在爆炸遮挡下移除建筑。加入白亮闪光、错开的次级火球、飞散火星和低位扬尘，最后只留下装饰性弹坑。这里的时间属于画面表现，不延迟死亡判定。结算面板等待燃爆演出完成。

施工中受毁时，先保持实际已建结构并燃烧，再爆炸并移除建筑，仅在地面留下装饰性弹坑，不会先出现完整建筑。弹坑只在地表装饰层绘制，不加入游戏对象，不占格、不挡路；旧残骸图仅作素材档案。

源目录保留早期结构分解试验及静态残骸，用于素材记录。当前游戏不播放这些序列，爆炸后移除建筑本体及阴影，只保留不占格的地表弹坑。弹坑位于单位和建筑绘制层之下，重新建设时会被正常遮挡，装饰数量有上限。

`construction.js` 保存刚性装配及归档碎片机构；`damage.js` 保存局部破损绘制。它们使用 V09 的尺寸与材质。离线导出使用 `export-structure-v10.cjs`、`export-damage-v10.cjs`，打包使用 `pack-structure-v10.py`。原生游戏不运行网页。

原生验收：`scripts/build.ps1 -StructurePreview`。完整回归：`scripts/build.ps1 -Test`。火焰与黑烟是固定大小的循环图集，源帧和显示缓存继续限制为各 64 MiB。

参考资料：

- [红警一代原版电站图集](https://www.spriters-resource.com/ms_dos/commandconquerredalert/asset/141534/)：完整／受损外观参考。
- [红警 2 模组作者发布的建筑受损对照](https://www.moddb.com/mods/scorched-earth-ra2-mod-with-smart-ai/images/working-on-buildings-again)：为 Scorched Earth 模组素材，不能当作红警 2 官方原版素材。
- [EA 开源红警一代建筑绘制](https://github.com/electronicarts/CnC_Red_Alert/blob/main/CODE/BUILDING.CPP)：按生命值切换建筑受损帧。
- [OpenRA 建筑规则](https://github.com/OpenRA/OpenRA/blob/bleed/mods/ra/rules/defaults.yaml)：受损状态、死亡爆炸与建造动画分开处理，属于重制实现。

上述外部图片用于观察，未作为本项目运行素材打包；本项目沿用自己的模型、涂装和特效。

火焰与黑烟各 96 帧，爆炸 32 帧，已预绘并嵌入程序。重新生成时设置 `GREYHAVEN_REBAKE_EFFECTS=1` 并执行 `-StructurePreview`，将导出的三张 hazard 图集保存到本目录 materials 后重新构建。
