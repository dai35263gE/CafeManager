# Hệ thống quản lý quán cà phê

Dự án môn học hướng đối tượng, backend ASP.NET Core **10**, EF Core **10**, SQL Server,
ASP.NET Core Identity và JWT. Kiến trúc Domain / Infrastructure / API; có Docker
Compose cho môi trường phát triển cục bộ, không dùng microservices, CQRS hay MediatR.

## Trạng thái: Phase 5 (hoàn thiện)

Đã có solution, entity/enum, DbContext, cấu hình Identity/JWT/CORS/Swagger,
migration `InitialCreate`, seed Development và kiểm thử các quy tắc nền tảng.
Phase 2 bổ sung API Auth, quản trị tài khoản/role, bàn, thực đơn, đơn hàng và pha chế.
Phase 3 bổ sung thanh toán/hóa đơn, khuyến mãi, nguyên liệu/kho, nhân viên và
báo cáo/CSV. Phase 4 bổ sung giao diện React tiếng Việt. Phase 5 hoàn thiện seed
(thêm nhân viên liên kết tài khoản), kiểm thử, hướng dẫn chạy và checklist nghiệm thu.
`GET /api/health` là liveness công khai, không kiểm tra database.

```text
QLQCF\
├── CafeManagement.sln
├── Directory.Build.props
├── .gitignore
├── .env.example
├── .env.docker.example
├── .dockerignore
├── docker-compose.yml
├── .config\
│   └── dotnet-tools.json
├── scripts\
│   └── New-DockerEnv.ps1
├── backend\
│   ├── Dockerfile
│   ├── src\
│   │   ├── CafeManagement.Domain\
│   │   │   ├── Common\       BaseEntity, DomainException, Roles
│   │   │   ├── Entities\     entities và bảng liên kết
│   │   │   ├── Enums\        trạng thái nghiệp vụ
│   │   │   └── Rules\        quy tắc doanh thu và thanh toán
│   │   ├── CafeManagement.Infrastructure\
│   │   │   ├── Identity\     ApplicationUser
│   │   │   ├── Persistence\
│   │   │   │   ├── Configurations\
│   │   │   │   └── Migrations\  InitialCreate, VietnameseTextSupport, TableSessionsAndConcurrency, ModelSnapshot
│   │   │   └── Seeding\      DevelopmentSeeder
│   │   └── CafeManagement.Api\
│   │       ├── Contracts\
│   │       ├── Controllers\
│   │       ├── Middleware\
│   │       ├── Security\
│   │       ├── Services\
│   │       ├── Utils\        CSV
│   │       ├── Properties\
│   │       ├── Program.cs
│   │       ├── CafeManagement.Api.http
│   │       └── appsettings.json
│   └── tests\
│       └── CafeManagement.Tests\
│           ├── BusinessRulesTests.cs
│           ├── SqlServerTests.cs
│           ├── Phase2ApiTests.cs
│           ├── Phase3ApiTests.cs
│           └── SqlApiHost.cs
├── frontend\
│   └── cafe-management-web\
│       ├── .dockerignore
│       ├── Dockerfile
│       └── nginx.conf
└── README.md
```

Frontend nằm ở `frontend\cafe-management-web`: React 18, TypeScript, Vite,
Ant Design, React Router, Axios, TanStack Query, React Hook Form và Zod
(xem mục 8 để chạy). Docker build frontend thành static assets và dùng Nginx
proxy `/api` tới API container.

## 1. Yêu cầu môi trường

- .NET 10 SDK (bao gồm ASP.NET Core 10 runtime); dự án target `net10.0`.
- SQL Server hoặc SQL Server Express; Windows Authentication là lựa chọn đơn giản.
- Quyền tạo database cho lần chạy migration đầu tiên, hoặc database đã tạo sẵn
  và tài khoản có quyền tạo bảng/index.
- Node.js LTS (kèm npm) để chạy frontend.
- Docker Desktop với Linux containers và Docker Compose v2 nếu chạy theo cách Docker.
- Các lệnh dưới đây chạy bằng PowerShell tại thư mục chứa solution.

## 2. Cấu hình cục bộ

Không đưa mật khẩu SQL hay khóa JWT vào source control. `appsettings.json` chứa
placeholder SQL Server và không chứa khóa JWT. `.env.example` chỉ là tài liệu:
ASP.NET Core **không tự đọc** file `.env`.

```powershell
dotnet restore CafeManagement.sln
dotnet tool restore

# Đổi server theo máy của bạn; ví dụ .\SQLEXPRESS.
# TrustServerCertificate=True chỉ dành cho môi trường Development cục bộ.
$env:ConnectionStrings__CafeDatabase = 'Server=.\SQLEXPRESS;Database=CafeManagement;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet user-secrets set 'ConnectionStrings:CafeDatabase' $env:ConnectionStrings__CafeDatabase --project backend\src\CafeManagement.Api

# Tạo khóa ngẫu nhiên, tương thích Windows PowerShell 5.1.
$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($bytes)
$rng.Dispose()
$env:Jwt__Key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set 'Jwt:Key' $env:Jwt__Key --project backend\src\CafeManagement.Api
```

User-secrets chỉ dành cho Development và không phải kho bí mật đã mã hóa.
Trong môi trường triển khai, cấp các giá trị qua cấu hình bí mật của môi trường;
SQL Server dùng chứng chỉ hợp lệ, `Encrypt=True`, không bỏ kiểm tra chứng chỉ.

## 3. Migration và khởi chạy

```powershell
dotnet ef database update --project backend\src\CafeManagement.Infrastructure --startup-project backend\src\CafeManagement.Api
dotnet run --project backend\src\CafeManagement.Api
```

Migration `VietnameseTextSupport` dùng `nvarchar` cho các cột chứa nội dung tiếng
Việt. Chạy `database update` để áp dụng trên database hiện có. Việc đổi kiểu cột
không thể khôi phục ký tự đã bị thay bằng `?`; seed cũng không ghi đè các bản ghi
đã tồn tại. Hãy sao lưu database trước khi quyết định sửa hoặc tạo lại dữ liệu seed.

Migration `TableSessionsAndConcurrency` bổ sung rowversion và lịch sử liên kết
nhiều-nhiều `DonHangBan`, đồng thời backfill từ `DonHang.MaBan` hiện có. Đây là
migration cộng thêm, không cần reset database; áp dụng bằng cùng lệnh
`dotnet ef database update` trước khi chạy API phiên bản mới.

