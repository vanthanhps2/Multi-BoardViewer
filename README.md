# Multi BoardViewer

Ứng dụng Windows giúp quản lý nhiều instance của BoardViewer.exe trong một cửa sổ với hệ thống tab

## Yêu cầu hệ thống

- Windows 10/11
- .NET 8.0 Runtime
- BoardViewer.exe

## Cài đặt và chạy

### Bước 1: Build ứng dụng

```powershell
# Clone repository
git clone https://github.com/mhqb365/Multi-BoardViewer.git
cd Multi-BoardViewer

# Build bằng script
.\Build.bat

# Hoặc build bằng dotnet CLI
dotnet build MultiBoardViewer.sln -c Release
```

### Bước 2: Enable Multi-Instance trong BoardViewer

**QUAN TRỌNG**: Trước khi sử dụng, bật tính năng multi-instance trong BoardViewer:

1. Mở **BoardViewer.exe**
2. Vào **Options** → **Options**
3. **Bỏ tick chọn**: "Use Only One instance of Program"
4. Click **OK** và đóng BoardViewer

### Bước 3: Chạy ứng dụng

```powershell
# Chạy bằng script
.\Run.bat

# Hoặc chạy trực tiếp
.\MultiBoardViewer\bin\Release\net8.0-windows\MultiBoardViewer.exe
```
 
## Hướng dẫn sử dụng

1. **Chọn BoardViewer.exe**: Click "Browse..." và chọn file `BoardViewer.exe`
2. **Tạo tab mới**: Click "➕ New Tab" - mỗi tab là 1 instance riêng
3. **Sử dụng BoardViewer**: Click vào vùng BoardViewer để active focus trước khi dùng phím tắt
4. **Đóng tab**: Click "✕" trên tab

## Xử lý sự cố

### Phím tắt không hoạt động
👉 **Click vào vùng BoardViewer** trong tab để set focus

### BoardViewer bị thoát khi tạo tab mới
👉 Chưa enable multi-instance - xem lại Bước 1

### Tab mới không hiển thị gì
👉 Đợi vài giây (BoardViewer đang khởi động) hoặc thử đóng tab và tạo lại

## Tính năng

✅ Chạy nhiều instance BoardViewer trong cùng 1 cửa sổ  
✅ Quản lý tabs dễ dàng  
✅ Auto-focus khi switch tab  
✅ Tự động cleanup khi đóng  

## Tips

💡 Hover vào tab để tự động set focus  
💡 Click vào BoardViewer area nếu phím tắt không hoạt động  
💡 Mỗi tab hoàn toàn độc lập

---

## Development (cho developer)

### Công nghệ
- WPF + C# .NET 8.0
- Windows API (SetParent, MoveWindow)
- Process embedding technique

### Build từ Visual Studio
1. Cài [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/) với ".NET desktop development" workload
2. Mở `MultiBoardViewer.sln`
3. Build → Build Solution (`Ctrl+Shift+B`)

### Các file chính
- `MainWindow.xaml` - Giao diện UI
- `MainWindow.xaml.cs` - Logic xử lý tab và process embedding
