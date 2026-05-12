# RemoteViewerApp — Viewer cho hệ thống điều khiển máy tính từ xa

## Task list

- [x] UI
- [x] Connect SignalR
- [x] Viewer Register (chưa ổn lắm phần giao diện)
- [ ] 
- [ ] 
...

## 1. Tổng quan

`RemoteViewerApp` là ứng dụng **WinForms .NET 8** phía Viewer, kết nối với server `remoteControllerApp` để:

- Kết nối realtime qua **SignalR** (`/remoteHub`)
- Đăng ký Viewer với server
- Lấy danh sách Host đang online qua **REST API**
- Gửi yêu cầu điều khiển Host
- Nhận và hiển thị frame màn hình Host (JPEG Base64)
- Gửi sự kiện **chuột** (Move, LeftClick, RightClick, DoubleClick, Scroll)
- Gửi sự kiện **bàn phím** (KeyDown, KeyUp + modifiers Ctrl/Shift/Alt)

---

## 2. Yêu cầu

- **Windows** 10/11 (WinForms chỉ chạy trên Windows)
- **.NET 8 SDK** — https://dotnet.microsoft.com/download/dotnet/8.0
- Server `remoteControllerApp` đang chạy
- Host App (`RemoteHostApp`) đã kết nối và đăng ký

---

## 3. Cấu trúc project

```
RemoteViewerApp/
│
├── Program.cs                      → Entry point, khởi tạo services
├── appsettings.json                → Cấu hình ServerUrl, PingInterval
├── RemoteViewerApp.csproj
├── app.manifest
│
├── Models/
│   ├── AppSettings.cs              → Cấu hình app
│   └── ActiveSession.cs            → Session hiện tại + HostInfo (map từ REST API)
│
├── DTOs/                           → Khớp CHÍNH XÁC với server DTOs
│   ├── ViewerRegisterDto.cs        → { ViewerId, ViewerName, UserId }
│   ├── ControlRequestDto.cs        → { HostId, ViewerId, ViewerName, UserId }
│   ├── MouseEventDto.cs            → { SessionId, Action, X, Y, ScreenWidth, ScreenHeight, Button, Delta, SentAt }
│   ├── KeyboardEventDto.cs         → { SessionId, Action, Key, Code, CtrlKey, ShiftKey, AltKey, SentAt }
│   └── ScreenFrameDto.cs           → { SessionId, HostId, ViewerId, ImageBase64, ScreenWidth/Height, FrameWidth/Height, MouseX/Y, SentAt, ReceivedAt }
│
├── Services/
│   └── SignalRViewerService.cs     → Toàn bộ logic SignalR + REST API lấy Host list
│
├── Controls/
│   └── RemoteScreenControl.cs      → PictureBox tuỳ chỉnh: render frame + bắt chuột/bàn phím
│
├── Forms/
│   └── MainForm.cs                 → UI chính: kết nối, chọn host, hiển thị màn hình
│
└── Helpers/
    ├── LoggingHelper.cs            → Logger singleton với màu sắc
    └── KeyMapper.cs                → Map WinForms Keys → (Key, Code) cho server
```

---

## 4. Cài đặt và chạy

### Bước 1: Clone và chuẩn bị

```bash
# Copy thư mục RemoteViewerApp vào máy
cd RemoteViewerApp
```

### Bước 2: Cấu hình server URL

Mở `appsettings.json` và sửa `ServerUrl` nếu server không chạy ở localhost:

```json
{
  "AppSettings": {
    "ServerUrl": "http://localhost:5271",
    "HubEndpoint": "/remoteHub",
    "PingIntervalSeconds": 10
  }
}
```

### Bước 3: Restore và build

```bash
dotnet restore
dotnet build
```

### Bước 4: Chạy

```bash
dotnet run
```

Hoặc mở file `.sln` trong Visual Studio 2022 và nhấn F5.

---

## 5. Cách sử dụng

### Luồng đầy đủ:

```
1. Đảm bảo server đang chạy:   dotnet run (trong thư mục remoteControllerApp)
2. Đảm bảo Host App đã kết nối và đăng ký
3. Mở RemoteViewerApp
4. Nhập Server URL → [🔌 Kết nối]
5. Nhập Viewer ID, Viewer Name, User ID → [📋 Đăng ký Viewer]
6. Nhấn [🔄 Làm mới] để lấy danh sách Host online
7. Chọn Host từ dropdown
8. Nhấn [🖥 Yêu cầu điều khiển]
9. Chờ Host chấp nhận → màn hình Host hiện ra
10. Click vào vùng màn hình để điều khiển
11. Nhấn [🛑 End Control] để kết thúc
```

### Lưu ý quan trọng về User ID:

Server bắt buộc `UserId` không được rỗng cho cả `RegisterViewer` và `RequestControl`.
Nhập bất kỳ giá trị nào (ví dụ: `user-001`) là đủ ở giai đoạn MVP hiện tại.

---

## 6. Protocol & Data Flow

### SignalR Hub: `/remoteHub`

#### Viewer → Server (gọi lên):

