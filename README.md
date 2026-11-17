<div align="center">

# CS2 盒子

**一个给 CS2 玩家用的本地小工具**

把平时要开控制台、翻配置文件、到处查的东西，集中到一个界面里。

![platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![UI](https://img.shields.io/badge/UI-WPF--UI%20(Fluent)-2d2d2d)
![license](https://img.shields.io/badge/license-MIT-green)
![price](https://img.shields.io/badge/开源-免费-success)

</div>

---

## 功能

| 模块 | 说明 |
| --- | --- |
| 🔁 账号切换 | 读取本机登录过的 Steam 账号，不退出 Steam 就能换号（需勾过"记住密码"） |
| 🚀 启动项 | 勾选常用启动参数，也能直接写进 Steam 的启动选项 |
| 🎯 准星 | 调参数、实时预览，支持与分享码（`CSGO-xxxxx-…`）互转 |
| 🖥️ 视频设置 | 改分辨率、宽高比、刷新率和各项画质 |
| ⌨️ 按键绑定 | 一套常用绑定模板，填好按键一键写入 autoexec.cfg |
| 🏆 职业选手 | 收录选手的准星、灵敏度、分辨率，按自己的 DPI 换算后一键套用 |
| 📁 配置文件 | 备份 / 还原按键、视频、autoexec；换号可自动备份 |
| 📶 服务器延迟 | 测到 Valve 各数据中心的延迟，国内节点显示中文名 |
| 🎬 录像 | 列出本机 demo，可播放、删除、清理旧录像 |
| 🗺️ 创意工坊 | 管理已下载的工坊地图 |
| 🪪 国服认证 | 蒸汽平台实名认证的表单辅助填写 |

> 界面基于 [WPF-UI](https://github.com/lepoco/wpfui)，Win11 Fluent 风格，跟随系统深浅色，可换强调色。

---

## 运行要求

- Windows 10 / 11（64 位）
- **自带运行时，不需要装 .NET**
- 除「服务器延迟」和「国服认证」外，全程不需要联网

## 怎么用

1. 把整个文件夹解压出来
2. 双击 `CS2Box.exe`
3. 第一次打开会先弹一份免责声明，读完勾选同意才能继续

> `Data` 文件夹要和 `exe` 放在一起——里面是本机自带的职业选手数据，删了这个功能就空了。

## 从源码构建

```bash
git clone https://github.com/xiaomao8090/CS2BOX.git
cd CS2BOX
dotnet build -c Release
```

> 需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

---

## 数据与隐私

- 程序**不上传任何数据**到作者的服务器——本来就没有作者的服务器。
- 账号信息读的是你本机 Steam 自己的文件；所有设置存在你的 `%AppData%\CS2Box` 里，删掉那个文件夹就等于重置。
- 国服认证里填写的姓名、身份证、手机号、验证码**只在内存里过一遍，关闭即清空**，不写日志、不上传。

## 开源免费

本项目开源免费，不收取任何费用。任何人都可以阅读、学习、修改、分发。

**作者不出售、不接受打赏、不接定制、不做代实名。** 如果你在别处看到有人拿它收费，那不是作者本人，与作者无关。

## 免责声明

本程序按现状提供，作者不保证它能正常运行、没有错误，或不会导致账号异常、数据丢失。改动游戏配置前请自行备份。使用本程序产生的一切后果，由使用者自行承担。

本程序与 **Valve、完美世界**及其关联公司没有任何关系，未获得任何形式的授权、许可或认可，不包含任何官方代码、资源或数据。

完整条款见程序首次启动时的免责声明，或仓库内 [`README.txt`](README.txt)。

## 许可

[MIT](LICENSE) © xiaomao

---

<div align="center">
<sub>工具只是省点事，账号是你自己的，别为了省几下点鼠标去冒不值得的险。</sub>
</div>
