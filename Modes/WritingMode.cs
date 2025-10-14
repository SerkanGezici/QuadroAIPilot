using System.Diagnostics;

namespace QuadroAIPilot.Modes
{
    public class WritingMode : IMode
    {
        public void Enter() { /* Yazı moduna girildi */ }
        public void Exit() { /* Yazı modundan çıkıldı */ }

        public bool HandleSpeech(string text)
        {
            // Bu modda konuşma doğrudan metin kutusuna akar; burada işlem yok.
            return false; // ele almadım
        }
    }
}