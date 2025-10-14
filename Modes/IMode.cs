namespace QuadroAIPilot.Modes
{
    public interface IMode
    {
        void Enter();
        void Exit();
        bool HandleSpeech(string text);
    }
}