`CafeDbContextFactory` đọc **biến môi trường** `ConnectionStrings__CafeDatabase`
cho EF CLI, không đọc user-secrets của API. Vì vậy cần giữ biến môi trường này
trong shell chạy `database update` như ở bước 2. Có thể thay bằng tham số
`--connection` khi chạy EF CLI. Runtime API đọc cấu hình ASP.NET tiêu chuẩn.
Biến `Jwt__Key` trong shell giúp EF CLI khởi tạo startup project không báo lỗi;
việc sinh migration bằng factory không cần kết nối database.

- Swagger Development: <http://localhost:5080/swagger>
- Liveness: <http://localhost:5080/api/health>
- CORS cho đúng origin <http://localhost:5173>.
- API nghiệp vụ bắt buộc xác thực theo fallback policy.
  Liveness là ngoại lệ công khai; Swagger chỉ bật trong Development.
- Các response JSON dùng `{ success, message, data, errors }`; DELETE thành công
  trả `204 No Content` theo HTTP semantics, không có JSON body.
- Chạy API cục bộ không tự động migrate; migration là bước triển khai tường minh.
  Compose bật migration-on-startup có kiểm soát riêng cho Development.

## 4. Seed dữ liệu Development

Seed mặc định **tắt**. Bật chỉ sau khi đã áp dụng migration, với đủ sáu mật khẩu:

```powershell
dotnet user-secrets set 'Seed:Passwords:admin' 'Admin@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Passwords:manager' 'Manager@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Passwords:waiter' 'Waiter@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Passwords:barista' 'Barista@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Passwords:cashier' 'Cashier@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Passwords:warehouse' 'Warehouse@123' --project backend\src\CafeManagement.Api
dotnet user-secrets set 'Seed:Enabled' 'true' --project backend\src\CafeManagement.Api
dotnet run --project backend\src\CafeManagement.Api
```

Đây là mật khẩu mẫu công khai của đề bài, **không dùng trong thực tế**.
Identity hash mật khẩu; database không lưu plaintext. Khi seed chạy lại,
không reset mật khẩu/role/trạng thái của tài khoản đã tồn tại và không ghi đè
dữ liệu vận hành. Seed dùng transaction; lỗi phải được sửa thay vì bị bỏ qua.

| Username | Mật khẩu demo | Role |
|---|---|---|
| admin | Admin@123 | Admin |
| manager | Manager@123 | Manager |
| waiter | Waiter@123 | Waiter |
| barista | Barista@123 | Barista |
| cashier | Cashier@123 | Cashier |
| warehouse | Warehouse@123 | Warehouse |

Dữ liệu mẫu: 6 nhân viên `NV-ADMIN`…`NV-WAREHOUSE`, mỗi người liên kết với tài khoản
cùng tên (chỉ liên kết khi tạo mới, không liên kết lại nếu quản lý đã gỡ/cho nghỉ);
10 bàn / 4 khu vực; 4 danh mục; 6 món; size S/M/L; 2 topping;
2 khuyến mãi WELCOME10 và CAFE20K; 4 nguyên liệu, trong đó 2 nguyên liệu tồn thấp;
lịch sử nhập tồn đầu và audit tạo tài khoản. Hạn khuyến mãi tính từ lần seed
đầu, không tự gia hạn khi khởi động lại.

Sau lần seed đầu có thể tắt:

```powershell
dotnet user-secrets set 'Seed:Enabled' 'false' --project backend\src\CafeManagement.Api
```

Khởi động Production với `Seed:Enabled=true` sẽ thất bại rõ ràng.
Không chạy đồng thời nhiều instance seed trên cùng database Development.

## 5. Mô hình và giả định nghiệp vụ

- Dùng `Guid` làm khóa, thời gian `DateTimeOffset` UTC; hạn nguyên liệu dùng `DateOnly`.
- `ApplicationUser` nằm trong Infrastructure vì kế thừa Identity; Domain không
  phụ thuộc EF/Identity. Role lưu trong bảng liên kết chuẩn Identity, không tạo
  thêm cột Role hay tự quản lý PasswordHash.
- `CafeTable` thay tên lớp `Table` để tránh nhầm với các tên hệ thống; bảng SQL vẫn là `Tables`.
- Tiền `decimal(18,2)`, số lượng kho `decimal(18,3)`; topping quantity là số phần
  **trên mỗi ly**. `MenuItemSize.Price` là giá đầy đủ; `Size.PriceDelta` dùng gợi ý
  khi tạo giá size. Món không size dùng `MenuItem.BasePrice`.
- Có `MenuItemTopping` để xác định topping được phép. Tên món/size/topping và giá
  được snapshot ở dòng bán; hóa đơn có subtotal snapshot.
- Một bàn chỉ có một order Open/Confirmed/Preparing/Served bằng **filtered unique
  index SQL Server**, kể cả khi hai request tạo đơn đồng thời. Không được soft-delete
  đơn đang active. Đơn mang đi không có bàn.
- `RowVersion` trên bàn, đơn, dòng món, nguyên liệu và hóa đơn hỗ trợ optimistic
  concurrency; tầng API chuyển xung đột thành HTTP 409.
- Mỗi order chỉ có một invoice, kể cả invoice đã Cancelled; hủy hóa đơn không
  tạo lại hóa đơn cho cùng order. Phase 3 chỉ cho hủy có lý do, không sửa tiền.
- Check constraints bảo vệ tiền không âm, phương trình tiền thừa, mức giảm,
  số lượng tồn và lịch sử nhập/xuất/kiểm kê/điều chỉnh. Xuất/nhập dùng lượng dương,
  Adjust dùng delta có dấu, Stocktake dùng số tồn thực đếm.
- Mọi biến động kho giữ tồn trước/sau, lý do, người thao tác và thời gian.
  Audit/kho append-only qua DbContext; FK không cascade-delete lịch sử.
  Chỉ nguyên liệu chưa có tồn, lịch sử kho hoặc công thức mới được xóa.
- Xác nhận đơn rỗng hoặc chỉ có món hủy bị từ chối bởi `Order.Confirm()`.
  `Promotion.CalculateDiscount()` kiểm tra thời gian/trạng thái/đơn tối thiểu
  và chặn giảm vượt subtotal. `RevenueRules.PaidInvoices()` loại hóa đơn Cancelled.
- Điều kiện khuyến mãi chỉ gồm thời gian, trạng thái, minimum order và maximum
  discount; `ConditionDescription` là mô tả, không thực thi biểu thức tùy ý.
