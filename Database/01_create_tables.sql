-- =====================================================
-- SUPABASE DATABASE SCHEMA SETUP
-- EasyFlips Application
-- =====================================================

-- Drop existing tables if they exist (for clean setup)
DROP TABLE IF EXISTS public.members CASCADE;
DROP TABLE IF EXISTS public.classrooms CASCADE;
DROP TABLE IF EXISTS public.profiles CASCADE;

-- =====================================================
-- 1. PROFILES TABLE
-- L?u thông tin profile c?a ng??i dùng
-- =====================================================
CREATE TABLE public.profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    email TEXT UNIQUE NOT NULL,
    display_name TEXT,
    avatar_url TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Index cho tìm ki?m nhanh
CREATE INDEX idx_profiles_email ON public.profiles(email);

-- Trigger t? ??ng c?p nh?t updated_at
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_profiles_updated_at
    BEFORE UPDATE ON public.profiles
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 2. CLASSROOMS TABLE
-- L?u thông tin l?p h?c/phòng h?c
-- =====================================================
CREATE TABLE public.classrooms (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    description TEXT,
    room_code TEXT UNIQUE NOT NULL, -- Mã phòng ?? tham gia (ví d?: CLASS_DEMO_01)
    owner_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    is_active BOOLEAN DEFAULT TRUE
);

-- Index cho tìm ki?m
CREATE INDEX idx_classrooms_owner_id ON public.classrooms(owner_id);
CREATE INDEX idx_classrooms_room_code ON public.classrooms(room_code);

-- Trigger t? ??ng c?p nh?t updated_at
CREATE TRIGGER update_classrooms_updated_at
    BEFORE UPDATE ON public.classrooms
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 3. MEMBERS TABLE
-- L?u thành viên trong l?p h?c
-- =====================================================
CREATE TABLE public.members (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    classroom_id UUID NOT NULL REFERENCES public.classrooms(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    role TEXT NOT NULL DEFAULT 'member', -- 'owner', 'admin', 'member'
    joined_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    UNIQUE(classroom_id, user_id) -- M?t user ch? có th? tham gia m?t l?p m?t l?n
);

-- Index cho query nhanh
CREATE INDEX idx_members_classroom_id ON public.members(classroom_id);
CREATE INDEX idx_members_user_id ON public.members(user_id);

-- =====================================================
-- COMMENTS
-- =====================================================
COMMENT ON TABLE public.profiles IS 'User profiles linked to auth.users';
COMMENT ON TABLE public.classrooms IS 'Virtual classrooms/rooms for group study';
COMMENT ON TABLE public.members IS 'Classroom membership records';

COMMENT ON COLUMN public.classrooms.room_code IS 'Unique code for joining classroom';
COMMENT ON COLUMN public.members.role IS 'User role in classroom: owner, admin, or member';
