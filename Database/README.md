# EasyFlips Database Documentation

## 📋 Tổng quan

Database của EasyFlips được xây dựng trên **Supabase (PostgreSQL)** với đầy đủ hệ thống bảo mật Row Level Security (RLS), storage cho file upload, và các helper functions tự động hóa.

## 🗂️ Cấu trúc Database Files

Các file SQL cần được thực thi theo thứ tự:

1. **`01_create_tables.sql`** - Tạo cấu trúc bảng
2. **`02_setup_rls.sql`** - Thiết lập bảo mật Row Level Security
3. **`03_setup_storage.sql`** - Cấu hình storage cho file upload
4. **`04_helper_functions.sql`** - Functions và triggers tự động

---

## 📊 Cấu trúc Database Schema

### 1. **profiles** - Hồ sơ người dùng
```
id (UUID, PK) → liên kết với auth.users
email (TEXT, UNIQUE)
display_name (TEXT)
avatar_url (TEXT)
created_at (TIMESTAMP)
```

**Mục đích**: Lưu thông tin chi tiết người dùng, tách biệt với auth system của Supabase

---

### 2. **classrooms** - Lớp học
```
id (UUID, PK)
name (TEXT)
room_code (TEXT, UNIQUE) → Mã phòng 8 ký tự để join
owner_id (UUID, FK → profiles.id)
is_active (BOOLEAN)
created_at (TIMESTAMP)
```

**Mục đích**: Quản lý các lớp học, mỗi lớp có 1 owner và mã phòng duy nhất

---

### 3. **members** - Thành viên lớp học
```
id (UUID, PK)
classroom_id (UUID, FK → classrooms.id)
user_id (UUID, FK → profiles.id)
role (TEXT) → 'owner' | 'member'
joined_at (TIMESTAMP)
```

**Ràng buộc**: 
- Unique(classroom_id, user_id) - 1 user chỉ join 1 lần
- Khi xóa classroom → xóa cascade tất cả members

**Mục đích**: Quản lý quan hệ many-to-many giữa users và classrooms

---

### 4. **flashcard_sets** - Bộ thẻ học
```
id (UUID, PK)
classroom_id (UUID, FK → classrooms.id)
created_by (UUID, FK → profiles.id)
title (TEXT)
description (TEXT)
is_public (BOOLEAN)
created_at (TIMESTAMP)
updated_at (TIMESTAMP)
```

**Mục đích**: Tổ chức các bộ flashcard trong lớp học

---

### 5. **flashcards** - Thẻ học
```
id (UUID, PK)
set_id (UUID, FK → flashcard_sets.id)
front_text (TEXT) → Mặt trước
back_text (TEXT) → Mặt sau
front_image_url (TEXT) → Hình mặt trước
back_image_url (TEXT) → Hình mặt sau
audio_url (TEXT) → File audio
position (INTEGER) → Thứ tự sắp xếp
created_at (TIMESTAMP)
```

**Mục đích**: Lưu nội dung chi tiết từng thẻ học

---

### 6. **learning_progress** - Tiến độ học tập
```
id (UUID, PK)
user_id (UUID, FK → profiles.id)
flashcard_id (UUID, FK → flashcards.id)
confidence_level (INTEGER) → 0-5 (độ tự tin)
last_reviewed (TIMESTAMP)
next_review (TIMESTAMP) → Spaced repetition
times_reviewed (INTEGER)
```

**Ràng buộc**: Unique(user_id, flashcard_id)

**Mục đích**: Theo dõi tiến độ học của từng user trên từng thẻ, hỗ trợ spaced repetition

---

## 🔒 Row Level Security (RLS)

### Nguyên tắc bảo mật:

#### **profiles**
- ✅ Mọi người đọc được profile của nhau
- ✅ Chỉ update profile của chính mình

#### **classrooms**
- ✅ Chỉ members mới thấy classroom
- ✅ Chỉ owner mới update/delete classroom
- ✅ Mọi user đều có thể tạo classroom mới

#### **members**
- ✅ Chỉ members trong lớp mới thấy danh sách members
- ✅ Owner có thể xóa members
- ✅ User có thể tự xóa mình khỏi lớp

#### **flashcard_sets**
- ✅ Members trong lớp thấy được tất cả sets
- ✅ Chỉ người tạo hoặc owner lớp mới sửa/xóa được

#### **flashcards**
- ✅ Members trong lớp thấy được tất cả cards
- ✅ Chỉ người tạo set hoặc owner lớp mới sửa/xóa được

#### **learning_progress**
- ✅ Chỉ thấy progress của chính mình
- ✅ Chỉ sửa progress của chính mình

---

## 📦 Storage Buckets