- Phase 2 có transaction chuyển/ghép bàn, kiểm tra role/trạng thái tài khoản
  mỗi request JWT, quyền hủy món và thông báo pha chế bằng polling.
  Phase 3 bảo vệ bất biến tiền hóa đơn tại service và DbContext.

## Phase 2: API và quy tắc sử dụng

Kiến trúc HTTP dùng Controller → service interface/implementation → EF DbContext.
Liveness cũng chuyển sang controller để không trộn hai phong cách endpoint.
DTO là sealed record; không trả EF entity hoặc PasswordHash/SecurityStamp.
Các lỗi dùng envelope của đề bài thay vì chuyển sang ProblemDetails.
Swagger có XML documentation cho endpoint/DTO và Bearer Authorize.
Schema database không thay đổi ở Phase 2, **không cần migration bổ sung**.

### Phân quyền

| Phân hệ / thao tác | Role |
|---|---|
| Đăng nhập | Không cần JWT |
| Me / đổi mật khẩu | Mọi tài khoản hợp lệ |
| Quản trị tài khoản, gán role, khóa/mở | Admin |
| Đọc bàn / thực đơn / đơn hàng | Manager, Waiter, Barista, Cashier |
| CRUD danh mục, món, size, topping; tạo/sửa/xóa bàn | Manager |
| Trạng thái bàn, tạo/sửa đơn, thêm món, chuyển/ghép bàn | Manager, Waiter |
| Đọc hàng đợi pha chế | Manager, Waiter, Barista |
| Đọc thông báo đơn hàng / thiếu hàng | Manager, Waiter, Barista, Cashier |
| Waiting → Preparing → Completed | Barista, Manager |
| Completed → Served | Waiter, Manager |
| Hủy món Waiting / hủy đơn chưa bắt đầu | Waiter, Manager |
| Hủy món/đơn đã bắt đầu pha chế | Manager |
| Available ↔ OutOfStock / báo thiếu nguyên liệu | Barista, Manager |
| Discontinued / khôi phục món đã ngừng bán | Manager |

Admin có toàn quyền trên mọi phân hệ và thao tác trong các bảng phân quyền,
bao gồm vận hành, pha chế, thanh toán, kho, nhân viên, báo cáo và quản trị tài khoản.
Các quy tắc nghiệp vụ, kiểm tra trạng thái/RowVersion và bảo vệ Admin cuối cùng
vẫn được áp dụng. Admin được xem và đánh dấu đã đọc thông báo của mọi vai trò.
Warehouse có quyền kho ở Phase 3 như bảng dưới đây. Khách hàng không có tài khoản.

### Auth và tài khoản

- `POST /api/auth/login`: `{ username, password }` → `{ token, expiresAt, user }`.
- `GET /api/auth/me`: account ID, username, fullName, role, employeeId, isActive,
  lastLoginAt và isLocked; role là đúng một trong sáu role hỗ trợ.
- `POST /api/auth/change-password`: `{ currentPassword, newPassword }`.
  Sau đổi mật khẩu, **mọi JWT cũ bị vô hiệu**, cần đăng nhập lại.
- `GET /api/tai-khoan`, `GET /api/tai-khoan/{id}`, `POST /api/tai-khoan`,
  `PUT /api/tai-khoan/{id}`, `PATCH /api/tai-khoan/{id}/lock`.
  Lock payload `{ locked: true }`, unlock `{ locked: false }`.
- Username/email được kiểm tra theo contract; mật khẩu tối thiểu 8 ký tự và
  được hash; role whitelist; employee liên kết nếu có phải đang Working.
- JWT kiểm tra account active, lockout, employee Working, security stamp và
  role hiện tại trên **mỗi request**. Khóa/mở, đổi role/profile và đổi mật khẩu
  vô hiệu token cũ. Tránh cache authorization gây chậm hiệu lực khóa tài khoản.
- Sai mật khẩu ghi audit và tăng bộ đếm: 5 lần sai khóa 15 phút.
  Login giới hạn 20 request/IP/phút, trả HTTP 429 rõ ràng.
- Không cho Admin tự khóa hoặc tự bỏ role Admin; giữ ít nhất một Admin active.
- Logout ở frontend là xóa token, không cần endpoint logout hay refresh token.
- Audit lưu login/thất bại, đổi mật khẩu, tài khoản, menu, bàn và thao tác đơn.
  Không ghi mật khẩu/token vào audit.

### Bàn và thực đơn

- `GET /api/ban` và `GET /api/ban/{id}` hiển thị bàn theo khu vực/trạng thái,
  rowversion và đơn đang liên kết; quyền xem dành cho Manager/Waiter.
- `POST /api/ban/{id}/seat` xếp bàn trống, không tạo đơn. Đơn dine-in có thể
  được tạo sau đó trên bàn đã xếp; BR01 vẫn chặn đơn thứ hai đang hoạt động.
- `POST /api/ban/transfer` chuyển một liên kết đơn/bàn nguyên tử. Bàn đích phải
  trống; bàn nguồn chuyển sang NeedsCleaning.
- `POST /api/ban/merge` ghép hai bàn Available/InUse. Nếu có một đơn, liên kết
  cả hai bàn với đơn đó; nếu có hai đơn, ghép các dòng vào đơn đích và chuyển
  toàn bộ liên kết bàn đang hoạt động. Hai bàn giữ trạng thái InUse. Có thể ghép
  bàn trước khi tạo đơn; phản hồi `activeOrder` khi đó là null.
- `PATCH /api/ban/{id}/status`: chỉ cho đánh dấu NeedsCleaning/Available khi
  không còn đơn active; chỉ bàn NeedsCleaning mới được trả về Available.
  Thanh toán/hủy đơn tự chuyển bàn sang NeedsCleaning và đóng liên kết hiện tại.
- Manager có quyền `POST /api/ban`, `PUT /api/ban/{id}` và
  `DELETE /api/ban/{id}?rowVersion=...`. Mã bàn không đổi; không thể ngừng bàn
  đang InUse hoặc còn đơn active. Waiter không được CRUD danh sách bàn.
- `DonHangBan` lưu lịch sử nhiều-nhiều bàn/đơn; partial unique index bảo đảm
  một bàn tối đa có một liên kết đơn đang hoạt động. ERD/class diagram tương ứng
  nằm trong `docs\erd.puml` và `docs\classdiagram.puml`.
