-- =====================================================
-- HELPER FUNCTIONS & TRIGGERS
-- EasyFlips Application
-- =====================================================

-- =====================================================
-- 1. AUTO-CREATE PROFILE ON USER SIGNUP
-- Trigger ?? t? ??ng t?o profile khi user ??ng ký
-- =====================================================
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.profiles (id, email, display_name)
    VALUES (
        NEW.id,
        NEW.email,
        COALESCE(NEW.raw_user_meta_data->>'display_name', split_part(NEW.email, '@', 1))
    );
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Trigger fires after user is created in auth.users
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_new_user();

-- =====================================================
-- 2. AUTO-ADD CLASSROOM OWNER AS MEMBER
-- Khi t?o classroom, t? ??ng thêm owner vào members
-- =====================================================
CREATE OR REPLACE FUNCTION public.add_owner_as_member()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.members (classroom_id, user_id, role)
    VALUES (NEW.id, NEW.owner_id, 'owner');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER on_classroom_created
    AFTER INSERT ON public.classrooms
    FOR EACH ROW
    EXECUTE FUNCTION public.add_owner_as_member();

-- =====================================================
-- 3. FUNCTION TO GENERATE UNIQUE ROOM CODE
-- T?o mã phòng ng?u nhiên và unique
-- =====================================================
CREATE OR REPLACE FUNCTION public.generate_room_code()
RETURNS TEXT AS $$
DECLARE
    chars TEXT := 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
    result TEXT := '';
    i INTEGER;
    code_exists BOOLEAN;
BEGIN
    LOOP
        result := '';
        FOR i IN 1..8 LOOP
            result := result || substr(chars, floor(random() * length(chars) + 1)::int, 1);
        END LOOP;
        
        -- Check if code already exists
        SELECT EXISTS(SELECT 1 FROM public.classrooms WHERE room_code = result) INTO code_exists;
        
        IF NOT code_exists THEN
            EXIT;
        END IF;
    END LOOP;
    
    RETURN result;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- 4. FUNCTION TO JOIN CLASSROOM BY ROOM CODE
-- Hàm ?? user tham gia l?p h?c b?ng room code
-- =====================================================
CREATE OR REPLACE FUNCTION public.join_classroom_by_code(
    p_room_code TEXT,
    p_user_id UUID
)
RETURNS JSONB AS $$
DECLARE
    v_classroom_id UUID;
    v_already_member BOOLEAN;
BEGIN
    -- Find classroom by room code
    SELECT id INTO v_classroom_id
    FROM public.classrooms
    WHERE room_code = p_room_code AND is_active = true;
    
    IF v_classroom_id IS NULL THEN
        RETURN jsonb_build_object(
            'success', false,
            'message', 'Invalid room code or classroom is not active'
        );
    END IF;
    
    -- Check if already a member
    SELECT EXISTS(
        SELECT 1 FROM public.members
        WHERE classroom_id = v_classroom_id AND user_id = p_user_id
    ) INTO v_already_member;
    
    IF v_already_member THEN
        RETURN jsonb_build_object(
            'success', false,
            'message', 'You are already a member of this classroom'
        );
    END IF;
    
    -- Add as member
    INSERT INTO public.members (classroom_id, user_id, role)
    VALUES (v_classroom_id, p_user_id, 'member');
    
    RETURN jsonb_build_object(
        'success', true,
        'message', 'Successfully joined classroom',
        'classroom_id', v_classroom_id
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- =====================================================
-- 5. FUNCTION TO GET USER'S CLASSROOMS
-- L?y danh sách l?p h?c mà user tham gia
-- =====================================================
CREATE OR REPLACE FUNCTION public.get_user_classrooms(p_user_id UUID)
RETURNS TABLE (
    classroom_id UUID,
    classroom_name TEXT,
    room_code TEXT,
    role TEXT,
    owner_id UUID,
    member_count BIGINT,
    joined_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        c.id,
        c.name,
        c.room_code,
        m.role,
        c.owner_id,
        (SELECT COUNT(*) FROM public.members WHERE classroom_id = c.id),
        m.joined_at
    FROM public.classrooms c
    INNER JOIN public.members m ON c.id = m.classroom_id
    WHERE m.user_id = p_user_id AND c.is_active = true
    ORDER BY m.joined_at DESC;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