### **flashcard-images**
- **Public**: ✅
- **File size limit**: 5MB
- **Allowed formats**: image/jpeg, image/png, image/gif, image/webp
- **Folder structure**: `{classroom_id}/{set_id}/{filename}`

### **flashcard-audios**
- **Public**: ✅
- **File size limit**: 10MB
- **Allowed formats**: audio/mpeg, audio/wav, audio/mp3, audio/ogg
- **Folder structure**: `{classroom_id}/{set_id}/{filename}`

### **profile-avatars**
- **Public**: ✅
- **File size limit**: 2MB
- **Allowed formats**: image/jpeg, image/png, image/webp
- **Folder structure**: `{user_id}/{filename}`

---

## ⚙️ Helper Functions & Triggers

### 1. **Auto-create Profile on User Signup**

```sql
handle_new_user() → Trigger on auth.users INSERT
```

**Hoạt động**:
- Khi user đăng ký (Supabase Auth tạo record trong `auth.users`)
- Tự động tạo record tương ứng trong bảng `profiles`
- `display_name` mặc định = phần trước @ của email

**Ví dụ**:
```
User đăng ký: john.doe@gmail.com
→ Tự động tạo profile:
  - id: [same as auth.users.id]
  - email: john.doe@gmail.com
  - display_name: john.doe
```

---

### 2. **Auto-add Classroom Owner as Member**

```sql
add_owner_as_member() → Trigger on classrooms INSERT
```

**Hoạt động**:
- Khi tạo classroom mới
- Tự động thêm owner vào bảng `members` với role = 'owner'

**Ví dụ**:
```
INSERT INTO classrooms (name, owner_id, room_code)
VALUES ('Math 101', 'user-123', 'ABC12345')
→ Tự động INSERT INTO members:
  - classroom_id: [new classroom id]
  - user_id: 'user-123'
  - role: 'owner'
```

---

### 3. **Generate Unique Room Code**

```sql
generate_room_code() → RETURNS TEXT
```

**Hoạt động**:
- Tạo mã phòng ngẫu nhiên 8 ký tự
- Sử dụng ký tự: A-Z (trừ I, O) và số 2-9 (dễ đọc, tránh nhầm lẫn)
- Kiểm tra unique, nếu trùng thì generate lại

**Cách dùng từ code**:
```csharp
var roomCode = await supabase
    .Rpc("generate_room_code", null);
```

---

### 4. **Join Classroom by Room Code**

```sql
join_classroom_by_code(p_room_code TEXT, p_user_id UUID) → RETURNS JSONB
```

**Input**:
- `p_room_code`: Mã phòng (8 ký tự)
- `p_user_id`: ID của user muốn join

**Output (JSONB)**:
```json
{
  "success": true/false,
  "message": "Successfully joined classroom",
  "classroom_id": "uuid-here"
}
```

**Hoạt động**:
1. Tìm classroom theo room_code (phải is_active = true)
2. Kiểm tra xem user đã là member chưa
3. Nếu hợp lệ → INSERT vào `members` với role = 'member'

**Cách dùng từ code**:
```csharp
var result = await supabase.Rpc(
    "join_classroom_by_code",
    new Dictionary<string, object> {
        { "p_room_code", "ABC12345" },
        { "p_user_id", currentUserId }
    }
);
```

---

### 5. **Get User's Classrooms**

```sql
get_user_classrooms(p_user_id UUID) → RETURNS TABLE
```

**Input**:
- `p_user_id`: ID của user

**Output (Table)**:
```
classroom_id    | UUID
classroom_name  | TEXT
room_code       | TEXT
role            | TEXT ('owner' hoặc 'member')
owner_id        | UUID
member_count    | BIGINT
joined_at       | TIMESTAMP
```

**Hoạt động**:
- Lấy tất cả classrooms mà user tham gia
- Kèm theo thông tin role, số lượng members
- Sắp xếp theo thời gian join (mới nhất trước)

**Cách dùng từ code**:
```csharp
var classrooms = await supabase.Rpc(
    "get_user_classrooms",
    new Dictionary<string, object> {
        { "p_user_id", currentUserId }
    }
);
```

---

## 🔄 Workflow Chính

### **1. User Registration & Login**
```
1. User đăng ký qua Supabase Auth
2. Trigger handle_new_user() tự động tạo profile
3. User login và lấy session token
```

### **2. Create Classroom**
```
1. User tạo classroom mới
2. Generate room_code bằng generate_room_code()
3. INSERT vào bảng classrooms
4. Trigger add_owner_as_member() tự động thêm owner vào members
```

