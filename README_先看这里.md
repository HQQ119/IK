# IK 关节链作业

已在此项目中完成可运行场景，Unity 版本为2022.3.62f3c1。

## 最快开始

1. 打开 `Assets/IKDemo/Scenes/IK_Demo.unity`。
2. 点击 Unity 顶部 ▶ Play，进入 Game 页签。
3. 左侧选择关节数，点击“创建 / 重建关节链”。
4. 鼠标左键按住浅色末端 E 或红色目标十字拖动。

也可以双击 `Build/IK_Demo/IK_Demo.exe` 直接体验。运行程序时保留整个 `IK_Demo` 文件夹，不能只复制 exe。

## 已实现

- 2—20个转动关节可选，骨段长度可调。
- 真实的父子 Transform 关节链；根部位置固定，骨段长度固定。
- XY 平面拖动，关节绕 Z 轴旋转。
- CCD / FABRIK 两种算法切换；每个关节的局部角度实时显示。
- 不可达目标处理、末端误差、根部偏移和骨长误差显示。
- 暂停、单轮迭代、重置；R 重置，Space 暂停 / 继续。

N个转动关节包含根部，共N段骨骼；额外的末端点E不是转动关节。

## 学习资料

打开 `Documentation/关节动画与IK学习指南.html`，可直接阅读基本概念、课件页码、CCD / FABRIK 步骤、实现顺序、代码对应和演示说明；也有同名Markdown文件。

## 核心代码

- `Assets/IKDemo/Scripts/IKSolvers.cs`：CCD / FABRIK。
- `Assets/IKDemo/Scripts/IKChainDemo.cs`：链生成、鼠标拖拽、局部角度及界面。
- `Assets/IKDemo/Editor/IKProjectSetup.cs`：已保存场景的创建、检查和构建。
- `Assets/IKDemo/Scripts/IKRuntimeVerification.cs`：显式请求时运行的实际场景验证。

## 验证记录

`Verification/验收说明.md` 记录已实际运行的检查；JSON文件保留原始结果。顶栏 `IK` 菜单可以重新运行数学和Play模式检查。

完成日期：2026-11-02。
