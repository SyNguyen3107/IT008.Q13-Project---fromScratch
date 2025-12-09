-- =====================================================
-- ROW LEVEL SECURITY (RLS) POLICIES
-- EasyFlips Application
-- =====================================================

-- =====================================================
-- 1. ENABLE RLS ON ALL TABLES
-- =====================================================
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.classrooms ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.members ENABLE ROW LEVEL SECURITY;

-- =====================================================
-- 2. PROFILES TABLE POLICIES
-- =====================================================

-- Policy: Users can view all profiles (for searching/displaying)
CREATE POLICY "Profiles are viewable by everyone"
    ON public.profiles
    FOR SELECT
    USING (true);

-- Policy: Users can insert their own profile
CREATE POLICY "Users can insert their own profile"
    ON public.profiles
    FOR INSERT
    WITH CHECK (auth.uid() = id);

-- Policy: Users can update their own profile
CREATE POLICY "Users can update their own profile"
    ON public.profiles
    FOR UPDATE
    USING (auth.uid() = id)
    WITH CHECK (auth.uid() = id);

-- Policy: Users can delete their own profile
CREATE POLICY "Users can delete their own profile"
    ON public.profiles
    FOR DELETE
    USING (auth.uid() = id);

-- =====================================================
-- 3. CLASSROOMS TABLE POLICIES
-- =====================================================

-- Policy: Users can view all active classrooms
CREATE POLICY "Classrooms are viewable by everyone"
    ON public.classrooms
    FOR SELECT
    USING (is_active = true);

-- Policy: Authenticated users can create classrooms
CREATE POLICY "Authenticated users can create classrooms"
    ON public.classrooms
    FOR INSERT
    WITH CHECK (auth.uid() = owner_id);

-- Policy: Owners can update their classrooms
CREATE POLICY "Owners can update their classrooms"
    ON public.classrooms
    FOR UPDATE
    USING (auth.uid() = owner_id)
    WITH CHECK (auth.uid() = owner_id);

-- Policy: Owners can delete their classrooms
CREATE POLICY "Owners can delete their classrooms"
    ON public.classrooms
    FOR DELETE
    USING (auth.uid() = owner_id);

-- =====================================================
-- 4. MEMBERS TABLE POLICIES
-- =====================================================

-- Policy: Users can view members of classrooms they are in
CREATE POLICY "Users can view members of their classrooms"
    ON public.members
    FOR SELECT
    USING (
        auth.uid() IN (
            SELECT user_id 
            FROM public.members 
            WHERE classroom_id = members.classroom_id
        )
    );

-- Policy: Users can join classrooms (insert themselves)
CREATE POLICY "Users can join classrooms"
    ON public.members
    FOR INSERT
    WITH CHECK (auth.uid() = user_id);

-- Policy: Owners can add members to their classrooms
CREATE POLICY "Owners can add members to their classrooms"
    ON public.members
    FOR INSERT
    WITH CHECK (
        auth.uid() IN (
            SELECT owner_id 
            FROM public.classrooms 
            WHERE id = classroom_id
        )
    );

-- Policy: Owners can remove members from their classrooms
CREATE POLICY "Owners can remove members from their classrooms"
    ON public.members
    FOR DELETE
    USING (
        auth.uid() IN (
            SELECT owner_id 
            FROM public.classrooms 
            WHERE id = classroom_id
        )
    );

-- Policy: Users can remove themselves from classrooms
CREATE POLICY "Users can leave classrooms"
    ON public.members
    FOR DELETE
    USING (auth.uid() = user_id);

-- Policy: Owners can update member roles
CREATE POLICY "Owners can update member roles"
    ON public.members
    FOR UPDATE
    USING (
        auth.uid() IN (
            SELECT owner_id 
            FROM public.classrooms 
            WHERE id = classroom_id
        )
    )
    WITH CHECK (
        auth.uid() IN (
            SELECT owner_id 
            FROM public.classrooms 
            WHERE id = classroom_id
        )
    );