- `GET/POST /api/categories`, `GET/PUT/DELETE /api/categories/{id}`.
- `GET/POST /api/sizes`, `GET/PUT/DELETE /api/sizes/{id}`.
- `GET/POST /api/toppings`, `GET/PUT/DELETE /api/toppings/{id}`.
- `GET/POST /api/menu-items`, `GET/PUT/DELETE /api/menu-items/{id}`,
  `PATCH /api/menu-items/{id}/status`.
- DELETE danh mục/size/topping là deactivation; DELETE món là
  `Discontinued + IsActive=false`. Không xóa lịch sử hoặc giải phóng tên/mã cũ.
- PUT menu chứa `sizes: [{ sizeId, price }]` và `toppingIds: [id]`;
  thay danh sách option cho các giao dịch mới. Giá theo size là giá đầy đủ;
  cập nhật `Size.PriceDelta` không tự thay `MenuItemSize.Price`.
- Menu GET mặc định `orderableOnly=true`, ẩn món hết/ngừng bán/inactive và
  category inactive; trang quản lý dùng `orderableOnly=false`.
- Tất cả danh sách có `page` (mặc định 1), `pageSize` (mặc định 20, tối đa 100),
  `search`; trả `{ items, totalCount, page, pageSize }` trong `data`.
  Bàn lọc `status, area`, menu `categoryId, status`, các catalog `active`.

### Đơn hàng và pha chế

- `GET/POST /api/orders`, `GET/PUT/DELETE /api/orders/{id}`.
- `POST /api/orders/{id}/items`: `{ rowVersion, items: [...] }`.
- `PATCH /api/orders/{id}/items/{itemId}`: `{ quantity, note, rowVersion }`.
  RowVersion ở đây là token **của dòng món**; chỉ sửa Waiting.
- `DELETE /api/orders/{id}/items/{itemId}`:
  body `{ rowVersion, reason }`; giữ dòng Cancelled, bỏ khỏi subtotal.
- `PATCH /api/orders/{id}/status`: `{ status, rowVersion, reason? }`.
  Chỉ nhận Confirmed/Served/Cancelled. Không cho set Paid hoặc nhảy trực tiếp
  sang Preparing bằng API trạng thái đơn.
- DELETE cả đơn dùng body `{ rowVersion, reason }`, là hủy, không hard-delete.
  Đơn Paid/Cancelled không được thay đổi.
- Tạo draft cho phép chưa có món; chỉ Confirm khi có ít nhất một món active.
  `POST` có thể nhận `confirm: true` để tạo và xác nhận đơn cùng giao dịch; UI dùng
  luồng này sau khi đã chọn món. DineIn bắt buộc tableId; TakeAway không được có tableId.
- Mỗi dòng gửi `{ menuItemId, sizeId?, quantity, note?, toppings }`;
  ghi chú được lưu trên dòng; không nhận giá từ client. Nếu món có sizes phải chọn
  đúng size thuộc món; topping phải thuộc danh sách được phép, không lặp topping.
- Quantity 1–100; topping được chọn một lần theo liên kết trong class diagram;
  tối đa 100 dòng/đơn.
  Giá tiền không âm, tối đa 999999999, tối đa hai chữ số thập phân.
- UnitPrice là snapshot giá size cộng topping tại thời điểm gọi món.
  Sửa số lượng/ghi chú không repricing. Gọi thêm món dùng giá hiện tại và vào hàng đợi
  ngay nếu đơn đã confirmed.
- `PATCH /api/orders/{id}/items/{itemId}/status` dùng token của dòng:
  Waiting → Preparing → Completed → Served, không chuyển ngược.
  Khi Barista nhận món, người nhận được ghi trong audit và trả về `PreparedByUserId`;
  Barista, Waiter và Manager nhận thông báo; chỉ người nhận hoặc Manager được Complete.
  Hai người nhận cùng một token chỉ một người thành công.
- Hàng đợi mặc định hiển thị món Waiting, sắp xếp theo thời điểm gọi món (kể cả món
  được thêm sau); chi tiết size/topping/ghi chú được giữ trên từng dòng. Thông báo
  lưu đến khi được người dùng cùng vai trò xác nhận đã xem, và hiển thị cả thông báo cũ
  khi đăng nhập lại.
- Báo món hết hàng cho phép bỏ trống lý do, gửi thông tin người báo/thời điểm và các
  đơn Waiting/Preparing bị ảnh hưởng; không tạo báo cáo lặp khi món đã OutOfStock.
- Manager có thể mở lại dòng đã hủy trong đơn còn hoạt động bằng lý do bắt buộc;
  thao tác ghi audit và thông báo lại cho các bộ phận liên quan.
- `PATCH /api/don-hang/{id}/items/{itemId}/reopen` chỉ dành cho Manager, nhận
  `{ rowVersion, reason }`. `PATCH /api/don-hang/kitchen-notices/{id}/read`
  ghi nhận người dùng hiện tại đã xem thông báo thuộc vai trò của họ.
- Hủy dòng cuối cùng của đơn đã confirmed bị từ chối: hãy hủy cả đơn.
  Không cho thay số lượng/ghi chú món đã bắt đầu pha chế, kể cả Manager;
  Manager có thể hủy và tạo dòng mới nếu cần.
- Từng món Served không tự hoàn tất đơn. Khi tất cả món hợp lệ đã Served, nhân viên
  phải xác nhận hoàn tất; lúc đó order chuyển Served và sẵn sàng thanh toán.
  Served vẫn là đơn chưa thanh toán và vẫn chiếm bàn.
- `POST /api/orders/{id}/transfer`: targetTableId, rowVersion của order,
  sourceTableRowVersion và targetTableRowVersion; chỉ chuyển đơn dine-in chưa hoàn tất
  sang bàn Available.
- `POST /api/orders/{id}/merge`: targetOrderId, rowVersion của source,
  targetOrderRowVersion. Cả hai phải là draft hoặc đang hoạt động, chưa hoàn tất;
  không tự xác nhận draft khi ghép. Chuyển nguyên dòng sang đơn đích, giữ ID,
  size/topping/giá/note/owner/state, hủy đơn nguồn với subtotal 0 và ghi audit
  liên kết. Không ghép takeaway.
- API table-oriented transfer/merge gọi cùng service giao dịch.
- `POST /api/ban/merge` nhận `targetTableId`, token của bàn chính và mảng
  `sources` gồm bàn nguồn cùng token bàn/đơn đang hoạt động. Chọn rõ `targetOrderId`
  và `targetOrderRowVersion` khi ghép nhiều đơn; nếu chỉ có một đơn thì hệ thống
  tự dùng đơn đó. Ghép nhiều bàn được lưu đồng bộ, mỗi bàn có tối đa một đơn
  hoạt động; ghép bàn trống không tự tạo đơn.
