-- =====================================================
-- 5. FLASHCARDS & SYNC TABLES SETUP
-- EasyFlips Application
-- =====================================================

-- =====================================================
-- 1. DECKS TABLE
-- Tương ứng với Models/Deck.cs
-- =====================================================
CREATE TABLE public.decks (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE, -- Link với Auth User
    name TEXT NOT NULL,
    description TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    last_synced_at TIMESTAMP WITH TIME ZONE
);

-- Index cho user để query deck nhanh
CREATE INDEX idx_decks_user_id ON public.decks(user_id);

-- Trigger cập nhật updated_at
CREATE TRIGGER update_decks_updated_at
    BEFORE UPDATE ON public.decks
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 2. CARDS TABLE
-- Tương ứng với Models/Card.cs
-- =====================================================
CREATE TABLE public.cards (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    deck_id UUID NOT NULL REFERENCES public.decks(id) ON DELETE CASCADE,
    
    -- Nội dung thẻ
    front_text TEXT NOT NULL,
    front_image_path TEXT, -- Lưu đường dẫn file trên Storage
    front_audio_path TEXT,
    
    back_text TEXT NOT NULL,
    back_image_path TEXT,
    back_audio_path TEXT,
    
    answer TEXT,
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- Index cho deck_id
CREATE INDEX idx_cards_deck_id ON public.cards(deck_id);

-- Trigger cập nhật updated_at
CREATE TRIGGER update_cards_updated_at
    BEFORE UPDATE ON public.cards
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 3. CARD PROGRESSES TABLE
-- Tương ứng với Models/CardProgress.cs
-- =====================================================
CREATE TABLE public.card_progresses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    card_id UUID NOT NULL REFERENCES public.cards(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE, -- Quan trọng để phân biệt tiến độ của ai
    
    -- Thuật toán SM2
    due_date TIMESTAMP WITH TIME ZONE NOT NULL,
    interval FLOAT8 NOT NULL DEFAULT 0,
    ease_factor FLOAT8 NOT NULL DEFAULT 2.5,
    repetitions INTEGER NOT NULL DEFAULT 0,
    last_review_date TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    
    -- Ràng buộc: Một người chỉ có 1 progress cho 1 thẻ (hiện tại)
    UNIQUE(card_id, user_id)
);

-- Index
CREATE INDEX idx_card_progresses_user_id ON public.card_progresses(user_id);
CREATE INDEX idx_card_progresses_due_date ON public.card_progresses(due_date);

-- Trigger cập nhật updated_at
CREATE TRIGGER update_card_progresses_updated_at
    BEFORE UPDATE ON public.card_progresses
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- 4. ROW LEVEL SECURITY (RLS)
-- Bảo mật dữ liệu: Ai tạo người nấy dùng
-- =====================================================

-- Enable RLS
ALTER TABLE public.decks ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.cards ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.card_progresses ENABLE ROW LEVEL SECURITY;

-- --- DECKS POLICIES ---

CREATE POLICY "Users can view own decks" ON public.decks
    FOR SELECT USING (auth.uid() = user_id);

CREATE POLICY "Users can insert own decks" ON public.decks
    FOR INSERT WITH CHECK (auth.uid() = user_id);

CREATE POLICY "Users can update own decks" ON public.decks
    FOR UPDATE USING (auth.uid() = user_id);

CREATE POLICY "Users can delete own decks" ON public.decks
    FOR DELETE USING (auth.uid() = user_id);

-- --- CARDS POLICIES ---
-- Card thuộc về Deck, nên check quyền dựa trên Deck Owner
-- Hoặc đơn giản hóa: Nếu user sở hữu Deck thì sở hữu Card

CREATE POLICY "Users can view cards in own decks" ON public.cards
    FOR SELECT USING (
        EXISTS (SELECT 1 FROM public.decks WHERE id = deck_id AND user_id = auth.uid())
    );

CREATE POLICY "Users can insert cards to own decks" ON public.cards
    FOR INSERT WITH CHECK (
        EXISTS (SELECT 1 FROM public.decks WHERE id = deck_id AND user_id = auth.uid())
    );

CREATE POLICY "Users can update cards in own decks" ON public.cards
    FOR UPDATE USING (
        EXISTS (SELECT 1 FROM public.decks WHERE id = deck_id AND user_id = auth.uid())
    );

CREATE POLICY "Users can delete cards in own decks" ON public.cards
    FOR DELETE USING (
        EXISTS (SELECT 1 FROM public.decks WHERE id = deck_id AND user_id = auth.uid())
    );

-- --- PROGRESS POLICIES ---

CREATE POLICY "Users can manage own progress" ON public.card_progresses
    FOR ALL USING (auth.uid() = user_id);