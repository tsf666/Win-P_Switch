# Win+P Switch (顯示輸出模式切換工具)

![License](https://img.shields.io/badge/license-MIT-blue.svg)

***

单文件目录：

```
Win+P Switch\bin\Release\net8.0-windows\win-x64\publish\Win+P Switch.exe
```

或者直接点本页面右侧👉→ [Releases](https://github.com/user-attachments/files/30730488/Release.zip) 下载

***

# 版本更新记录

v\_0.1 首次发布，支持四种显示输出模式切换、默认模式倒计时与全键盘操作

***

一個基於 .NET 8.0 Windows 窗體 (WinForms) 開發的輕量化、純綠色免安裝工具。用於快速切換 Windows 顯示輸出模式（相當於系統內建的 `Win + P` 面板），支援鍵盤快捷鍵、默認模式自動應用/自動退出倒計時、自適應中英文雙語切換以及深淺色主題。

## 📥 運行環境要求 (重要)

本程序採用**框架依賴**方式進行精簡打包（體積僅幾百 KB），運行時需要依賴本機環境。

* **必要條件**：您的電腦必須安裝 **.NET 8.0 或更高版本** 的桌面運行時 (Desktop Runtime)。
* **官方下載指引**：如果雙擊程序時提示缺少環境，請前往微軟官方網站下載並安裝： 👉 [.NET 8.0 下載頁面](https://dotnet.microsoft.com/download/dotnet/8.0) _(請選擇 **Windows** 平台下的 **Download .NET Desktop Runtime**)_

***

## 🚀 主要功能

* **1 僅電腦屏幕**：立刻切換為僅使用電腦（主）屏幕輸出。
* **2 僅第二屏幕**：立刻切換為僅使用第二屏幕輸出。
* **3 複製**：立刻切換為複製（鏡像）輸出模式。
* **4 擴展**：立刻切換為擴展桌面輸出模式。
* **5 / 6 循環切換**：在本次運行期間，於四種模式之間依序切到下一個 / 上一個，不依賴默認模式設置。
* **默認模式 + 倒計時**：可指定 1~4 中任一模式為"默認模式"，開機後倒計時結束自動應用；任意一次切換完成後，會重新開始"無操作則自動退出"的倒計時，避免程序常駐佔用資源。
* **0 暫停 / 恢復**：暫停或恢復當前正在進行的倒計時。
* **Esc 退出**：直接安全退出本程序，並保存當前設置。
* **🌐 雙語切換**：右上角一鍵切換中/英文 (ZH/EN)，界面佈局隨文字長度完美自適應，不吞字、不換行。
* **🌓 主題切換**：右上角一鍵切換深色/淺色模式。

***

## ⌨️ 快捷鍵指南

本程序支持全鍵盤無鼠標操作：

* **`1` / `2` / `3` / `4`**：立刻切換到對應的顯示輸出模式。
* **`5` / `6`**：本次運行期間循環切到下一個 / 上一個模式。
* **`0`**：暫停 / 恢復倒計時。
* **`Enter (回車)`**：立刻執行當前選中的默認模式（若已設置）。
* **`↑` / `↓` (方向鍵)**：在"不設置默認模式"與 1~4 四種模式之間循環切換默認模式選中項，並自動暫停倒計時。
* **`←` / `→` (方向鍵)**：在底部功能按鈕之間切換選中焦點。
* **`Esc`**：退出程序。
* **`L`**：切換界面語言。
* **`T`**：切換界面主題。

***

## 🛠️ 開發與編譯環境

如果您需要自行修改源代碼並重新打包，請參考以下環境配置：

* **開發語言**：C# 12
* **目標框架**：`.NET 8.0-windows`
* **發布命令 (框架依賴單文件)**：
  ```bash
  dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
  ```

***

## License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/tsf666/Win-P_Switch/blob/Win%2BP_Switch/LICENSE)
) file for details.

***

## 📈 Star History

[![Star History Chart](https://api.star-history.com/svg?repos=%3C%E4%BD%A0%E7%9A%84GitHub%E7%94%A8%E6%88%B7%E5%90%8D%3E/Win-P-Switch\&type=Date)](https://star-history.com/#tsf666/Win-P_Switch&Date)

---