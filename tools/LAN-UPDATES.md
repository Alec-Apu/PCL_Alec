# PCL Ap 内网更新

内网下载入口：http://192.168.5.7:19997/ 。仅允许 192.168.5.0/24 访问。

首次需安装支持内网更新的版本。之后启动时自动检查，也可在设置 → 软件信息 → 检查内网更新中手动检查。
更新需用户确认；未连接内网时自动检查静默失败，不影响启动。HTTP 下载不走系统代理。
Windows x64 包需 .NET 10 Desktop Runtime。当前发布工具与安装器只支持 x64。

## 发布新版

1. 修改 `Plain Craft Launcher 2/Modules/LanUpdate.cs` 中的 `Revision`，每次发布必须递增。
2. 编译发布到干净的目录：

```powershell
dotnet publish 'Plain Craft Launcher 2/Plain Craft Launcher 2.csproj' -c Release -p:Platform=x64 --self-contained false -o 'D:\pcl-release'
```

3. 安装发布工具依赖（首次）：`python -m pip install paramiko cryptography`。
4. 上传并签名（SSH 密码交互输入，不保存在仓库）：

```powershell
python tools/publish-lan-update.py 'D:\pcl-release' --key 'D:\codex-pcl-alec\release-keys\pcl-ap-update.pem' --notes '本次更新内容'
```

必须先递增版本并重新编译，不能仅修改服务器版本信息。发布器最后原子替换 latest.json，防止用户下载到未完成的更新包。
签名私钥位于仓库外，需单独备份；不要上传 GitHub 或放入服务器公开下载目录。

## 服务器

- 文件：`/srv/pcl-ap-updates`
- Nginx 独立配置：`/home/alecapu/pcl-ap-updates/nginx.conf`
- 用户服务：`systemctl --user status pcl-ap-updates`
- 重启：`systemctl --user restart pcl-ap-updates`
- 已启用服务开机启动和用户 linger；防火墙仅允许内网访问该端口。
- 更新服务与现有认证服务、Minecraft 服务独立运行。

## 替换和恢复

版本信息使用内置 RSA 公钥验证，下载 ZIP 进行 SHA-256 校验，并限定仅更新 `PCL Ap.exe` 和 `UI/`。
下载和权限检查成功后启动器退出；本机更新脚本等待旧进程退出，再替换文件和重启。
旧 EXE 和 UI 会保留备份，复制失败会尝试恢复；错误写入安装目录 `update-error.log`。
`PCL/`、游戏、账号及用户设置不在更新包中。

已验证内网下载、签名/篡改拒绝、隔离目录文件替换、失败回滚及配置保留。
尚未在用户正式安装目录执行跨版本自更新；首次真实升级时需再确认自动重启结果。
