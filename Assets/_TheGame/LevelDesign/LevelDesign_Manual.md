# Level Design Tool — Hướng dẫn sử dụng

> Tài liệu dành cho **Game Designer**. Mô tả cách sử dụng tool thiết kế level trong Unity Editor.

## Mở Tool

**Menu:** `Tools → Level → Level Design`

## Giao diện

| Vùng | Vị trí | Chức năng | Ghi chú |
|------|--------|-----------|---------|
| **Left Panel** | Trái | Chọn tool + bảng màu | Kéo thanh phân cách để resize |
| **Grid Canvas** | Giữa | Lưới level, tương tác trực tiếp | Hiện thông báo *"No level data"* khi chưa tạo grid |
| **Right Panel** | Phải | Kích thước, I/O, sinh ngẫu nhiên, thống kê | Kéo thanh phân cách để resize |

### Left Panel chi tiết

| Zone | Vị trí | Nội dung |
|------|--------|----------|
| **Tools** (trên) | Các swatch 66×66px dạng lưới | Obstacle, Hidden Cube, Garage, Connection — chọn 1 tool |
| **Palettes** (dưới) | Các swatch 44×44px nhiều hàng | Bảng màu (Paint Color) + Eraser ở cuối |

---

## Hiển thị ô trên Grid

| Loại | Hình ảnh | Mô tả |
|------|---------|--------|
| **Trống** | Ô vuông xám sáng/tối xen kẽ (checkerboard) | Không chứa gì |
| **Cube (màu)** | Ô vuông tô màu + viền bóng trong | Xe có màu trên grid |
| **Cube ẩn** | Ô màu + lớp phủ tím bán trong suốt + ký hiệu **?** | Cube bị ẩn mặt (hidden) |
| **Tường (Obstacle)** | Ô nền tối + chữ **X** đỏ | Vật cản, chặn đường di chuyển |
| **Garage** | Ô nền xanh lá + mũi tên hướng (↑↓←→) + số xe ở góc dưới | Ống sinh cube |
| **Liên kết (Connection)** | Thanh nối trắng bo tròn giữa 2 ô liền kề, có icon vạch đôi | Cặp cube di chuyển cùng nhau |
| **Hover preview** | Ô bán trong suốt màu tool đang chọn | Xem trước khi Paint Color hoặc Obstacle |

---

## Bước khởi đầu

| Bước | Thao tác |
|------|----------|
| 1. Tạo lưới | Right Panel → nhập **Width** & **Height** (2–25) → bấm **Generate Grid** |
| 2. Hoặc Import | Right Panel → **Import JSON** → chọn file `.json` level có sẵn |
| 3. Chọn tool | Left Panel → click vào tool/màu muốn dùng |
| 4. Vẽ lên grid | Click/kéo chuột trái lên các ô trong lưới |
| 5. Export | Right Panel → **Export JSON** → lưu file |

---

## Các Tool

### Vùng Tools (Toggle — chọn 1 trong các tool)

| Icon | Tool | Mô tả | Click ô trống | Click ô cube | Click ô tường | Click ô garage | Kéo chuột |
|------|------|--------|---------------|-------------|---------------|----------------|-----------|
| ▦ | **Obstacle** | Đặt/xóa tường | Đặt tường | Thay bằng tường | **Xóa** tường | — | Toggle (đặt/xóa) |
| ? | **Hidden Cube** | Ẩn/hiện mặt cube | — | Bật/tắt ẩn | — | — | Chỉ ẩn (1 chiều) |
| G | **Garage** | Đặt ống sinh cube | Tạo + mở popup | — | — | Mở popup | — (không drag) |
| ↔ | **Connection** | Nối 2 cube liền kề | — | Chọn/nối cube | — | — | — (không drag) |
| ⛶ | **Select** | Chọn vùng + di chuyển | Kéo chuột để chọn vùng nhiều ô | Kéo chuột trong vùng đã chọn để di chuyển cả vùng | — | — | Xem mục **Select (Chọn + Di chuyển)** bên dưới |

### Vùng Palettes (Bảng màu)

| Icon | Tool | Mô tả | Click ô trống | Click ô cube | Click ô tường | Kéo chuột |
|------|------|--------|---------------|-------------|---------------|-----------|
| 🎨 | **Paint Color** | Tô màu cube | Đặt cube màu | Đổi màu (giữ ẩn) | Thay bằng cube | Tô liên tục |
| ✕ | **Eraser** | Xóa cube | — | Xóa cube + liên kết | — | Xóa liên tục |

> **Lưu ý:** Paint Color và Eraser **không** ảnh hưởng ô garage. Eraser **không** xóa tường.