- Các mutation dùng SQL Server transaction Serializable + RowVersion;
  nạp aggregate và bàn đích dùng tham số SQL `UPDLOCK, ROWLOCK` để tránh hai
  người cùng nhận món/bàn. Mọi mutation dòng đều đổi token của aggregate.
  Conflict/deadlock trả **409**, client reload rồi quyết định thử lại;
  không tự retry một giao dịch có side effect.

### Thông báo: polling, không thêm SignalR

- `GET /api/orders/kitchen-queue` lấy Waiting/Preparing/Completed của đơn đã
  xác nhận; trả cả owner và token. Món Cancelled hiển thị trong 24 giờ để tránh
  pha trùng. Draft không xuất hiện trong queue.
- Waiter poll `GET /api/orders/kitchen-queue?status=Completed` để thông báo
  món sẵn sàng; khi Served món tự rời danh sách này. Frontend hiển thị bằng polling ở trang Pha chế.
- `POST /api/orders/shortage-reports`: chọn menuItemId hoặc ingredientId,
  kèm reason. Nếu chọn món thì chuyển OutOfStock, lưu notice audit để Waiter/
  Manager xử lý. **Không tự trừ hoặc sửa tồn kho** khi chỉ báo thiếu nguyên liệu.
- `GET /api/orders/kitchen-notices?since=<ISO8601>` lấy thông báo thiếu hàng và
  các thay đổi đáng chú ý của đơn/món, theo vai trò nhận; Cashier nhận thông báo
  đơn sẵn sàng thanh toán. Mặc định 24 giờ gần nhất, có phân trang/tìm kiếm.
- Các ví dụ payload, login theo role, status và lỗi có trong
  `backend\src\CafeManagement.Api\CafeManagement.Api.http`.
  Dùng REST Client hoặc Swagger; thay placeholder ID/token bằng GET thực tế.
  Các request thay đổi mật khẩu hoặc xóa dữ liệu nên chạy có chủ đích, không
  gửi toàn bộ file HTTP một cách mù quáng.

### Concurrency token

RowVersion là Base64 8 byte trả về từ SQL Server. Không tự tạo token.
Mutation đơn dùng token aggregate; mutation dòng dùng token item. Table PUT,
seat, status, transfer, merge và DELETE đều kiểm tra token của các bàn liên quan;
transfer/merge cũng kiểm tra token đơn đang hoạt động. DELETE bàn nhận `rowVersion` trong query
(URL-encode ký tự `+`, `/`, `=`). Thiếu/không hợp lệ → 400, token cũ → 409.
Sau mọi mutation, lấy token mới từ response hoặc GET lại resource.

## Phase 3: thanh toán, kho, nhân viên và báo cáo

Giữ Controller → service → EF DbContext, không thêm tầng kiến trúc mới.
**Không cần migration bổ sung:** các cột/index/constraint cần thiết đã có trong
InitialCreate; EF xác nhận không có pending model changes.

### Phân quyền bổ sung

| Thao tác | Role |
|---|---|
| Thanh toán, đọc hóa đơn; đọc/kiểm tra khuyến mãi | Manager, Cashier |
| Hủy hóa đơn; tạo/sửa/ngừng khuyến mãi | Manager |
| Đọc nguyên liệu và cảnh báo | Manager, Warehouse, Barista |
| CRUD nguyên liệu, nhập/xuất/điều chỉnh/kiểm kê, đọc lịch sử | Manager, Warehouse |
| Đọc nhân viên/liên kết tài khoản | Manager, Admin |
| Tạo/sửa hồ sơ, nghỉ việc, tạo tài khoản và khóa/mở khóa tài khoản nhân viên | Manager |
| Tạo/sửa tài khoản qua API quản trị tổng quát | Admin |
| Doanh thu, món bán chạy, phương thức thanh toán, chi tiết hóa đơn và xuất báo cáo | Manager |
| Báo cáo tồn kho với lọc mức tồn/hạn dùng và xuất báo cáo | Manager, Warehouse |

### Thanh toán và hóa đơn

- `GET/POST /api/hoa-don`, `GET /api/hoa-don/{id}`,
  `GET /api/hoa-don/{id}/print`, `POST /api/hoa-don/{id}/cancel`.
- `GET /api/hoa-don/quote?orderId=...&rowVersion=...&promotionCode=...`
  trả báo giá được máy chủ tính từ đơn hiện hành, gồm giảm giá, VAT, phí dịch vụ
  và cảnh báo cấu hình. Quote không thay thế bước kiểm tra lại trong lúc thanh toán.
- Danh sách có `page, pageSize, search, status, from, to`; khoảng thời gian
  `[from,to)` theo thời điểm thanh toán.
- POST nhận `{ orderId, rowVersion, promotionCode?, paymentMethod, receivedAmount }`.
  Token là của **order**. Chỉ thanh toán đơn Served có ít nhất một dòng không hủy
  và tất cả dòng không hủy đã Served. Subtotal lấy từ snapshot phía server.
- VAT và phí dịch vụ tính theo phần tiền còn lại sau giảm giá, làm tròn hai chữ số.
  Cấu hình qua `Billing:VatRatePercent` và `Billing:ServiceFeeRatePercent`;
  mặc định trong `appsettings.json` là 0%. Thiếu hoặc sai cấu hình được áp dụng 0%
  và trả cảnh báo cùng quote. Hóa đơn lưu snapshot riêng của VAT và phí dịch vụ.
- `Cash` cho nhận dư và tính tiền thừa; `BankTransfer`, `Card`, `EWallet` phải nhận
  đúng tổng sau giảm. Không tích hợp ngân hàng/ví, không xác minh giao dịch ngoài.
- Khuyến mãi được kiểm tra lại trong transaction thanh toán. Invoice, order Paid,
  bàn NeedsCleaning và audit lưu nguyên tử; chống thanh toán hai lần.
  Bàn không còn đơn active nhưng phải xác nhận vệ sinh trước khi dùng lại.
- Cancel nhận `{ rowVersion, reason }`, token của **invoice**, chỉ Manager.
  Giữ nguyên số tiền, người thu và PaidAt; lưu người hủy/thời gian/lý do.
  Không mở lại order, không chiếm lại bàn, không tạo invoice mới cho order đó.
  Đây là hủy nội bộ có audit, không tự hoàn tiền qua cổng thanh toán.
