-- =====================================================
-- 6. SETUP REALTIME
-- Bật tính năng lắng nghe thay đổi dữ liệu cho bảng Members
-- =====================================================

-- 1. Thêm bảng members vào publication 'supabase_realtime'
-- Điều này cho phép Client lắng nghe các sự kiện INSERT/UPDATE/DELETE trên bảng này
ALTER PUBLICATION supabase_realtime ADD TABLE public.members;

-- 2. (Tùy chọn) Cấu hình Replica Identity để nhận đầy đủ dữ liệu cũ khi Update/Delete
-- Mặc định Insert đã có đầy đủ data (NEW row) nên dòng này chỉ để chắc chắn cho các tính năng sau này
ALTER TABLE public.members REPLICA IDENTITY FULL;