### Thao tác chuột phải (mọi tool)

| Đối tượng | Chuột phải |
|-----------|-----------|
| Cube / Tường | Xóa ô + liên kết liên quan |
| Garage | Xóa garage |
| Đang nối (Connection) | Hủy lựa chọn |

---

## Garage Popup

Khi click garage (hoặc tạo mới), popup hiện ra bên cạnh ô:

| Phần | Mô tả |
|------|--------|
| **Thanh tiêu đề (⠿⠿ Garage)** | **Kéo thả** để di chuyển popup tự do như 1 cửa sổ |
| **Góc dưới-phải (⋱)** | **Kéo** để phóng to/thu nhỏ popup (cả chiều rộng lẫn chiều cao) |
| **Direction** | 4 nút mũi tên ↑ ↓ ← → — chọn hướng xuất cube. Nút active = xanh lá |
| **Cars queue** | Danh sách màu xe. **Chuột phải** ô màu = xóa xe đó |
| **+ (Thêm xe)** | Mở bảng chọn màu → click màu → thêm xe cuối hàng đợi |
| **✕ (Đóng)** | Hoặc click ra ngoài popup. Chuyển tool cũng đóng |

> Popup tự chuyển sang trái nếu bị tràn phải cửa sổ khi mới mở. Nếu thu nhỏ chiều cao xuống thấp hơn nội dung thực tế, nội dung bên trong tự cuộn được (thanh cuộn dọc). Kích thước bạn chỉnh được **giữ nguyên** cho các garage mở sau trong cùng phiên làm việc; vị trí thì luôn mở lại cạnh ô garage vừa click.

---

## Connection (Liên kết cube)

| Bước | Thao tác | Hiển thị |
|------|----------|----------|
| 1 | Chọn tool **Connection** | — |
| 2 | Click cube thứ nhất | Ô sáng viền vàng |
| 3a | Click cube **liền kề** | Tạo/xóa liên kết (toggle). Hiện thanh nối trắng |
| 3b | Click cube **không liền kề** hoặc ô trống | Hủy lựa chọn |
| 3c | Chuột phải | Hủy lựa chọn |

- Chỉ nối được 2 cube **liền kề** (trên/dưới/trái/phải, khoảng cách Manhattan = 1)
- Click lại cặp đã nối → **xóa** liên kết
- Khi hover ô cube liền kề đã chọn cube đầu, hiện preview thanh nối bán trong suốt

---

## Undo / Redo

| Phím | Chức năng |
|------|-----------|
| **Ctrl+Z** | Hoàn tác (Undo) |
| **Ctrl+Y** hoặc **Ctrl+Shift+Z** | Làm lại (Redo) |

Hoạt động cho: vẽ/xóa/obstacle/hidden/connection trên grid (1 nét kéo chuột = 1 bước), Garage popup (đổi hướng, thêm/xóa xe — mỗi thao tác 1 bước), **di chuyển vùng chọn (Select)**, Generate (Obstacle/Color/All), Generate Receiver Queues, Resize lưới, Clear All, và cả 3 kiểu Import (File/Clipboard/Quick Load).

> Giới hạn 50 bước gần nhất. Lịch sử Undo/Redo **không** được lưu lại khi đóng tool hoặc khi Unity recompile script (dữ liệu grid vẫn được khôi phục — xem mục dưới — nhưng lịch sử undo thì reset về rỗng).

## Phím tắt chuyển Tool

Khi không gõ chữ vào ô nhập liệu nào (ô Grid Width, Level ID, weight...):

| Phím | Tool |
|------|------|
| **1** | Paint Color |
| **2** | Obstacle |
| **3** | Hidden Cube |
| **4** | Garage |
| **5** | Connection |
| **D** | Eraser |
| **V** | Select |

## Chống mất dữ liệu khi Unity recompile

Nếu Unity biên dịch lại script (rất hay xảy ra khi vừa code vừa thiết kế level) hoặc bạn đóng/mở lại cửa sổ Level Design, dữ liệu grid **chưa lưu** sẽ tự động được khôi phục lại khi mở tool lên — không cần lo lắng mất công vẽ dở. Đây là lưới an toàn bổ sung, không thay thế cho việc Quick Save/Export định kỳ.

---

## Select (Chọn + Di chuyển)

