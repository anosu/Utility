# 构建与工程维护

项目入口：`src/Utility/Utility.csproj`。标准方案：`Utility.slnx`。

## 准备

安装 global.json 指定的 .NET SDK、.NET 8 测试运行时、Python 3.10+、PowerShell 7。VS 使用 2022 17.14 或更新版本。

```sh
git submodule update --init --recursive
python shared/ModEngineering/scripts/mod.py check
python shared/ModEngineering/scripts/mod.py test
python shared/ModEngineering/scripts/mod.py build --configuration Debug
python shared/ModEngineering/scripts/mod.py build --configuration Release
```

引用只使用游戏/加载器必要 DLL；具体资源和游戏差异见 [dependencies](../dependencies/README.md)（若项目未提供该文件，以项目的 Reference 声明为准）。完整游戏导出和本机路径不提交。

## 本地开发

标准方案包含本库源码和全部测试。消费者通过自己的本地方案引用本仓库；本库不需要配置指向自身的 SharedDependencies.local.props。

## 规范与升级

`mod.json` 是项目工程清单，声明平台、项目、测试及发行文件。重复构建逻辑来自固定的 ModEngineering 子模块。生成文件改动应在公共实现或清单中完成，然后执行 `mod.py sync`；`mod.py check` 检测漂移和格式问题。

更新工程用 `mod.py update --revision <commit>`，更新运行库增加 `--dependency Utility`。更新后验证并提交子模块指针。

详见 [公共规范](../shared/ModEngineering/docs/CONVENTIONS.md)。

共享库不自动发布 Release。消费者通过固定源码引用并随 Mod 包携带 DLL。
