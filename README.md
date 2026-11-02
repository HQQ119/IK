# IK 关节链演示

Unity 课程项目：根部位置固定，鼠标拖动末端，通过逆向运动学求解关节角度。

## 打开项目

1. 下载或克隆本仓库，在 Unity Hub 中选择添加本地项目，指向仓库根目录。
2. 使用 Unity **2022.3.62f3c1** 打开（版本记录在 `ProjectSettings/ProjectVersion.txt`）。首次打开需要等待包恢复和资源导入。
3. 打开 `Assets/IKDemo/Scenes/IK_Demo.unity`。
4. 点击 Play，进入 Game 页签，按住鼠标左键拖动末端 E 或红色目标十字。

## 功能

- 2—20个转动关节可选，每段骨骼长度可调。
- 真实的 Transform 父子层级；根部位置固定，骨长保持不变。
- XY 平面拖动，关节绕 Z 轴旋转。
- CCD 与 FABRIK 两种 IK 算法可切换，实时显示关节局部角度。
- 显示末端误差、根部偏移、骨长误差，并处理不可达目标。
- 支持暂停、单轮迭代和重置。`R` 重置，`Space` 暂停或继续。

N个转动关节包含根关节，对应N段骨骼；额外的末端位置点E不计入转动关节数。

## 主要代码

- [IKSolvers.cs](Assets/IKDemo/Scripts/IKSolvers.cs)：CCD / FABRIK 核心求解。
- [IKChainDemo.cs](Assets/IKDemo/Scripts/IKChainDemo.cs)：链生成、鼠标拖拽、角度恢复和界面。
- [IKProjectSetup.cs](Assets/IKDemo/Editor/IKProjectSetup.cs)：场景创建、检查及Windows构建菜单。
- [IKRuntimeVerification.cs](Assets/IKDemo/Scripts/IKRuntimeVerification.cs)：显式请求时运行的场景检查。

## 学习与检查

- [学习指南](Documentation/关节动画与IK学习指南.md)：概念、公式、算法步骤和代码对应关系。
- [验收说明](Verification/验收说明.md)：数学、场景和真实鼠标交互检查记录。
- Unity 顶栏 `IK` 菜单可重新运行检查，也可以自行构建 Windows 演示程序。

仓库保留 `Assets`、`Packages`、`ProjectSettings` 和说明文档。`Library`、`Temp`、`Build` 等缓存及构建产物由 `.gitignore` 排除，打开项目时会按需重新生成。