| Bước | Thao tác | Kết quả |
|------|----------|---------|
| 1 | Chọn tool **Select** (phím tắt **V**) | — |
| 2 | Kéo chuột trên grid | Vẽ vùng chữ nhật chọn nhiều ô, viền vàng |
| 3 | Thả chuột | Xác nhận vùng đã chọn (viền vàng tĩnh) |
| 4 | Kéo chuột **từ bên trong** vùng đã chọn | Xem trước (preview mờ) vị trí sẽ chuyển tới, không vượt ra ngoài lưới |
| 5 | Thả chuột | **Di chuyển** toàn bộ nội dung vùng chọn (cube màu, tường, ẩn, garage) tới vị trí mới, **ghi đè** dữ liệu cũ ở đích |
| — | Nhấn **Esc** | Bỏ chọn vùng hiện tại |
| — | Kéo chọn vùng mới / đổi tool khác | Vùng chọn cũ bị thay thế/bỏ |

> Di chuyển cả garage (giữ nguyên hướng + hàng chờ xe) và liên kết (Connection) **nằm hoàn toàn trong** vùng chọn. Liên kết chỉ có 1 đầu trong vùng chọn sẽ bị xóa (vì đầu kia không di chuyển theo). Nếu vị trí đích đang có garage khác (không thuộc vùng chọn), garage đó sẽ bị xóa. Ctrl+Z hoàn tác được cả thao tác di chuyển.

## Thông báo hành động (Toast)

Mỗi khi thực hiện 1 hành động "lớn", 1 thông báo lớn hiện **giữa màn hình** rồi tự mờ dần biến mất sau ~2-3 giây, không cần bấm OK:

- Generate (Obstacles/Colors/All), Generate Receiver Queues
- Resize lưới, Clear All
- Import (File/Clipboard), Export (File/Clipboard), Quick Save, Quick Load
- Undo, Redo
- Tạo/xóa Garage, Nối/hủy Connection
- Di chuyển vùng chọn (Select)

> Không thông báo cho từng ô tô/xóa đơn lẻ để tránh dồn dập gây rối mắt. Các dialog cần xác nhận (Cancel/OK như xóa dữ liệu, ghi đè, cảnh báo mất dữ liệu khi resize) **vẫn giữ nguyên** — chỉ các dialog "chỉ có nút OK để thông báo thành công" (Export Complete, Generate Receiver Queues xong...) được thay bằng thông báo này.

## Chụp ảnh Board hàng loạt (Screenshot)

Nhóm **Screenshot** ở Right Panel:

| Trường | Mô tả |
|--------|--------|
| **Enable Auto Screenshot** | Bật/tắt tính năng. Tắt thì nút Capture Range bị disable |
| **From / To** | Khoảng Level ID muốn quét (bao gồm cả 2 đầu) |
| **Capture Range** | Bắt đầu quét: lần lượt Quick Load từng Level ID trong khoảng, đợi board vẽ xong rồi chụp và lưu ảnh |
| **Cancel** | Hiện khi đang chạy, dừng giữa chừng |

**Cách hoạt động:** Với mỗi ID trong khoảng, tool tìm file `Assets/_TheGame/Levels/Level_<ID>.json` (đã lưu bằng Quick Save trước đó) — nếu không thấy thì bỏ qua ID đó. Ảnh lưu vào **`Assets/_TheGame/Screenshots/Level_<ID>.png`** (thư mục tự tạo).

> ⚠️ **Đây là chụp màn hình thật (cấp hệ điều hành)**, không phải chụp riêng nội dung Unity — nên trong lúc quét:
> - Cửa sổ Level Design phải **đang hiển thị rõ trên màn hình** (không thu nhỏ, không bị cửa sổ khác đè lên đúng vùng board).
> - Tránh di chuyển/thao tác đè lên vùng board trong lúc quét.
> - Chỉ chụp đúng phần lưới ở giữa (không gồm 2 panel trái/phải).
>
> Quét sẽ **thay thế dữ liệu board hiện tại** qua từng level — nhớ Export/Quick Save trước nếu đang có việc chưa lưu (tool sẽ hỏi xác nhận 1 lần trước khi bắt đầu nếu board đang có dữ liệu).

## Đang dùng Tool nào?

Một banner nhỏ luôn hiện cố định ở góc trên-trái của lưới (không cuộn theo khi kéo scroll), cho biết tool đang chọn (ví dụ "Paint Color", "Obstacle", "Select"...) kèm màu/ô vuông nhận diện tương ứng, kể cả khi chưa chọn tool nào ("No Tool Selected").

---

## Right Panel — Chi tiết

### Grid Size (Kích thước lưới)

| Trường | Giá trị | Mặc định |
|--------|---------|---------|
| Width | 2–25 | 5 |
| Height | 2–25 | 5 |
| Generate Grid | Nút | — |