- DbContext cấm sửa/xóa thông tin tài chính invoice đã lưu; không có API PUT
  để sửa tiền. Dòng món/size/topping/giá giữ snapshot; mã bàn và mã khuyến mãi
  trong response là tra cứu hiện tại, không phải snapshot hiển thị mới.

### Khuyến mãi

- `GET/POST /api/khuyen-mai`, `GET/PUT/DELETE /api/khuyen-mai/{id}`,
  `POST /api/khuyen-mai/validate`, `PATCH /api/khuyen-mai/{id}/resume`,
  `PATCH /api/khuyen-mai/{id}/end`.
- Quản lý xem lịch sử theo `GET /api/khuyen-mai/{id}/history` và số lượt/tổng
  tiền giảm theo `GET /api/khuyen-mai/{id}/history/summary`. Chỉ hóa đơn đã thanh
  toán được tính; hóa đơn đã hủy không tính vào thống kê.
- Danh sách có `page, pageSize, search, status`. Mã được chuẩn hóa uppercase;
  tối đa 10 ký tự; `Percentage` không vượt 100%, `FixedAmount` là số tiền,
  có giá trị đơn tối thiểu. Thời gian và mức tối thiểu là điều kiện áp dụng.
- Ngày bắt đầu đã đến thì khóa loại/mức giảm/thời gian/giá trị đơn tối thiểu;
  quản lý chỉ có thể đổi tên. `DELETE` tạm ngưng mã; `PATCH .../resume` kích hoạt
  lại mã còn trong hạn; `PATCH .../end` kết thúc sớm. Các thay đổi trạng thái đều
  ghi audit. Chương trình trong tương lai được sửa điều kiện trước ngày bắt đầu.
- Giai đoạn hiển thị gồm sắp diễn ra, đang diễn ra, tạm ngưng và đã kết thúc.
  Phạm vi hiện áp dụng cho mọi đơn đủ điều kiện; sơ đồ dữ liệu không có phân nhóm
  khách hàng/đơn hàng. Không hỗ trợ mức giảm tối đa hay điều kiện tự do vì các
  trường này không thuộc mô hình hiện tại.
- Validate nhận `{ orderId, code }`, tính từ đơn chưa thanh toán thực tế,
  không nhận subtotal/discount từ client. Preview không đảm bảo mã còn hợp lệ
  khi thanh toán: server kiểm tra lại thời gian/trạng thái/điều kiện.
- Active đã quá hạn trả trạng thái Expired; tạm ngưng/kết thúc giữ liên kết với
  hóa đơn. Sửa định nghĩa không thay đổi tiền hóa đơn đã lưu.

### Nguyên liệu và biến động kho

- `GET/POST /api/nguyen-lieu`, `GET/PUT/DELETE /api/nguyen-lieu/{id}`,
  `GET /api/nguyen-lieu/warnings`.
- `GET /api/nguyen-lieu/{id}/history`,
  `GET /api/nguyen-lieu/{id}/history/{transactionId}`.
- `POST /api/nguyen-lieu/{id}/import`, `/export`, `/adjust`, `/stocktake`.
  Cùng body `{ quantity, reason, rowVersion }`, token của nguyên liệu.
- Tạo nguyên liệu có tồn **0**; ghi tồn đầu bằng import. PUT chỉ sửa metadata,
  cần token và không thay Quantity; không đổi Unit sau khi có lịch sử.
- Import/export dùng lượng dương; export không được vượt tồn. Adjust dùng delta
  có dấu khác 0; stocktake dùng số tồn thực đếm không âm, không phải delta.
  Không trộn ý nghĩa quantity giữa các endpoint.
- SQL lock + RowVersion bảo vệ số dư; lưu tồn trước/sau, người làm và lý do
  trong cùng transaction. Kiểm kê dù tồn không đổi vẫn đổi token.
  Chỉ xóa nguyên liệu chưa có tồn, lịch sử kho hoặc định lượng; không có trạng
  thái “ngừng sử dụng” trong mô hình.
- Cảnh báo tồn khi Quantity <= MinQuantity; tồn bằng 0 là cảnh báo khẩn. Quá hạn
  khi ExpiryDate < ngày UTC
  hiện tại; sắp hết hạn từ hôm nay đến +7 ngày, bao gồm hai đầu.
- Hạn dùng và đơn vị là thuộc tính chung của nguyên liệu, không theo từng lô;
  mô hình hiện tại không lưu giá nhập theo lô.
- Danh sách/lịch sử/cảnh báo có phân trang/tìm kiếm. Giao diện kho tự làm mới
  định kỳ và hiển thị cảnh báo hết hàng. DELETE nhận `rowVersion` URL-encoded.
- Quản lý có thể thêm/sửa/xóa định lượng tại `/recipes` hoặc API
  `/api/dinh-luong`; mỗi cặp món-nguyên liệu chỉ có một dòng và định lượng phải
  dương, tối đa ba chữ số thập phân.
- Khi thanh toán thành công, hệ thống tính lượng nguyên liệu từ định lượng món
  và số lượng đã bán, rồi ghi trừ kho, lịch sử, audit và hóa đơn trong cùng
  transaction. Mỗi món chưa hủy phải có công thức và tồn đủ; nếu thiếu công
  thức/tồn thì trả `409`, không tạo hóa đơn và không trừ một phần kho.

### Nhân viên và tài khoản

- API hồ sơ dùng `/api/nhan-vien`: `GET` danh sách/chi tiết, `POST` tạo,
  `PUT /{id}` cập nhật, `DELETE /{id}` đánh dấu nghỉ việc. Danh sách hỗ trợ
  `page, pageSize, search, status, position`; tìm kiếm theo mã hoặc họ tên.
- Mã nhân viên do hệ thống sinh. Phone và email nếu có phải là duy nhất; nhân
  viên mới bắt đầu ở trạng thái Working. Chi tiết trả cả trạng thái tài khoản
  chưa có, đang hoạt động, bị khóa hoặc đã vô hiệu.
- Manager tạo tài khoản qua `POST /api/nhan-vien/{id}/account` với
  `{ username, password, role }`; nhân viên phải đang Working và chưa có tài
  khoản. Mật khẩu ban đầu tối thiểu 8 ký tự và được hash bằng ASP.NET
  `IPasswordHasher`. Manager có thể gán role nghiệp vụ/Manager nhưng không thể
  gán role Admin.
