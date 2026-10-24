<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.png" alt="PCL Ap" width="100" height="100">

# PCL Ap

由 [Alec-Apu](https://github.com/Alec-Apu) 维护的 Minecraft Java 版启动器定制项目。

[源码](https://github.com/Alec-Apu/PCL_Alec) · [发行版](https://github.com/Alec-Apu/PCL_Alec/releases) · [反馈问题](https://github.com/Alec-Apu/PCL_Alec/issues)

</div>

## 项目介绍

PCL Ap 基于 PCL-XH / PCL 社区版二次开发，使用 PCL Ap 名称和 Ap 图标。仓库名为 PCL_Alec，生成程序为 PCL Ap.exe。

本项目由 Alec-Apu 独立维护，与原版及上游社区不存在隶属关系。定制版的问题请提交到本项目。

## 环境要求

- Windows 10 1809 或更新版本，建议 Windows 10 / 11。
- 框架依赖版需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)。
- 编译需要 .NET 10 SDK；Visual Studio 用户安装“.NET 桌面开发”工作负载。
- 游戏运行需要匹配版本的 Java。

## 编译

```powershell
git clone --branch dev https://github.com/Alec-Apu/PCL_Alec.git
cd PCL_Alec
dotnet build "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Release -p:Platform=x64
```

输出位于 `Plain Craft Launcher 2/bin/Release/net10.0-windows/win-x64/PCL Ap.exe`。

发布命令：

```powershell
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" -c Release -p:Platform=x64 --self-contained false -o ./publish
```

请使用完整的 publish 产物；普通 build 目录中的 exe 不能脱离依赖单独分发。

## 外部服务

本次以 [PCL-Alec 的 dev 分支](https://github.com/Alec-Apu/PCL-Alec/tree/dev) 为基线修改品牌、图标和说明，不自动包含其他本地目录的功能修改。

- Modrinth 公开搜索和下载无需账号或 API Key。
- 当前源码仍包含微软登录，需要自行配置 OAuth Client ID 和相应服务访问资格。
- CurseForge 官方接口需要 API Key；未配置时可以手动选择 Modrinth。
- 更新和公告仍引用部分上游服务，尚未接入独立的 PCL Ap 更新服务器；独立发布前需要调整，避免更新到其他版本。

Debug 版可读取环境变量 `PCL_MS_CLIENT_ID`、`PCL_CURSEFORGE_API_KEY`。Release 构建通过 `PCL_WRITE_SECRET=1` 和源码生成器注入变量。GitHub Actions 对应仓库 Secrets 为 `CLIENT_ID` 和 `CURSEFORGE_API_KEY`。不要把密钥提交到源码中。

## 来源与致谢

- [开发基线 PCL-Alec](https://github.com/Alec-Apu/PCL-Alec)
- [PCL-XH](https://github.com/NoSetViolin/PCL-XH)
- [PCL 社区版](https://github.com/PCL-Community/PCL-CE)
- [原版 PCL](https://github.com/Meloong-Git/PCL) 及原作者龙腾猫跃

保留原作者、社区贡献者和第三方依赖的署名与许可信息。

## 许可证

- `Plain Craft Launcher 2/` 使用目录内的 [LICENCE](Plain%20Craft%20Launcher%202/LICENCE)。
- 其余源码使用 [Apache License 2.0](LICENSE)。

品牌定制不改变原代码和第三方资源的许可条款。