> **Thu nhỏ lưới** sẽ cảnh báo chi tiết số cube, tường, garage, liên kết bị mất ở vùng cắt. Gốc (0,0) luôn ở góc trên-trái; cột/hàng thêm/xóa ở phải và dưới.

### Level ID và Quick Save/Load

Để lưu/mở nhanh mà không cần duyệt file mỗi lần:

1. Nhập số vào ô **Level ID** ở đầu nhóm Level I/O (nhập/Import cũng tự động điền lại ID từ `levelIndex` trong JSON).
2. **Quick Save**: ghi thẳng ra `Assets/_TheGame/Levels/Level_<ID>.json` (thư mục tự tạo nếu chưa có), **không** hiện dialog "thành công" để giữ thao tác nhanh — chỉ hiện dialog khi thiếu ID, chưa có dữ liệu, hoặc file đã tồn tại (hỏi ghi đè).
3. **Quick Load**: nhập đúng ID rồi bấm, tool tự tìm file `Level_<ID>.json` trong thư mục trên. Nếu đang có dữ liệu chưa lưu, sẽ hỏi xác nhận thay thế giống Import thường.

> Level ID = 0 là hợp lệ (không bị coi là "chưa nhập"). Import File/Import Clipboard/Export File/Export Clipboard vẫn hoạt động như cũ, dùng khi cần chọn file thủ công.

### Import / Export

| Nút | Chức năng | Dialog xác nhận |
|-----|-----------|-----------------|
| **Quick Save** | Lưu nhanh theo Level ID, không hộp thoại chọn file | Chỉ khi lỗi/thiếu ID/ghi đè |
| **Quick Load** | Mở nhanh theo Level ID, không hộp thoại chọn file | "Import will replace current level data." nếu đang có dữ liệu |
| **Import Level Data File** | Mở file `.json` → load level | "Import will replace current level data." nếu đang có dữ liệu |
| **Import Level Data From Clipboard** | Dán JSON từ clipboard → load level | "Import will replace current level data." nếu đang có dữ liệu |
| **Export Level Data File** | Lưu ra file `.json` (tên mặc định: `Level_<index>.json`) | Báo lỗi nếu không có data; thông báo thành công kèm đường dẫn |
| **Export Level Data To Clipboard** | Copy JSON vào clipboard | Báo lỗi nếu không có data; thông báo thành công |
| **Clear Board** | Xóa màu, obstacle, garage, connection, hidden trên toàn bộ lưới — giữ nguyên kích thước lưới, Level ID, cấu hình Generate | Không hỏi xác nhận (Ctrl+Z hoàn tác được); báo nếu board đang trống |
| **Clear All** | Xóa toàn bộ, reset lưới về 5×5 | "Are you sure...?" (Ctrl+Z hoàn tác được) |

> **Bảo toàn dữ liệu:** Khi Export, các trường không chỉnh sửa được trong tool (ice, direction mode, passengers, color palette override...) được giữ nguyên từ lần Import gần nhất.

### Generate (Sinh ngẫu nhiên)

#### Obstacle (Tường)

| Cài đặt | Mô tả | Mặc định |
|---------|--------|---------|
| **Area Ratio** | Min–Max % diện tích là tường (0–100) | Min=10, Max=25 |
| **Symmetry** | Kiểu đối xứng | None |
| **Density** | Trọng số 4 kiểu phân bố (tổng = 1.0) | Scattered=1.0, còn lại=0.0 |

| Kiểu phân bố | Mô tả |
|---------------|--------|
| **Scattered** | Rải rác ngẫu nhiên đều |
| **Clustered** | Cụm dày lan từ các điểm gốc (BFS) |
| **Line** | Đường cong/thẳng với quán tính hướng |
| **Funnel** | Hình phễu chữ V từ mép lưới |

| Kiểu đối xứng | Mô tả |
|----------------|--------|
| **None** | Không đối xứng |
| **Horizontal ↔** | Đối xứng trái-phải |
| **Vertical ↕** | Đối xứng trên-dưới |
| **Both ✦** | Đối xứng 4 góc |

> Sau khi sinh, thuật toán **EnsureReachability** tự động xóa các tường chặn đường đi (BFS từ hàng 0).

#### Color (Màu)