- Manager khóa/mở khóa qua `PATCH /api/nhan-vien/{id}/account/lock` với
  `{ locked: true|false }`. Không thể khóa tài khoản đang đăng nhập; thay đổi
  được audit và có hiệu lực với JWT ngay. API quản trị tổng quát
  `/api/tai-khoan` vẫn giới hạn cho Admin.
- Chuyển Resigned hoặc soft-delete vô hiệu hóa tài khoản liên kết trong cùng
  transaction. Chuyển lại Working không tự mở khóa; Manager phải mở khóa qua
  endpoint nhân viên rồi người dùng đăng nhập lại.

### Báo cáo và thống kê

- `GET /api/bao-cao/revenue?from=...&to=...`: summary toàn kỳ, tổng hóa đơn,
  giảm giá và trang doanh thu theo ngày UTC.
- `GET /api/bao-cao/top-items?from=...&to=...`: món bán chạy có search/page;
  `GET /api/bao-cao/payment-methods?from=...&to=...`: số hóa đơn và doanh thu
  theo phương thức.
- `GET /api/bao-cao/inventory?filter=all|low-stock|expiring`: tồn hiện tại có
  search/page; `low-stock` gồm tồn bằng hoặc dưới định mức, `expiring` gồm hạn
  dùng từ hôm nay đến 7 ngày tới. Đây không phải báo cáo tồn lịch sử/định giá.
- `GET /api/bao-cao/details?report=...&key=...&from=...&to=...`: danh sách hóa
  đơn đã thanh toán tương ứng một ngày, món hoặc phương thức. Mở chi tiết bằng
  `GET /api/hoa-don/{id}`.
- Trang báo cáo hỗ trợ kỳ ngày, tuần (bắt đầu thứ Hai), tháng hoặc khoảng tùy
  chọn; hiển thị biểu đồ thanh, tỷ trọng phương thức và drill-down hóa đơn.
  Ngày/tuần/tháng được chọn theo giờ địa phương rồi gửi UTC theo khoảng
  `[from,to)`; `to` không bao gồm. Chỉ invoice Paid được tính, loại Cancelled.
- Doanh thu và phương thức thanh toán là tiền **sau giảm**. Top items gom theo
  menu ID/tên món hiện tại, doanh thu dòng gồm topping **trước giảm giá cấp
  invoice**, nên không bằng tổng doanh thu thuần.
- `GET /api/bao-cao/export/{report}` hỗ trợ `revenue`, `top-items`,
  `payment-methods` và `inventory`; báo cáo tài chính cần `from/to`, tồn kho
  nhận `filter`. Export theo bộ lọc đang chọn, không phân trang. Giao diện cho
  tải Excel (`.xlsx`), PDF (`.pdf`) hoặc CSV; dữ liệu gốc không bị thay đổi.
- Không có dữ liệu → **404** với thông báo rõ ràng; khoảng không hợp lệ → 400;
  Warehouse xuất tài chính → 403. CSV là UTF-8, escape đúng trường và chặn
  công thức spreadsheet; response tải tệp không bọc JSON.

## 6. Kiểm thử

```powershell
dotnet build CafeManagement.sln
dotnet test CafeManagement.sln
```

Không có `CAFE_TEST_SQL`, test SQL được **skip rõ ràng**; các unit test vẫn chạy.
Để kiểm tra cả migration, seed và concurrency trên SQL Server:

```powershell
$env:CAFE_TEST_SQL = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test CafeManagement.sln
Remove-Item Env:\CAFE_TEST_SQL
```

Test SQL tạo database tên `CafeManagement_Test_<guid>` riêng, chạy migration và
seed hai lần, kiểm tra một bàn/một đơn active, chống invoice trùng, rowversion
trên kho, trừ kho nguyên tử khi thanh toán, cảnh báo ngưỡng/hết hàng, rollback
khi thiếu công thức/tồn và constraint tồn không âm. Database test được xóa trong
`finally`; không sử dụng database vận hành trong connection đầu vào. Tài khoản
SQL cần quyền tạo/xóa **database test**.

Unit test kiểm tra đơn phải có món, tính topping/snapshot, promotion/time/minimum/cap,
tổng sau giảm không âm, kho không âm và chỉ invoice Paid vào doanh thu.

Phase 2 có bốn kịch bản HTTP integration qua ASP.NET TestServer + SQL Server thật:
Auth/role/lock/password; snapshots/preparation/cancellation; transfer/merge/
concurrent table creation; catalog CRUD/validation/rollback. Mỗi test tạo
`CafeManagement_Test_Phase2_<guid>` riêng, migrate/seed rồi xóa trong Dispose.
Không kiểm thử bằng provider InMemory thay cho các constraint SQL Server.

```powershell
$env:CAFE_TEST_SQL = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test backend\tests\CafeManagement.Tests --filter 'FullyQualifiedName~Phase2ApiTests'
```

Phase 3 có các kịch bản HTTP/SQL về hóa đơn, khuyến mãi, tồn kho/cảnh báo,
định lượng, tiêu hao kho khi thanh toán, báo cáo/drill-down, CSV và nhân viên. Fixture dùng database
`CafeManagement_Test_Phase3_<guid>` riêng và xóa trong Dispose. Lần chạy hiện tại
không có `CAFE_TEST_SQL`: 7 test không phụ thuộc SQL pass, 16 test SQL được skip.
Vì vậy migration mới và luồng giao dịch kho cần được chạy lại trên SQL Server
theo lệnh dưới đây trước khi nghiệm thu. Nếu test host bị crash, Dispose có thể
không chạy: chỉ dọn database xác định thuộc lần test đó, không xóa hàng loạt.

```powershell
$env:CAFE_TEST_SQL = 'Server=.\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test backend\tests\CafeManagement.Tests --filter 'FullyQualifiedName~Phase3ApiTests'
dotnet ef migrations has-pending-model-changes --project backend\src\CafeManagement.Infrastructure --startup-project backend\src\CafeManagement.Api
```

## 7. Các phase

1. **Phase 1:** solution, entities/enums, DbContext, InitialCreate, seed, test nền tảng.
2. **Phase 2:** auth, tài khoản/role, bàn, menu, đơn hàng, chuyển/ghép bàn, pha chế.
3. **Phase 3:** thanh toán/hóa đơn, khuyến mãi, kho, nhân viên, báo cáo/CSV.
4. **Phase 4:** React UI tiếng Việt, auth/role guards, layout và các trang chính.
5. **Phase 5:** validation, business rules, seed hoàn chỉnh, test, README, nghiệm thu.

