# 干净世界下载器

![](./images/ScreenShot-01.png)

## 一、功能简介

为便于广泛传播干净世界节目，包括其各类真相节目，让众人展真知、明真相，特制作此干净世界视频节目下载器。

下载器使用和操作简单，一次可以下载一个视频节目，所有下载均可记忆上次下载位置，下次继续从断点下载。
 
## 二、开发环境等

|  类别  |说明|
| :---   | :---        |
|开发工具	|VS2022 社区版|
|语言|C#|
|DotNet|8.0|
|Nuget引用库|HtmlAgilityPack, HtmlAgilityPack.CssSelectors.NetCore, HttpToSocks5Proxy, Microsoft.NETFramework.ReferenceAssemblie, NETStandard.Library, Newtonsoft.Json, System.ValueTuple|
|添加且修改的项目|VideoDL-m3u8，根据代码需要做了适当修改|

程序调试时，也需要参考“三、使用前的准备”中第2部分，将ffmpeg.exe放在DownloaderForMHR\bin\Debug\net8.0-windows7.0下才能正常运行。

## 三、使用前的准备

使用前程序需要做好的准备： 

1、安装微软net Core8的桌面版程序运行时，下载地址：
https://download.visualstudio.microsoft.com/download/pr/f18288f6-1732-415b-b577-7fb46510479a/a98239f751a7aed31bc4aa12f348a9bf/windowsdesktop-runtime-8.0.1-win-x64.exe

2、下载ffmpeg.exe（用于转换干净世界视频为 mp4）
在 https://github.com/BtbN/FFmpeg-Builds/releases 下载 ffmpeg-master-latest-win64-gpl.zip，解压后在其下的 bin 目录下找到 ffmpeg.exe，将其复制到当前程序目录下。
此文件是在github上下载，只要把下载链接粘贴在浏览器地址栏回车后，如果没有打开网页，多刷新几次，过一会就会连上，然后就可以下载了。因为这个文件比较大，大于100M，要耐心点。

3、如果使用自由门限制版下载干净世界节目，需按照第4部分【添加自由门白名单】的方法，把如下内容添加到其代理的白名单里：
- .ganjingworld.com
- .cloudokyo.cloud
- .edgefare.net

4、【添加自由门白名单】的方法
（1）在自由门界面点击“设置”(图标为齿轮形状的)；
（2）在设置窗口，点击“自由门代理控制”按钮；
（3）在自由门代理控制窗口，在第三个“只允许通过……”的旁边，点击“添加”按钮，把上面的连接依次逐个输入进去，记得前面有个小点。然后一路点击确定就好了。


## 四、使用简要说明

1、	获取下载链接
打开干净世界网站，找到要下载的视频页面，在所在视频上点击右键，从弹出的菜单中选择“复制链接”；

![](./images/ScreenShot-02.png)

2、将得到的视频链接粘贴到程序的“网页链接”处；

![](./images/ScreenShot-03.png)

3、点击“获取下载链接”，过程中会弹出选择节目的视频清晰度（最新的节目还有音频质量选择）的对话框，选择后，点击“确认”按钮。获取成功后，会在下载列表添加一个下载项；

![](./images/ScreenShot-04.png)

4、点击“开始下载按钮”下载视频，视频下载后会自动合并转换下载的内容为MP4文件。

![](./images/ScreenShot-05.png)

下载完成后，MP4 文件保存在程序所在目录下的 “下载”目录里，一个视频单独一个文件夹，如：《我们告诉未来 第一集： 气功铺路》就保存在“下载\我们告诉未来 第一集： 气功铺路”目录下。

## 五、所使用或引用的项目


1、VideoDL-m3u8
https://github.com/fysh711426/VideoDL-m3u8

2、ffmpeg
https://github.com/BtbN/FFmpeg-Builds/releases

### 诚心感谢作者的付出！

## 六、郑重声明

#### 本项目仅为广泛传播干净世界节目，包括其各类真相节目，让世人展真知、明真相所用而特别制作。
#### 下载的所有节目，请尊重节目的版权，请勿修改其任何内容，保证节目的完整。
#### 对于利用所下载的节目拼接、修改等以达到其各种不善目的的，请悬崖勒马。苍天在上，莫要做此等坏事，害己害人，绝不可取。
#### 对于下载节目，用于广传真相的可贵的善良的世人，感谢您的付出！您的善举将会给您带来美好的未来！