| Cài đặt | Mô tả |
|---------|--------|
| **Count + Weight + Randomize Colors** | Nhập số lượng màu muốn dùng + trọng số áp dụng chung, bấm **Randomize Colors** → tool tự random chọn N màu từ palette và điền sẵn trọng số. **Thay thế** toàn bộ danh sách màu hiện tại. Sau khi random xong vẫn sửa được từng màu/trọng số bình thường |
| **+ Add Color** | Mở bảng chọn màu, thêm thủ công từng màu một. Không thêm trùng |
| **Weight field** | Số nguyên (0–9999). Hệ thống tự tính tỷ lệ = weight / tổng weight |
| **✕** | Xóa màu khỏi danh sách |

#### Nút sinh

| Nút | Yêu cầu | Kết quả |
|-----|---------|---------|
| **Generate Obstacles** | Có lưới | Sinh tường (giữ cube/garage) |
| **Generate Colors** | Có lưới + ≥1 màu + mọi màu có weight > 0 | Sinh màu cube (giữ tường/garage) |
| **Generate All** | Cả hai điều kiện trên | Xóa tất cả → sinh tường + màu |

> Dialog lỗi sẽ hiện nếu: chưa tạo grid, chưa thêm màu, hoặc có màu chưa nhập weight.

### Generate Receiver Queues (Sinh hàng chờ Receiver)

Sinh tự động hàng chờ receiver (passengersQueuesData) dựa trên cube hiện có trên grid và trong garage.

| Trường | Mô tả | Mặc định |
|--------|--------|---------|
| **Queues Amount** | Số lượng hàng chờ (≥ 1) | — (bắt buộc nhập) |
| **Clear Ratio** | Số receiver sinh ra cho mỗi cube | 3 |
| **Generate Receiver Queues** | Nút sinh | — |

**Thuật toán:** Mỗi cube (trên grid + trong garage) sinh ra `Clear Ratio` receiver cùng màu. Tổng receiver = `Clear Ratio × tổng cube`. Receiver được xáo trộn ngẫu nhiên rồi phân bổ đều vào các hàng chờ (chênh lệch tối đa 1 đơn vị).

**Điều kiện:**
- Grid phải có ít nhất 1 cube
- Queues Amount phải ≥ 1
- Clear Ratio phải ≥ 1
- Queues Amount không vượt quá `Clear Ratio × tổng cube` (nếu không thì không đủ receiver để chia)

> Khi Export, receiver đã generate sẽ ghi đè `passengersQueuesData` trong JSON. Nếu chưa generate, dữ liệu import gốc được giữ nguyên.

**Hiển thị trực quan:** Sau khi generate hoặc import level data có chứa `passengersQueuesData`, các hàng chờ hiển thị **phía trên grid** (giống layout ingame). Mỗi cột = 1 hàng chờ, mỗi ô vuông = 1 receiver có màu tương ứng. Đầu hàng (ra trước) ở dưới cùng gần grid, cuối hàng (vào sau) ở trên. Khi Clear All, queues biến mất.

### Statistics (Thống kê)

| Thông tin | Mô tả |
|-----------|--------|
| **Cubes** | Tổng cube có màu trên grid + xe trong garage |
| **Obstacles** | Tổng tường |
| **Connections** | Tổng cặp liên kết |
| **Garages** | Tổng garage |
| **Colors Used** | Số màu **khác nhau** đang thực sự dùng (trên grid + trong garage) / tổng số màu có trong palette, ví dụ `5 / 28` |
| **Color Distribution** | Biểu đồ cột phân bố màu. Bao gồm cả xe trong garage. Trục Y: số lượng (0, giữa, max). Mỗi cột có ô màu bên dưới |

> Thống kê tự cập nhật ngay khi thay đổi bất kỳ ô nào.

---

## Quy tắc quan trọng

| Quy tắc | Chi tiết |
|---------|---------|
| **Không chồng đối tượng** | Mỗi ô chỉ chứa 1 loại: cube HOẶC tường HOẶC garage HOẶC trống |
| **Garage được bảo vệ** | Paint Color, Obstacle, Eraser đều **không** ghi đè lên garage |
| **Obstacle xóa cube** | Đặt tường lên ô cube → xóa cube + liên kết + dữ liệu import |
| **Paint giữ trạng thái** | Đổi màu cube → giữ nguyên trạng thái ẩn + dữ liệu import (ice, direction) |
| **Bảo toàn round-trip** | Import → chỉnh sửa → Export giữ nguyên mọi trường không hiển thị trong tool |
| **Undo/Redo có giới hạn** | Ctrl+Z/Ctrl+Y hoàn tác được hầu hết thao tác, tối đa 50 bước gần nhất, không lưu qua các phiên (xem mục Undo/Redo) |
| **Chống mất dữ liệu** | Dữ liệu chưa lưu tự khôi phục khi mở lại tool/sau khi Unity recompile (xem mục tương ứng) |