Không mở rộng sang mobile, giao hàng, kết nối ngân hàng/ví/máy in trực tiếp,
nhiều chi nhánh, nhận diện khuôn mặt, AI dự báo hoặc kế toán chuyên sâu.

## 8. Chạy toàn bộ hệ thống

### Cách 1: Docker Compose (khuyến nghị)

Docker Compose dựng SQL Server, API và frontend; tự áp dụng EF migrations và seed
dữ liệu demo khi API khởi động lần đầu. Chạy tại thư mục gốc solution:

```powershell
.\scripts\New-DockerEnv.ps1
docker compose up --build -d
docker compose ps
```

Mở <http://localhost:5173>. Swagger ở <http://localhost:5080/swagger>,
liveness ở <http://localhost:5080/api/health>, SQL Server được ánh xạ ra
`localhost,14330`. Tài khoản demo và mật khẩu ngẫu nhiên của lần chạy này nằm
trong file `.env` vừa tạo; không chia sẻ hoặc commit file đó.
Nếu đã có `.env` cho mục đích khác, không ghi đè; bổ sung các biến còn thiếu
theo `.env.docker.example` hoặc sao lưu file cũ trước khi tạo file mới.

API chờ SQL Server healthy, sau đó chạy migrations rồi seed. Theo dõi log nếu
đợi ứng dụng khởi động:

```powershell
docker compose logs -f api
```

`docker compose down` dừng stack nhưng giữ dữ liệu trong volume. Để xóa cả
database cục bộ và bắt đầu lại từ đầu, dùng `docker compose down -v`; thao tác
này xóa dữ liệu vĩnh viễn. Compose sử dụng SQL Server Developer và seed chỉ cho
Development, **không dùng cấu hình này cho production**.

### Cách 2: Chạy trực tiếp trên máy

Làm theo mục 2–4 để cấu hình, migrate và bật seed. Sau đó mở **hai terminal**:

```powershell
# Terminal 1 - API tại http://localhost:5080
dotnet run --project backend\src\CafeManagement.Api

# Terminal 2 - giao diện tại http://localhost:5173
Set-Location frontend\cafe-management-web
npm ci
npm run dev
```

Vite proxy `/api` tới API (`VITE_API_PROXY_TARGET`, mặc định `http://localhost:5080`;
xem `frontend\cafe-management-web\.env.example`). Đăng nhập bằng một trong sáu tài
khoản ở mục 4. Menu và route chỉ hiển thị theo role; API vẫn là nơi quyết định quyền.

Lệnh frontend khác (trong `frontend\cafe-management-web`):

```powershell
npm run build   # tsc -b + vite build
npm test        # vitest: 23 test
npm run lint    # oxlint
```

Nếu `dotnet test` hoặc `dotnet run` báo thiếu `Microsoft.NETCore.App 10.0.x`,
cài .NET 10 SDK/ASP.NET Core Runtime 10.

## 9. Validation và quy tắc nghiệp vụ chính

- Request được kiểm tra bằng DataAnnotations (bắt buộc, độ dài, khoảng giá trị,
  enum hợp lệ, URL/email/phone); lỗi trả `400` dạng `{ success:false, errors }`.
- Quy tắc chéo trường nằm ở service/domain: khuyến mãi `EndAt > StartAt` và
  phần trăm ≤ 100; size/topping không trùng; số lượng topping/món có giới hạn.
- Xung đột `RowVersion` trả `409`; thiếu quyền `403`; chưa đăng nhập `401`;
  đăng nhập có rate limit (20 lần/phút/IP).
- Frontend validate sớm bằng Zod/Ant Design, nhưng không thay thế validation phía API.

## 10. Checklist nghiệm thu

| # | Tiêu chí | Cách kiểm chứng | Kết quả |
|---|---|---|---|
| 1 | Build backend sạch | `dotnet build CafeManagement.sln` | 0 warning, 0 error |
| 2 | Backend test (domain + HTTP + SQL Server) | `dotnet test CafeManagement.sln` với `CAFE_TEST_SQL` | 7 pass; SQL cần cấu hình để chạy |
| 3 | Frontend build/test | `npm run build`, `npm test` | build OK, 23/23 pass |
| 4 | Migration trên DB mới | `dotnet ef database update` | Migration tạo xong; cần xác nhận trên SQL Server |
| 5 | Seed idempotent: 6 tài khoản/role, 6 nhân viên liên kết, 10 bàn, 6 món, 2 khuyến mãi, 4 nguyên liệu | test SQL seed hai lần + chạy API thật | OK |
| 6 | Sáu tài khoản demo đăng nhập được; sai mật khẩu → 401 | chạy API thật | OK |
| 7 | Phân quyền: không token 401; Waiter vào `/users` và `/invoices` 403; Warehouse vào doanh thu 403 | chạy API thật | OK |
| 8 | Validation: bàn rỗng 400, khuyến mãi >100% 400 | chạy API thật | OK |
| 9 | Đơn → pha chế → thanh toán → hóa đơn; trừ kho nguyên tử theo công thức | `Phase3ApiTests` với SQL Server | Chờ chạy SQL |
| 10 | Kho không âm, cảnh báo `<=`/hết hàng, lịch sử và concurrency | `Phase3ApiTests`, `SqlServerTests` | Chờ chạy SQL |
| 11 | Frontend → proxy Vite → API đăng nhập thành công | `POST http://localhost:5173/api/auth/login` | OK |
| 12 | Route guard theo role, phiên hết hạn quay về đăng nhập | `AuthContext.test.tsx` | OK |
| 13 | UC-9: lọc/tìm/chi tiết nhân viên, sinh mã, chặn liên hệ trùng, tạo tài khoản và khóa/mở | `EmployeeResignationLocksLinkedAccountWithoutAllowingRoleEscalation` với SQL Server | Chờ chạy SQL |
| 14 | Docker Compose cấu hình hợp lệ; API migrate/seed và frontend truy cập qua Nginx | `docker compose config`, build và chạy stack | Config OK; build/runtime chưa chạy vì Docker Engine chưa hoạt động |

Các mục 4–8 và 11 được chạy thủ công trên SQL Server Express cục bộ với database
tạm, sau đó đã xóa. Chưa có kiểm thử trình duyệt tự động cho từng trang; nên duyệt
thủ công mỗi role một lần trước khi nộp.