### **3. Join Classroom**
```
1. User nhập room_code
2. Gọi function join_classroom_by_code()
3. Function tự động:
   - Validate room_code
   - Kiểm tra duplicate membership
   - Thêm vào members nếu hợp lệ
```

### **4. Create Flashcard Set**
```
1. Member tạo set trong classroom
2. INSERT vào flashcard_sets
3. RLS tự động kiểm tra quyền (phải là member)
```

### **5. Create Flashcards**
```
1. Upload images/audio vào Storage buckets (nếu có)
2. Lấy public URL từ Storage
3. INSERT vào flashcards với URLs
4. RLS tự động kiểm tra quyền
```

### **6. Learning Flow**
```
1. User mở flashcard set để học
2. Xem từng flashcard (front → back)
3. Đánh giá confidence_level (0-5)
4. INSERT/UPDATE vào learning_progress
5. Hệ thống tính next_review date (spaced repetition)
```

---

## 🛠️ Cách Deploy Database

### ⚠️ **Lưu ý về lỗi hiển thị trong các file SQL**

> **Bạn có thể thấy các file `.sql` báo lỗi syntax trong IDE (Visual Studio, VS Code, etc.)** - **HÃY BỎ QUA!**
> 
> Nguyên nhân: Các file SQL này được viết cho **PostgreSQL/Supabase**, sử dụng các cú pháp đặc thù như:
> - `CREATE OR REPLACE FUNCTION`
> - `RETURNS TRIGGER`
> - `$$ LANGUAGE plpgsql $$`
> - `auth.uid()` (Supabase-specific)
> - `storage.buckets` (Supabase-specific)
> 
> IDE thường không hiểu các syntax này vì nó không phải T-SQL (SQL Server).
> 
> ✅ **Chỉ cần copy và chạy trực tiếp trên Supabase SQL Editor** - sẽ hoạt động bình thường!

---

### **Bước 1: Tạo Supabase Project**
1. Truy cập https://supabase.com
2. Tạo project mới
3. Lưu lại **API URL** và **anon public key**

### **Bước 2: Execute SQL Files**
Trong Supabase Dashboard → SQL Editor, chạy lần lượt:

```bash
1. 01_create_tables.sql      # Tạo bảng
2. 02_setup_rls.sql          # Bật RLS
3. 03_setup_storage.sql      # Tạo storage buckets
4. 04_helper_functions.sql   # Tạo functions
```

### **Bước 3: Configure App**
Update `AppConfig.cs` với Supabase credentials:

```csharp
public static string SupabaseUrl = "YOUR_SUPABASE_URL";
public static string SupabaseKey = "YOUR_SUPABASE_ANON_KEY";
```

---

## 🔍 Testing & Debugging

### **Test RLS Policies**
```sql
-- Test as specific user
SELECT auth.uid(); -- Check current user
SELECT * FROM classrooms; -- Should only see your classrooms
```

### **Test Functions**
```sql
-- Test generate room code
SELECT generate_room_code();

-- Test join classroom
SELECT join_classroom_by_code('TEST1234', 'your-user-id');

-- Test get user classrooms
SELECT * FROM get_user_classrooms('your-user-id');
```

### **Check Storage**
```sql
-- List all buckets
SELECT * FROM storage.buckets;

-- List files in bucket
SELECT * FROM storage.objects WHERE bucket_id = 'flashcard-images';
```

---

## 📝 Notes for Developers

### **Quan trọng**:
- ⚠️ Luôn sử dụng RPC functions từ code khi có thể (đã được SECURITY DEFINER)
- ⚠️ Không bao giờ disable RLS trên production
- ⚠️ Upload files phải validate file type và size ở client trước
- ⚠️ Cascade delete được bật → xóa classroom sẽ xóa hết data liên quan

### **Best Practices**:
- ✅ Sử dụng `auth.uid()` để lấy user hiện tại trong queries
- ✅ Luôn check `is_active = true` khi query classrooms
- ✅ Sử dụng transactions khi thao tác nhiều bảng
- ✅ Index đã được tạo sẵn cho các foreign keys

### **Common Pitfalls**:
- ❌ Quên check RLS permissions khi thêm query mới
- ❌ Upload file trực tiếp mà không validate
- ❌ Không handle race condition khi generate room_code
- ❌ Hardcode user_id thay vì dùng `auth.uid()`

---

## 📞 Support

Nếu có vấn đề về database, check theo thứ tự:

1. **RLS Policies** - Có đúng quyền không?
2. **Foreign Keys** - Có tồn tại parent record không?
3. **Triggers** - Có auto-trigger nào chặn không?
4. **Supabase Logs** - Check logs trong Dashboard

---

**Version**: 1.0  
**Last Updated**: 2025  
**Maintainer**: EasyFlips Team