| Method | Tham số | Mô tả |
|--------|---------|-------|
| `RegisterViewer` | `ViewerRegisterDto` | Đăng ký viewer online |
| `PingViewer` | `viewerId: string` | Giữ kết nối |
| `RequestControl` | `ControlRequestDto` | Yêu cầu điều khiển host |
| `EndControl` | `sessionId: string` | Kết thúc phiên |
| `SendMouseEvent` | `MouseEventDto` | Gửi sự kiện chuột |
| `SendKeyboardEvent` | `KeyboardEventDto` | Gửi sự kiện bàn phím |

#### Server → Viewer (nhận về):

| Event | Payload | Mô tả |
|-------|---------|-------|
| `RegisterViewerSuccess` | `viewerId` | Đăng ký thành công |
| `RegisterViewerFailed` | `error` | Đăng ký thất bại |
| `ControlRequestSent` | `{ sessionId, hostId, viewerId, status, createdAt }` | Server xác nhận nhận yêu cầu |
| `ControlRequestFailed` | `error` | Gửi yêu cầu thất bại |
| `ControlAccepted` | `{ sessionId, hostId, viewerId, status, acceptedAt }` | Host đồng ý |
| `ControlRejected` | `{ sessionId, hostId, viewerId, status, reason, rejectedAt }` | Host từ chối |
| `ControlEnded` | `{ sessionId, hostId, viewerId, status, endedAt }` | Phiên kết thúc |
| `ReceiveScreenFrame` | `{ sessionId, hostId, viewerId, imageBase64, screenWidth/Height, frameWidth/Height, mouseX/Y, sentAt, receivedAt }` | Frame màn hình |
| `SendMouseEventFailed` | `error` | Gửi mouse event thất bại |
| `SendKeyboardEventFailed` | `error` | Gửi keyboard event thất bại |

#### REST API (GET):

| Endpoint | Mô tả |
|----------|-------|
| `GET /api/Connection/hosts` | Danh sách host đang online |
| `GET /api/Connection/viewers` | Danh sách viewer online |
| `GET /api/Session` | Danh sách session |

---

## 7. Chi tiết DTO — QUAN TRỌNG

### MouseEventDto (gửi lên server)
```csharp
// Field "Action" (không phải "EventType" như HostApp internal)
// Action values: "Move" | "LeftClick" | "RightClick" | "DoubleClick" | "Scroll"
// X, Y: tọa độ THỰC TẾ trên màn hình Host (đã scale)
// ScreenWidth/Height: kích thước màn hình Host (để Host không cần scale lại)
```

### KeyboardEventDto (gửi lên server)
```csharp
// Field "Action" (không phải "EventType" như HostApp internal)
// Key: tên phím kiểu web (e.g. "a", "Enter", "ArrowLeft", "F5")
// Code: mã vật lý (e.g. "KeyA", "Enter", "ArrowLeft", "F5")
// CtrlKey/ShiftKey/AltKey: boolean (không phải IsCtrl/IsShift/IsAlt như HostApp)
```

> **Lý do khác biệt:** HostApp dùng DTOs riêng nội bộ (`EventType`, `KeyCode`, `IsCtrl`...)
> Server dùng DTOs khác (`Action`, `Key`, `Code`, `CtrlKey`...).
> Viewer phải dùng DTOs theo chuẩn SERVER.

---

## 8. Tính năng

### Đã có:
- ✅ Kết nối SignalR với auto-reconnect
- ✅ Auto re-register sau reconnect
- ✅ Ping định kỳ (giữ online)
- ✅ Đăng ký Viewer (với UserId bắt buộc)
- ✅ Lấy danh sách Host online qua REST API
- ✅ Yêu cầu điều khiển Host
- ✅ Nhận và hiển thị frame JPEG realtime
- ✅ Tính FPS hiển thị
- ✅ Hiển thị cursor vị trí chuột Host
- ✅ Gửi MouseMove (throttle 30fps)
- ✅ Gửi LeftClick, RightClick, DoubleClick
- ✅ Gửi Scroll
- ✅ Gửi KeyDown/KeyUp với modifier (Ctrl/Shift/Alt)
- ✅ Scale tọa độ chuột theo tỉ lệ màn hình Host
- ✅ Kết thúc phiên (End Control)
- ✅ Cleanup khi đóng form

### Chưa có (phạm vi MVP):
- ❌ Authentication / JWT
- ❌ Clipboard sync
- ❌ Audio streaming
- ❌ File transfer
- ❌ Fullscreen mode
- ❌ Multi-monitor support

---

## 9. Lỗi thường gặp

### "ControlRequestFailed: Host is not online"
→ Host App chưa đăng ký. Mở Host App, kết nối và bấm "Đăng ký Host".

### "RegisterViewerFailed: UserId is required"
→ Server bắt buộc UserId. Nhập giá trị bất kỳ vào ô User ID.

### Không thấy Host trong dropdown
→ Nhấn "Làm mới" sau khi đã đăng ký Viewer. Host phải đang online.

### Màn hình không hiển thị sau khi Accept
→ Kiểm tra Host App đã bắt đầu stream chưa. Host App sẽ tự động start stream khi accept.

### Phím không gửi được
→ Click vào vùng màn hình remote để focus vào `RemoteScreenControl` trước khi gõ phím.
