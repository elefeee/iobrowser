#index 浏览器宿主


在用某进销存软件的时候，觉得它原生的浏览器内核功能有点弱，干脆自己写一个——WebView2 套个壳，
方便在它内部脚本中命令调用。如果没用它的，不知道在其他软件中是否能调用。兜底也可vbs调用。
加上贴边隐藏、鼠标穿透、透明度切换这些小功能，够用就行。代码没什么高深的东西，
就是 WinForms 调 WebView2 的那套，但架不住实用。开源出来，说不定有人也需要一个"能用的浏览器壳"

默认本地导航页取至  微信公众号H技术派  的此篇 《开源离线本地导航页 V0.1.3：游辰版本再次升级》
不喜欢可以自己替换，nav\


本程序以 MIT License 开源发布，详见仓库根目录 LICENSE 文件。
你可以自由使用、复制、修改、合并、发布、分发、再授权及销售本软件副本，
但须保留上述版权声明及本许可声明。

##命令行调用说明

用法：
    index.exe [URL] [mode] [width height x y] [refresh=N]

参数（均为可选，省略时使用默认值）：

  [URL]
        启动时打开的网址或本地文件路径。
        示例：  index.exe https://www.example.com
                index.exe file:///C:/app/nav/index.html
        省略时默认打开内置主页 nav/index.html；若不存在则回退到在线网址。

  [mode]
        窗口模式，可选值：
            normal          普通窗口（默认，记忆上次位置和大小）
            max             启动时最大化
            full            全屏无边框（Esc 可退出）
            kiosk           全屏无边框（无退出提示，用于展台）
            fixed           固定大小，不可最大化（需配合 width/height）
            borderless      无边框窗口（支持边缘拖拽调整大小）
            remember        记忆上次窗口位置与大小（默认行为）
            clean           清除本地缓存后立即退出（不打开窗口）
        示例：  index.exe https://www.example.com kiosk

  [width] [height]
        窗口宽高（像素），仅在 fixed / borderless / remember 等模式生效。
        示例：  index.exe https://example.com fixed 1024 768

  [x] [y]
        窗口左上角屏幕坐标（像素）。省略则居中。
        示例：  index.exe https://example.com normal 1280 720 100 100

  refresh=N
        自动刷新间隔（秒）。设置后页面会周期性重新加载。
        示例：  index.exe https://example.com refresh=30

常用组合示例：
    1) 默认启动（加载主页，记忆窗口）
        index.exe

    2) 打开指定网址，全屏展台模式
        index.exe https://www.example.com kiosk

    3) 固定 1024x768 窗口，居中，每 30 秒刷新
        index.exe https://www.example.com fixed 1024 768 refresh=30

    4) 无边框窗口，指定位置
        index.exe https://www.example.com borderless 1280 720 200 100

    5) 清除缓存并退出
        index.exe clean


##全局快捷键

    Esc                 退出程序（full / kiosk / fixed / borderless 模式）
    Ctrl + L            显隐地址栏
    F5                  刷新当前页面
    Ctrl + Shift + T    贴边隐藏 / 恢复窗口
    Ctrl + F9           鼠标穿透开关
    Alt + F7            灰度去色（三档循环）
    Alt + F8            窗口透明度切换（1.0 / 0.7 / 0.45）

##运行效果，其实就是想放张圆圆。
<img width="959" height="539" alt="2026-09-06_223641_144" src="https://github.com/user-attachments/assets/69204c23-f382-441d-85b1-9548361438de" />




##第三方组件声明
本程序基于以下第三方组件构建：

Microsoft Edge WebView2 (Microsoft.Web.WebView2)
许可证：MIT License
版权：(c) Microsoft Corporation. All rights reserved.
项目地址：https://github.com/MicrosoftEdge/WebView2Feedback

Newtonsoft.Json
许可证：MIT License
项目地址：https://github.com/JamesNK/Newtonsoft.Json
版权：(c) James Newton-King

上述组件均以 MIT License 发布，其许可证全文见各项目官方仓库。
MIT License 正文见本仓库 LICENSE 文件。



##免责声明
本程序按"现状"提供，作者不对使用过程中产生的任何损失承担责任。
