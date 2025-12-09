-- =====================================================
-- STORAGE BUCKET SETUP
-- EasyFlips Application - Avatar Storage
-- =====================================================

-- Note: Storage buckets must be created via Supabase Dashboard or Storage API
-- This file contains the SQL commands for bucket policies
-- Create bucket via Dashboard first: Storage > Create bucket > Name: "avatars"

-- =====================================================
-- 1. STORAGE BUCKET POLICIES
-- =====================================================

-- Policy: Anyone can view avatars (public read)
CREATE POLICY "Avatar images are publicly accessible"
    ON storage.objects
    FOR SELECT
    USING (bucket_id = 'avatars');

-- Policy: Users can upload their own avatar
-- File path format: {user_id}/avatar.{extension}
CREATE POLICY "Users can upload their own avatar"
    ON storage.objects
    FOR INSERT
    WITH CHECK (
        bucket_id = 'avatars' AND
        (auth.uid())::text = (storage.foldername(name))[1]
    );

-- Policy: Users can update their own avatar
CREATE POLICY "Users can update their own avatar"
    ON storage.objects
    FOR UPDATE
    USING (
        bucket_id = 'avatars' AND
        (auth.uid())::text = (storage.foldername(name))[1]
    )
    WITH CHECK (
        bucket_id = 'avatars' AND
        (auth.uid())::text = (storage.foldername(name))[1]
    );

-- Policy: Users can delete their own avatar
CREATE POLICY "Users can delete their own avatar"
    ON storage.objects
    FOR DELETE
    USING (
        bucket_id = 'avatars' AND
        (auth.uid())::text = (storage.foldername(name))[1]
    );
